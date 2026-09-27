using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Hosting;
using Testcontainers.PostgreSql;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>PostgreSQL 上でスケジュール発火と手動実行が Start を受理する。</summary>
/// <remarks>sqlite は Dispatcher のトランザクション内 Start を入れ子にできないため、この経路だけシナリオで固定する。</remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(ExecutionScheduleFireScenarioCollection))]
public sealed class ExecutionScheduleFireScenarioTests
{
    private static readonly DateTime SlotUtc = new(2026, 9, 19, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueNowUtc = new(2026, 9, 19, 3, 0, 30, DateTimeKind.Utc);
    private static readonly DateTime NextSlotUtc = new(2026, 9, 20, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FollowingSlotUtc = new(2026, 9, 21, 3, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private const string NoopWorkflowYaml = """
        workflow:
          name: W
        states:
          A:
            action: statevia.action.builtin.execution.noop
            on:
              Completed:
                end: true
        """;

    private readonly ExecutionScheduleFireScenarioFixture _fixture;

    /// <summary>共有 PostgreSQL と scheduler プロセス DI を受け取る。</summary>
    public ExecutionScheduleFireScenarioTests(ExecutionScheduleFireScenarioFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>期限到来で run-as SA の Start が work item に載り、主体は scheduler になる。</summary>
    [SkippableFact]
    public async Task DispatchDue_StartsExecutionOwnedByServiceAccount()
    {
        // Arrange
        Skip.IfNot(_fixture.IsReady, _fixture.SkipReason);
        await using var context = await FireArrangement.OpenAsync(_fixture, "nightly");
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync();
        await context.SetNextFireAtAsync(created.ScheduleId, SlotUtc);

        // Act
        var processed = await context.Dispatch.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        await using var verify = await context.CreateDbAsync();
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters()
            .SingleAsync(row => row.ScheduleId == created.ScheduleId);
        Assert.Equal(NextSlotUtc, schedule.NextFireAt);
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters()
            .SingleAsync(row => row.ScheduleId == created.ScheduleId);
        Assert.Equal(ExecutionScheduleRunOutcomes.Started, run.Outcome);
        Assert.False(run.Manual);
        var execution = await verify.Executions.IgnoreQueryFilters()
            .SingleAsync(row => row.ExecutionId == run.ExecutionId);
        Assert.Equal(ExecutionProjectionStatuses.Running, execution.Status);
        Assert.NotNull(execution.SecuritySnapshotJson);
        using (var snapshot = JsonDocument.Parse(execution.SecuritySnapshotJson))
        {
            Assert.Equal(context.RunAsPrincipalId, snapshot.RootElement.GetProperty("startedByPrincipalId").GetGuid());
        }

        var startedEvent = await verify.EventStore.IgnoreQueryFilters()
            .SingleAsync(row => row.ExecutionId == execution.ExecutionId);
        Assert.Equal(nameof(EventStoreEventType.WorkflowStarted), startedEvent.Type);
        Assert.Equal("scheduler", startedEvent.ActorKind);
        Assert.Equal(created.ScheduleId.ToString("D"), startedEvent.ActorId);
        var workItem = await verify.ExecutionWorkItems.IgnoreQueryFilters()
            .SingleAsync(row => row.ExecutionId == execution.ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Start, workItem.Kind);
    }

    /// <summary>overlap skip で非終端の実行が残っている枠は Start せず次枠へ進む。</summary>
    [SkippableFact]
    public async Task DispatchDue_WhenOverlapSkipAndRunning_SkipsWithoutSecondStart()
    {
        // Arrange
        Skip.IfNot(_fixture.IsReady, _fixture.SkipReason);
        await using var context = await FireArrangement.OpenAsync(_fixture, "overlap");
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync();
        await context.SetNextFireAtAsync(created.ScheduleId, SlotUtc);
        var first = await context.Dispatch.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);
        Assert.True(first >= 1);

        // Act
        var processed = await context.Dispatch.DispatchDueAsync(NextSlotUtc.AddSeconds(30), limit: 64, CancellationToken.None);

        // Assert
        Assert.True(processed >= 1);
        await using var verify = await context.CreateDbAsync();
        var runs = await verify.ExecutionScheduleRuns.IgnoreQueryFilters()
            .Where(row => row.ScheduleId == created.ScheduleId)
            .OrderBy(row => row.ScheduledFireAt)
            .ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.Equal(ExecutionScheduleRunOutcomes.Started, runs[0].Outcome);
        Assert.Equal(ExecutionScheduleRunOutcomes.SkippedOverlap, runs[1].Outcome);
        Assert.Null(runs[1].ExecutionId);
        Assert.Equal(1, await verify.Executions.IgnoreQueryFilters().CountAsync(row => row.ExecutionId == runs[0].ExecutionId));
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters()
            .SingleAsync(row => row.ScheduleId == created.ScheduleId);
        Assert.Equal(FollowingSlotUtc, schedule.NextFireAt);
    }

    /// <summary>手動実行は Start するが cron の next は進めない。</summary>
    [SkippableFact]
    public async Task RunNow_StartsWithoutAdvancingNextFire()
    {
        // Arrange
        Skip.IfNot(_fixture.IsReady, _fixture.SkipReason);
        await using var context = await FireArrangement.OpenAsync(_fixture, "manual");
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync();

        // Act
        var started = await context.Schedules.RunNowAsync(created.ScheduleId, "manual-key", CancellationToken.None);

        // Assert
        Assert.Equal(ExecutionProjectionStatuses.Running, started.Status);
        await using var verify = await context.CreateDbAsync();
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters()
            .SingleAsync(row => row.ScheduleId == created.ScheduleId);
        Assert.Equal(created.NextFireAt, schedule.NextFireAt);
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters()
            .SingleAsync(row => row.ScheduleId == created.ScheduleId);
        Assert.True(run.Manual);
        Assert.Null(run.ScheduledFireAt);
        Assert.Equal(ExecutionScheduleRunOutcomes.Started, run.Outcome);
        Assert.Equal(started.ResourceId, run.ExecutionId);
        var workItem = await verify.ExecutionWorkItems.IgnoreQueryFilters()
            .SingleAsync(row => row.ExecutionId == started.ResourceId);
        Assert.Equal(ExecutionWorkItemKinds.Start, workItem.Kind);
    }

    /// <summary>1 テスト分の定義・運用者・run-as SA。</summary>
    private sealed class FireArrangement : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        private FireArrangement(
            AsyncServiceScope scope,
            Guid definitionId,
            Guid creatorPrincipalId,
            Guid runAsPrincipalId,
            string scheduleName)
        {
            _scope = scope;
            DefinitionId = definitionId;
            CreatorPrincipalId = creatorPrincipalId;
            RunAsPrincipalId = runAsPrincipalId;
            ScheduleName = scheduleName;
            Accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        }

        public Guid DefinitionId { get; }

        public Guid CreatorPrincipalId { get; }

        public Guid RunAsPrincipalId { get; }

        public string ScheduleName { get; }

        public ITenantContextAccessor Accessor { get; }

        public IExecutionScheduleService Schedules =>
            _scope.ServiceProvider.GetRequiredService<IExecutionScheduleService>();

        public IExecutionScheduleDispatchService Dispatch =>
            _scope.ServiceProvider.GetRequiredService<IExecutionScheduleDispatchService>();

        public static async Task<FireArrangement> OpenAsync(
            ExecutionScheduleFireScenarioFixture fixture,
            string namePrefix)
        {
            var scope = fixture.Provider!.CreateAsyncScope();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var compiler = scope.ServiceProvider.GetRequiredService<IDefinitionCompilerService>();
            var compiled = compiler.ValidateAndCompile("W", NoopWorkflowYaml);
            var definitionId = Guid.NewGuid();
            var creatorPrincipalId = Guid.NewGuid();
            var runAsPrincipalId = Guid.NewGuid();
            var serviceAccountId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var hasher = new PasswordCredentialService();
            await using (var seed = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>()
                .CreateDbContextAsync())
            {
                seed.Projects.Add(new ProjectRow
                {
                    ProjectId = projectId,
                    OwnerTenantId = DefaultTenantId,
                    Slug = $"p-{suffix}",
                    DisplayName = $"p-{suffix}",
                    Visibility = ProjectVisibility.Private,
                    CreatedAt = now
                });
                seed.Definitions.Add(new DefinitionRow
                {
                    DefinitionId = definitionId,
                    TenantId = DefaultTenantId,
                    ProjectId = projectId,
                    Slug = DefinitionSlug.FromName(definitionId, "W"),
                    Name = "W",
                    LatestVersion = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                seed.DefinitionVersions.Add(new DefinitionVersionRow
                {
                    DefinitionVersionId = Guid.NewGuid(),
                    DefinitionId = definitionId,
                    Version = 1,
                    SourceYaml = NoopWorkflowYaml,
                    CompiledJson = compiled.CompiledJson,
                    CreatedAt = now
                });
                seed.Principals.Add(new PrincipalRow
                {
                    PrincipalId = creatorPrincipalId,
                    TenantId = DefaultTenantId,
                    PrincipalScope = PrincipalScope.Tenant,
                    PrincipalType = PrincipalType.User,
                    DisplayName = $"op-{suffix}",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                seed.Users.Add(new UserRow
                {
                    UserId = userId,
                    TenantId = DefaultTenantId,
                    Username = $"op-{suffix}",
                    PasswordHash = hasher.HashPassword("password"),
                    IsTenantAdmin = false,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                seed.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = creatorPrincipalId });
                seed.Principals.Add(new PrincipalRow
                {
                    PrincipalId = runAsPrincipalId,
                    TenantId = DefaultTenantId,
                    PrincipalScope = PrincipalScope.Tenant,
                    PrincipalType = PrincipalType.ServiceAccount,
                    DisplayName = $"sa-{suffix}",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                seed.ServiceAccounts.Add(new ServiceAccountRow
                {
                    ServiceAccountId = serviceAccountId,
                    TenantId = DefaultTenantId,
                    PrincipalId = runAsPrincipalId,
                    Name = $"sa-{suffix}",
                    CreatedAt = now
                });
                seed.Groups.Add(new GroupRow
                {
                    GroupId = groupId,
                    TenantId = DefaultTenantId,
                    Name = $"runners-{suffix}",
                    IsSystem = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                seed.GroupPermissions.Add(new GroupPermissionRow
                {
                    GroupId = groupId,
                    PermissionKey = WellKnownPermissionKeys.ExecutionsWrite
                });
                seed.ServiceAccountGroupMembers.Add(new ServiceAccountGroupMemberRow
                {
                    ServiceAccountId = serviceAccountId,
                    GroupId = groupId
                });
                await seed.SaveChangesAsync();
            }

            return new FireArrangement(scope, definitionId, creatorPrincipalId, runAsPrincipalId, $"{namePrefix}-{suffix}");
        }

        /// <summary>テストメソッド上で運用者文脈を張る。AsyncLocal は呼び出し元へ戻らない。</summary>
        public IDisposable BeginOperator() =>
            Accessor.SetContext(new TenantContextState(
                DefaultTenantId,
                "default",
                CreatorPrincipalId,
                TenantLifecycle.Active,
                new HashSet<string>(StringComparer.Ordinal)
                {
                    WellKnownPermissionKeys.ExecutionsWrite,
                    WellKnownPermissionKeys.ExecutionsRead
                }));

        public Task<ExecutionScheduleDetailDto> CreateScheduleAsync() =>
            Schedules.CreateAsync(
                new CreateExecutionScheduleRequest
                {
                    Name = ScheduleName,
                    DefinitionId = DefinitionId.ToString("D"),
                    CronExpression = "0 3 * * *",
                    TimeZone = "UTC",
                    OverlapPolicy = ExecutionScheduleOverlapPolicies.Skip,
                    RunAsPrincipalId = RunAsPrincipalId
                },
                CancellationToken.None);

        public async Task SetNextFireAtAsync(Guid scheduleId, DateTime nextFireAtUtc)
        {
            await using var db = await CreateDbAsync();
            var schedule = await db.ExecutionSchedules.IgnoreQueryFilters()
                .SingleAsync(row => row.ScheduleId == scheduleId);
            schedule.NextFireAt = nextFireAtUtc;
            await db.SaveChangesAsync();
        }

        public Task<CoreDbContext> CreateDbAsync() =>
            _scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>().CreateDbContextAsync();

        public async ValueTask DisposeAsync() => await _scope.DisposeAsync();
    }
}

/// <summary>発火シナリオ用の PostgreSQL。API ホストは起動しない。</summary>
public sealed class ExecutionScheduleFireScenarioFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>Docker 上のデータベースと DI が使えるか。</summary>
    public bool IsReady { get; private set; }

    /// <summary>起動できなかった理由。成功時は null。</summary>
    public string? SkipReason { get; private set; }

    /// <summary>scheduler プロセスの DI。未起動時は null。</summary>
    public ServiceProvider? Provider { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString()
            }).Build();
            services.AddStateviaSchedulerProcess(configuration);
            var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>()
                    .CreateDbContextAsync();
                await db.Database.MigrateAsync();
                var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
                await platform.EnsureDefaultTenantAsync(CancellationToken.None);
                await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
            }

            Provider = provider;
            IsReady = true;
        }
        catch (Exception ex)
        {
            SkipReason = $"Testcontainers の起動に失敗したため、スケジュール発火シナリオをスキップします: {ex.Message}";
            await Console.Error.WriteLineAsync($"WARNING: {SkipReason}");
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (Provider is not null)
            await Provider.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();
    }
}

/// <summary>発火シナリオが 1 つの PostgreSQL を共有する。</summary>
[CollectionDefinition(nameof(ExecutionScheduleFireScenarioCollection))]
public sealed class ExecutionScheduleFireScenarioCollection : ICollectionFixture<ExecutionScheduleFireScenarioFixture>;
