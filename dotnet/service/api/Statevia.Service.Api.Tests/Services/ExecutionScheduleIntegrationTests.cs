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

    /// <summary>定義の Project に所属しない Principal の一覧には、そのスケジュールが出ない。</summary>
    [Fact]
    public async Task List_WhenCallerNotMember_ReturnsEmpty()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using (context.BeginOperator())
            await context.CreateScheduleAsync("member-only");
        using var outsider = context.BeginOutsider();

        // Act
        var listed = await context.Schedules.ListAsync(CancellationToken.None);

        // Assert
        Assert.Empty(listed);
    }

    /// <summary>定義の Project に所属しない Principal の単票取得は、不在と同じ 404。</summary>
    [Fact]
    public async Task Get_WhenCallerNotMember_ThrowsNotFound()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        ExecutionScheduleDetailDto created;
        using (context.BeginOperator())
            created = await context.CreateScheduleAsync("member-only");
        using var outsider = context.BeginOutsider();

        // Act
        var act = () => context.Schedules.GetAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal("Schedule not found", exception.Message);
    }

    /// <summary>定義が論理削除されても、所属しない Principal はスケジュールを削除できない。</summary>
    [Fact]
    public async Task Delete_WhenDefinitionDeletedAndCallerNotMember_ThrowsNotFound()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        ExecutionScheduleDetailDto created;
        using (context.BeginOperator())
            created = await context.CreateScheduleAsync("orphan");
        await context.DeleteDefinitionAsync();
        using var outsider = context.BeginOutsider();

        // Act
        var act = () => context.Schedules.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.Null((await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync()).DeletedAt);
    }

    /// <summary>定義が論理削除されても、所属するメンバーはスケジュールを削除できる。</summary>
    [Fact]
    public async Task Delete_WhenDefinitionDeletedAndCallerMember_Deletes()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync("orphan");
        await context.DeleteDefinitionAsync();

        // Act
        await context.Schedules.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.NotNull((await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync()).DeletedAt);
    }

    /// <summary>
    /// 版を固定した定期実行で定義だけを変え、版を指定しない PATCH は 422 で、定義も版も保存しない。
    /// </summary>
    [Fact]
    public async Task Update_WhenPinnedAndDefinitionChangedWithoutVersion_ThrowsValidationWithoutSaving()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var targetDefinitionId = await context.AddDefinitionAsync();
        var created = await context.CreatePinnedScheduleAsync("pinned-move");

        // Act
        var act = () => context.Schedules.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { DefinitionId = targetDefinitionId.ToString("D") },
            CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Contains("definitionVersion", exception.Message, StringComparison.Ordinal);
        await using var verify = context.Database.Factory.CreateDbContext();
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(context.DefinitionId, schedule.DefinitionId);
        Assert.Equal(created.DefinitionVersionId, schedule.DefinitionVersionId);
    }

    /// <summary>版を固定した定期実行で定義と版を一緒に変えると、移し先の定義の版に固定し直す。</summary>
    [Fact]
    public async Task Update_WhenPinnedAndDefinitionChangedWithVersion_RepinsToTargetVersion()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var targetDefinitionId = await context.AddDefinitionAsync();
        var created = await context.CreatePinnedScheduleAsync("pinned-repin");

        // Act
        var updated = await context.Schedules.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest
            {
                DefinitionId = targetDefinitionId.ToString("D"),
                DefinitionVersion = 1
            },
            CancellationToken.None);

        // Assert
        await using var verify = context.Database.Factory.CreateDbContext();
        var targetVersion = await verify.DefinitionVersions
            .SingleAsync(row => row.DefinitionId == targetDefinitionId && row.Version == 1);
        Assert.Equal(targetDefinitionId, updated.DefinitionId);
        Assert.Equal(targetVersion.DefinitionVersionId, updated.DefinitionVersionId);
    }

    /// <summary>版を固定した定期実行で同じ定義を送る PATCH は、版を指定しなくても固定を保つ。</summary>
    [Fact]
    public async Task Update_WhenPinnedAndSameDefinitionWithoutVersion_KeepsPin()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var created = await context.CreatePinnedScheduleAsync("pinned-same");

        // Act
        var updated = await context.Schedules.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { DefinitionId = context.DefinitionId.ToString("D") },
            CancellationToken.None);

        // Assert
        Assert.NotNull(created.DefinitionVersionId);
        Assert.Equal(created.DefinitionVersionId, updated.DefinitionVersionId);
    }

    /// <summary>版を固定していない定期実行は、版を指定せずに定義を変えられ、発火時の latest のまま。</summary>
    [Fact]
    public async Task Update_WhenUnpinnedAndDefinitionChangedWithoutVersion_StaysUnpinned()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var targetDefinitionId = await context.AddDefinitionAsync();
        var created = await context.CreateScheduleAsync("unpinned-move");

        // Act
        var updated = await context.Schedules.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { DefinitionId = targetDefinitionId.ToString("D") },
            CancellationToken.None);

        // Assert
        Assert.Equal(targetDefinitionId, updated.DefinitionId);
        Assert.Null(updated.DefinitionVersionId);
    }

    /// <summary>グループの権限を実際に展開し、<c>executions.write</c> の無い run-as の作成を 422 で止める。</summary>
    [Fact]
    public async Task Create_WhenRunAsLacksExecutionsWrite_ThrowsValidationWithoutSaving()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var readOnlyRunAs = await context.AddReadOnlyRunAsAsync();

        // Act
        var act = () => context.CreateScheduleAsync("read-only-run-as", readOnlyRunAs);

        // Assert
        var exception = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Contains("executions.write", exception.Message, StringComparison.Ordinal);
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.Equal(0, await verify.ExecutionSchedules.IgnoreQueryFilters().CountAsync());
    }

    /// <summary><c>executions.write</c> の無い run-as の手動実行は、実行も <c>schedule_runs</c> の行も作らない。</summary>
    [Fact]
    public async Task RunNow_WhenRunAsLacksExecutionsWrite_LeavesNoRunOrExecution()
    {
        // Arrange
        await using var context = await ScheduleIntegrationContext.OpenAsync();
        using var tenant = context.BeginOperator();
        var created = await context.CreateScheduleAsync("lost-permission");
        await context.SetRunAsAsync(created.ScheduleId, await context.AddReadOnlyRunAsAsync());

        // Act
        var act = () => context.Schedules.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ApiValidationException>(act);
        await using var verify = context.Database.Factory.CreateDbContext();
        Assert.Equal(0, await verify.ExecutionScheduleRuns.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await verify.Executions.IgnoreQueryFilters().CountAsync());
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
            Guid projectId,
            Guid creatorPrincipalId,
            Guid runAsPrincipalId)
        {
            Database = database;
            Provider = provider;
            Scope = scope;
            DefinitionId = definitionId;
            ProjectId = projectId;
            CreatorPrincipalId = creatorPrincipalId;
            RunAsPrincipalId = runAsPrincipalId;
            Accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        }

        public SqliteTestDatabase Database { get; }

        public ServiceProvider Provider { get; }

        public AsyncServiceScope Scope { get; }

        public ITenantContextAccessor Accessor { get; }

        public Guid DefinitionId { get; }

        /// <summary>定義と作成者・run-as の所属がある Project。</summary>
        public Guid ProjectId { get; }

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
            var projectId = Guid.NewGuid();
            await using (var seed = database.Factory.CreateDbContext())
            {
                ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
                DefinitionTestData.AddDefinitionWithVersion(
                    seed,
                    TestTenantIds.DefaultTenantId,
                    definitionId,
                    "W",
                    projectId,
                    sourceYaml: NoopWorkflowYaml,
                    compiledJson: compiled.CompiledJson);
                await seed.SaveChangesAsync();
            }

            var creatorPrincipalId = await SecurityTestSeed.SeedUserAsync(database, "operator", "password");
            var runAs = await SecurityTestSeed.SeedApiKeyAsync(
                database,
                plainKey: $"statevia-test-key-{Guid.NewGuid():N}",
                permissionKeys: [WellKnownPermissionKeys.ExecutionsWrite]);

            // 作成者と実行主体の両方が定義の Project に executor で所属していないと、作成も発火も Project not found になる。
            await using (var membership = database.Factory.CreateDbContext())
            {
                ProjectTestData.GrantAccess(membership, projectId, creatorPrincipalId, ProjectAccessRole.Executor);
                ProjectTestData.GrantAccess(membership, projectId, runAs.PrincipalId, ProjectAccessRole.Executor);
                await membership.SaveChangesAsync();
            }

            return new ScheduleIntegrationContext(
                database,
                provider,
                scope,
                definitionId,
                projectId,
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

        /// <summary>
        /// 定義の Project に所属しない Principal の文脈を張る。権限キーは運用者と同じで、所属だけが無い。
        /// </summary>
        public IDisposable BeginOutsider() =>
            Accessor.SetContext(TestTenantIds.DefaultContext with
            {
                PrincipalId = Guid.NewGuid(),
                EffectivePermissionKeys = new HashSet<string>(StringComparer.Ordinal)
                {
                    WellKnownPermissionKeys.ExecutionsWrite,
                    WellKnownPermissionKeys.ExecutionsRead
                }
            });

        /// <summary>スケジュールの定義を論理削除する。</summary>
        public async Task DeleteDefinitionAsync()
        {
            await using var db = Database.Factory.CreateDbContext();
            var definition = await db.Definitions.IgnoreQueryFilters()
                .SingleAsync(row => row.DefinitionId == DefinitionId);
            definition.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        /// <summary>同じ Project に版 1 だけを持つ定義を足し、定義 ID を返す。</summary>
        public async Task<Guid> AddDefinitionAsync()
        {
            var compiler = Scope.ServiceProvider.GetRequiredService<IDefinitionCompilerService>();
            var compiled = compiler.ValidateAndCompile("W", NoopWorkflowYaml);
            var definitionId = Guid.NewGuid();
            await using var db = Database.Factory.CreateDbContext();
            DefinitionTestData.AddDefinitionWithVersion(
                db,
                TestTenantIds.DefaultTenantId,
                definitionId,
                "W",
                ProjectId,
                sourceYaml: NoopWorkflowYaml,
                compiledJson: compiled.CompiledJson);
            await db.SaveChangesAsync();
            return definitionId;
        }

        /// <summary>定義の版 1 を固定したスケジュールを作る。</summary>
        public async Task<ExecutionScheduleDetailDto> CreatePinnedScheduleAsync(string name)
        {
            return await Schedules.CreateAsync(
                new CreateExecutionScheduleRequest
                {
                    Name = name,
                    DefinitionId = DefinitionId.ToString("D"),
                    DefinitionVersion = 1,
                    CronExpression = "0 3 * * *",
                    TimeZone = "UTC",
                    OverlapPolicy = ExecutionScheduleOverlapPolicies.Skip,
                    RunAsPrincipalId = RunAsPrincipalId
                },
                CancellationToken.None);
        }

        public async Task<ExecutionScheduleDetailDto> CreateScheduleAsync(string name, Guid? runAsPrincipalId = null)
        {
            return await Schedules.CreateAsync(
                new CreateExecutionScheduleRequest
                {
                    Name = name,
                    DefinitionId = DefinitionId.ToString("D"),
                    CronExpression = "0 3 * * *",
                    TimeZone = "UTC",
                    OverlapPolicy = ExecutionScheduleOverlapPolicies.Skip,
                    RunAsPrincipalId = runAsPrincipalId ?? RunAsPrincipalId
                },
                CancellationToken.None);
        }

        /// <summary>定義の Project に executor で所属するが、<c>executions.write</c> を持たない run-as を作る。</summary>
        /// <returns>run-as の Principal ID。</returns>
        public async Task<Guid> AddReadOnlyRunAsAsync()
        {
            var runAs = await SecurityTestSeed.SeedApiKeyAsync(
                Database,
                plainKey: $"statevia-test-key-{Guid.NewGuid():N}",
                permissionKeys: [WellKnownPermissionKeys.ExecutionsRead]);
            await using var db = Database.Factory.CreateDbContext();
            ProjectTestData.GrantAccess(db, ProjectId, runAs.PrincipalId, ProjectAccessRole.Executor);
            await db.SaveChangesAsync();
            return runAs.PrincipalId;
        }

        /// <summary>保存後に run-as が変わった定期実行を、検査を通さずに作る。</summary>
        public async Task SetRunAsAsync(Guid scheduleId, Guid runAsPrincipalId)
        {
            await using var db = Database.Factory.CreateDbContext();
            var schedule = await db.ExecutionSchedules.IgnoreQueryFilters()
                .SingleAsync(row => row.ScheduleId == scheduleId);
            schedule.RunAsPrincipalId = runAsPrincipalId;
            await db.SaveChangesAsync();
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
