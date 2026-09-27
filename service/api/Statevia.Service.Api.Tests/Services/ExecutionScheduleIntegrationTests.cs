using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>
/// スケジュール作成・欠発・無効 SA・テナント境界の実サービス結合（sqlite）。
/// </summary>
/// <remarks>
/// 発火と手動実行は Start を外側トランザクションの中で呼ぶ。sqlite は同一接続の入れ子トランザクションができないため、
/// その経路は PostgreSQL のシナリオテストで確認する。
/// </remarks>
public sealed class ExecutionScheduleIntegrationTests
{
    private static readonly DateTime SlotUtc = new(2026, 9, 19, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueNowUtc = new(2026, 9, 19, 3, 0, 30, DateTimeKind.Utc);
    private static readonly DateTime NextSlotUtc = new(2026, 9, 20, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MissNowUtc = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MissedNextUtc = new(2026, 9, 21, 3, 0, 0, DateTimeKind.Utc);

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

    /// <summary>有効な ServiceAccount を run-as にしてスケジュールを作ると next が計算される。</summary>
    [Fact]
    public async Task Create_PersistsNextFireForActiveServiceAccount()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();

        // Act
        var created = await context.CreateScheduleAsync("nightly");

        // Assert
        Assert.Equal(context.DefinitionId, created.DefinitionId);
        Assert.Equal(context.RunAsPrincipalId, created.RunAsPrincipalId);
        Assert.Equal(context.CreatorPrincipalId, created.CreatedByPrincipalId);
        Assert.True(created.Enabled);
        Assert.Null(created.DefinitionVersionId);
        Assert.True(created.NextFireAt > DateTime.UtcNow.AddMinutes(-1));
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.Equal(0, await verify.ExecutionScheduleRuns.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verify.Executions.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>次枠も既に過ぎている保存枠は Start せず、now の次枠へ進める。</summary>
    [Fact]
    public async Task DispatchDue_WhenSlotMissed_AdvancesWithoutStart()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync("missed-slot");
        await context.SetNextFireAtAsync(created.ScheduleId, SlotUtc);

        // Act
        var processed = await context.Dispatch.DispatchDueAsync(MissNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.Equal(0, await verify.ExecutionScheduleRuns.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verify.Executions.IgnoreQueryFilters().CountAsync());
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(MissedNextUtc, AsUtc(schedule.NextFireAt));
        Assert.True(schedule.Enabled);
    }

    /// <summary>run-as SA が無効なら Start せず、スケジュールは enabled のまま次枠へ進む。</summary>
    [Fact]
    public async Task DispatchDue_WhenServiceAccountInactive_DoesNotStart()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync("inactive-sa");
        await using (var db = context.Database.Factory.CreateDbContext())
        {
            var principal = await db.Principals.SingleAsync(row => row.PrincipalId == context.RunAsPrincipalId);
            principal.IsActive = false;
            await db.SaveChangesAsync();
        }

        await context.SetNextFireAtAsync(created.ScheduleId, SlotUtc);

        // Act
        var processed = await context.Dispatch.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.Equal(0, await verify.Executions.IgnoreQueryFilters().CountAsync());
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(ExecutionScheduleRunOutcomes.Failed, run.Outcome);
        Assert.Null(run.ExecutionId);
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.True(schedule.Enabled);
        Assert.Equal(NextSlotUtc, AsUtc(schedule.NextFireAt));
    }

    /// <summary>他テナントはスケジュール ID を取得できない。</summary>
    [Fact]
    public async Task Get_WhenOtherTenant_ThrowsNotFound()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync("tenant-boundary");
        tenant.Dispose();
        using var otherTenant = context.Accessor.SetContext(TestTenantIds.T1Context with
        {
            PrincipalId = Guid.NewGuid(),
            EffectivePermissionKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                WellKnownPermissionKeys.ExecutionsRead
            }
        });

        // Act
        var act = () => context.Schedules.GetAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal("Schedule not found", exception.Message);
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>sqlite 上の scheduler プロセス DI と、作成者・run-as SA・定義。</summary>
    private sealed class ScheduleIntegrationContext : IAsyncDisposable
    {
        private ScheduleIntegrationContext(
            SqliteTestDatabase database,
            ServiceProvider provider,
            AsyncServiceScope scope,
            Guid definitionId,
            Guid creatorPrincipalId,
            Guid runAsPrincipalId)
        {
            Database = database;
            Provider = provider;
            Scope = scope;
            DefinitionId = definitionId;
            CreatorPrincipalId = creatorPrincipalId;
            RunAsPrincipalId = runAsPrincipalId;
            Accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        }

        public SqliteTestDatabase Database { get; }

        public ServiceProvider Provider { get; }

        public AsyncServiceScope Scope { get; }

        public ITenantContextAccessor Accessor { get; }

        public Guid DefinitionId { get; }

        public Guid CreatorPrincipalId { get; }

        public Guid RunAsPrincipalId { get; }

        public IExecutionScheduleService Schedules =>
            Scope.ServiceProvider.GetRequiredService<IExecutionScheduleService>();

        public IExecutionScheduleDispatchService Dispatch =>
            Scope.ServiceProvider.GetRequiredService<IExecutionScheduleDispatchService>();

        public static async Task<ScheduleIntegrationContext> OpenAsync()
        {
            var database = new SqliteTestDatabase();
            var services = new ServiceCollection();
            services.AddLogging();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=statevia;Username=x;Password=y"
            }).Build();
            services.AddStateviaSchedulerProcess(configuration);
            services.AddSingleton(database.Factory);

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var compiler = scope.ServiceProvider.GetRequiredService<IDefinitionCompilerService>();
            var compiled = compiler.ValidateAndCompile("W", NoopWorkflowYaml);
            var definitionId = Guid.NewGuid();
            await using (var seed = database.Factory.CreateDbContext())
            {
                var project = ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default");
                DefinitionTestData.AddDefinitionWithVersion(
                    seed,
                    TestTenantIds.DefaultTenantId,
                    definitionId,
                    "W",
                    project.ProjectId,
                    sourceYaml: NoopWorkflowYaml,
                    compiledJson: compiled.CompiledJson);
                await seed.SaveChangesAsync();
            }

            var creatorPrincipalId = await SecurityTestSeed.SeedUserAsync(database, "operator", "password");
            var runAs = await SecurityTestSeed.SeedApiKeyAsync(
                database,
                plainKey: $"statevia-test-key-{Guid.NewGuid():N}",
                permissionKeys: [WellKnownPermissionKeys.ExecutionsWrite]);

            return new ScheduleIntegrationContext(
                database,
                provider,
                scope,
                definitionId,
                creatorPrincipalId,
                runAs.PrincipalId);
        }

        /// <summary>
        /// テストメソッド上で運用者文脈を張る。
        /// AsyncLocal は呼び出し元へ戻らないため、別メソッドの中では設定しない。
        /// </summary>
        public IDisposable BeginOperator() =>
            Accessor.SetContext(TestTenantIds.DefaultContext with
            {
                PrincipalId = CreatorPrincipalId,
                EffectivePermissionKeys = new HashSet<string>(StringComparer.Ordinal)
                {
                    WellKnownPermissionKeys.ExecutionsWrite,
                    WellKnownPermissionKeys.ExecutionsRead
                }
            });

        public async Task<ExecutionScheduleDetailDto> CreateScheduleAsync(string name)
        {
            return await Schedules.CreateAsync(
                new CreateExecutionScheduleRequest
                {
                    Name = name,
                    DefinitionId = DefinitionId.ToString("D"),
                    CronExpression = "0 3 * * *",
                    TimeZone = "UTC",
                    OverlapPolicy = ExecutionScheduleOverlapPolicies.Skip,
                    RunAsPrincipalId = RunAsPrincipalId
                },
                CancellationToken.None);
        }

        public async Task SetNextFireAtAsync(Guid scheduleId, DateTime nextFireAtUtc)
        {
            await using var db = Database.Factory.CreateDbContext();
            var schedule = await db.ExecutionSchedules.IgnoreQueryFilters()
                .SingleAsync(row => row.ScheduleId == scheduleId);
            schedule.NextFireAt = nextFireAtUtc;
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await Provider.DisposeAsync();
            Database.Dispose();
        }
    }
}
