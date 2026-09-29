using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Application.Contracts.Scheduling;
using Statevia.Core.Application.Scheduling;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Common;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionScheduleDispatchService"/> の欠発・overlap・SA キルスイッチ。</summary>
public sealed class ExecutionScheduleDispatchServiceTests
{
    private static readonly DateTime SlotUtc = new(2026, 9, 19, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueNowUtc = new(2026, 9, 19, 3, 0, 30, DateTimeKind.Utc);
    private static readonly DateTime MissNowUtc = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>due 枠は SA 文脈で Start し、Owner 用 Principal と SCHEDULER 文脈を渡す。</summary>
    [Fact]
    public async Task DispatchDueAsync_StartsOnce_WithServiceAccountContext()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var scheduleId = await SeedDueScheduleAsync(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(1, executions.StartCount);
        Assert.Equal(runAs, executions.LastPrincipalId);
        Assert.Equal("SCHEDULER", executions.LastContext?.Method);
        Assert.Equal($"/v1/schedules/{scheduleId:D}/fires", executions.LastContext?.Path);
        Assert.Equal(scheduleId, executions.LastContext?.ScheduleId);
        Assert.Equal($"{scheduleId:N}:{SlotUtc:o}", executions.LastIdempotencyKey);
        await using var verify = db.Factory.CreateDbContext();
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(new DateTime(2026, 9, 20, 3, 0, 0, DateTimeKind.Utc), schedule.NextFireAt);
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(ExecutionScheduleRunOutcomes.Started, run.Outcome);
        Assert.Equal(executions.StartedExecutionId, run.ExecutionId);
    }

    /// <summary>2 回目の Dispatcher は同じ枠を Start しない。</summary>
    [Fact]
    public async Task DispatchDueAsync_SecondCall_DoesNotStartTwice()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        await SeedDueScheduleAsync(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);
        var second = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(0, second);
        Assert.Equal(1, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Equal(1, await verify.ExecutionScheduleRuns.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>無効 SA では Start せず run を failed にする。</summary>
    [Fact]
    public async Task DispatchDueAsync_WhenSaInactive_DoesNotStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        await SeedDueScheduleAsync(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions, principalActive: false);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(ExecutionScheduleRunOutcomes.Failed, run.Outcome);
        Assert.Null(run.ExecutionId);
    }

    /// <summary>許可を狭めたあとの発火は Start せず、スケジュールは無効化しない。</summary>
    [Fact]
    public async Task DispatchDueAsync_WhenResourceGrantDenied_DoesNotStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        await SeedDueScheduleAsync(db, runAs, definitionId: definitionId);
        var authorization = CreateDenyingAuthorization(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor)
        {
            OnStart = cancellationToken => authorization.EnsureAsync(Guid.NewGuid(), definitionId, cancellationToken)
        };
        var sut = CreateSut(db, runAs, executions);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(ExecutionScheduleRunOutcomes.Failed, run.Outcome);
        Assert.Equal("RESOURCE_GRANT_DENIED", run.ErrorCode);
        Assert.Null(run.ExecutionId);
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.True(schedule.Enabled);
    }

    /// <summary>許可外の手動実行は failed run を残して HTTP と同じ拒否を返す。</summary>
    [Fact]
    public async Task RunManuallyAsync_WhenResourceGrantDenied_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var scheduleId = await SeedDueScheduleAsync(db, runAs, definitionId: definitionId);
        var authorization = CreateDenyingAuthorization(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor)
        {
            OnStart = cancellationToken => authorization.EnsureAsync(Guid.NewGuid(), definitionId, cancellationToken)
        };
        var sut = CreateSut(db, runAs, executions);

        // Act
        var act = () => sut.RunManuallyAsync(scheduleId, "once", CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(ExecutionScheduleRunOutcomes.Failed, run.Outcome);
        Assert.Equal("RESOURCE_GRANT_DENIED", run.ErrorCode);
        Assert.True(run.Manual);
    }

    private static PrincipalResourceGrantAuthorization CreateDenyingAuthorization(SqliteTestDatabase db, Guid runAs)
    {
        var principals = new StubPrincipalDataAccess();
        principals.Add(runAs, TestTenantIds.DefaultTenantId, PrincipalType.ServiceAccount, isActive: true);
        return new PrincipalResourceGrantAuthorization(
            new SingleDefinitionGrantStore(runAs, Guid.NewGuid()),
            db.TenantAccessor,
            principals);
    }

    /// <summary>欠発は Start せず next だけ未来枠へ進める。</summary>
    [Fact]
    public async Task DispatchDueAsync_WhenSlotMissed_AdvancesWithoutStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        await SeedDueScheduleAsync(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var processed = await sut.DispatchDueAsync(MissNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Equal(0, await verify.ExecutionScheduleRuns.IgnoreQueryFilters().CountAsync());
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(new DateTime(2026, 9, 21, 3, 0, 0, DateTimeKind.Utc), schedule.NextFireAt);
    }

    /// <summary>overlap skip かつ非終端があれば Start せず skipped_overlap。</summary>
    [Fact]
    public async Task DispatchDueAsync_WhenOverlapSkipAndRunning_Skips()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var scheduleId = await SeedDueScheduleAsync(db, runAs);
        await SeedRunningExecutionAsync(db, scheduleId);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        var latest = await verify.ExecutionScheduleRuns.IgnoreQueryFilters()
            .OrderByDescending(run => run.CreatedAt)
            .FirstAsync();
        Assert.Equal(ExecutionScheduleRunOutcomes.SkippedOverlap, latest.Outcome);
    }

    /// <summary>overlap allow なら非終端があっても Start する。</summary>
    [Fact]
    public async Task DispatchDueAsync_WhenOverlapAllow_StartsDespiteRunning()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var scheduleId = await SeedDueScheduleAsync(db, runAs, overlap: ExecutionScheduleOverlapPolicies.Allow);
        await SeedRunningExecutionAsync(db, scheduleId);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(1, executions.StartCount);
    }

    /// <summary>テナント Suspended では Start しない。</summary>
    [Fact]
    public async Task DispatchDueAsync_WhenTenantSuspended_DoesNotStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        await SeedDueScheduleAsync(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions, tenantLifecycle: TenantLifecycle.Suspended);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
    }

    /// <summary>手動実行は SA 文脈で Start し、manual run を残して next は変えない。</summary>
    [Fact]
    public async Task RunManuallyAsync_StartsOnce_WithoutAdvancingNextFire()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var scheduleId = await SeedDueScheduleAsync(db, runAs);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var started = await sut.RunManuallyAsync(scheduleId, "once", CancellationToken.None);

        // Assert
        Assert.Equal(executions.StartedExecutionId, started.ResourceId);
        Assert.Equal(1, executions.StartCount);
        Assert.Equal(runAs, executions.LastPrincipalId);
        Assert.Equal($"{scheduleId:N}:manual:once", executions.LastIdempotencyKey);
        Assert.Equal("SCHEDULER", executions.LastContext?.Method);
        Assert.Equal($"/v1/schedules/{scheduleId:D}/run", executions.LastContext?.Path);
        Assert.Equal(scheduleId, executions.LastContext?.ScheduleId);
        await using var verify = db.Factory.CreateDbContext();
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(SlotUtc, schedule.NextFireAt);
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.True(run.Manual);
        Assert.Null(run.ScheduledFireAt);
        Assert.Equal(ExecutionScheduleRunOutcomes.Started, run.Outcome);
        Assert.Equal(executions.StartedExecutionId, run.ExecutionId);
    }

    /// <summary>無効スケジュールの手動実行は 422 で run を作らない。</summary>
    [Fact]
    public async Task RunManuallyAsync_WhenDisabled_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var scheduleId = await SeedDueScheduleAsync(db, runAs, enabled: false);
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var act = () => sut.RunManuallyAsync(scheduleId, "once", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Equal(0, await verify.ExecutionScheduleRuns.IgnoreQueryFilters().CountAsync());
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(SlotUtc, schedule.NextFireAt);
    }

    /// <summary>削除済み ID の手動実行は 404。</summary>
    [Fact]
    public async Task RunManuallyAsync_WhenMissing_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var runAs = Guid.NewGuid();
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, runAs, executions);

        // Act
        var act = () => sut.RunManuallyAsync(Guid.NewGuid(), "once", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(0, executions.StartCount);
    }

    /// <summary>システム行は Start せず、待機なしの古い Running とテナント失敗枠だけを数える。</summary>
    [Fact]
    public async Task DispatchDueAsync_SystemReport_DoesNotStartAndWritesTwoCounts()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(db, Guid.NewGuid(), executions);
        await SeedSystemReportFixtureAsync(db);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Equal(3, await verify.Executions.IgnoreQueryFilters().CountAsync());
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters()
            .SingleAsync(row => row.Outcome == ExecutionScheduleRunOutcomes.Completed);
        Assert.Equal("""{"stuckExecutionCount":1,"failedScheduleRunCount":1}""", run.SummaryJson);
        Assert.Null(run.ExecutionId);
        Assert.False(run.Manual);
    }

    /// <summary>実効 Action タイムアウトが範囲外なら補正せず REPORT_FAILED。</summary>
    [Fact]
    public async Task DispatchDueAsync_SystemReport_WhenTimeoutOutOfRange_FailsWithoutStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var executions = new FakeExecutionService(db.TenantAccessor);
        var sut = CreateSut(
            db,
            Guid.NewGuid(),
            executions,
            actionTimeout: new EffectiveActionTimeoutSettings(null, 5));
        await SeedSystemScheduleAsync(db);

        // Act
        var processed = await sut.DispatchDueAsync(DueNowUtc, limit: 64, CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(0, executions.StartCount);
        await using var verify = db.Factory.CreateDbContext();
        var run = await verify.ExecutionScheduleRuns.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(SystemScheduleRows.ReportFailedErrorCode, run.ErrorCode);
        var schedule = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.True(schedule.Enabled);
    }

    private static async Task<Guid> SeedDueScheduleAsync(
        SqliteTestDatabase db,
        Guid runAs,
        string overlap = ExecutionScheduleOverlapPolicies.Skip,
        bool enabled = true,
        Guid? definitionId = null)
    {
        var scheduleId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var repository = new ExecutionScheduleRepository(db.Factory);
        await repository.AddAsync(
            new ExecutionScheduleRow
            {
                ScheduleId = scheduleId,
                TenantId = TestTenantIds.DefaultTenantId,
                DefinitionId = definitionId ?? Guid.NewGuid(),
                RunAsPrincipalId = runAs,
                CreatedByPrincipalId = Guid.NewGuid(),
                Name = "dispatch-job",
                CronExpression = "0 3 * * *",
                TimeZone = "UTC",
                OverlapPolicy = overlap,
                Enabled = enabled,
                NextFireAt = SlotUtc,
                CreatedAt = now,
                UpdatedAt = now
            },
            CancellationToken.None);
        return scheduleId;
    }

    private static async Task SeedRunningExecutionAsync(SqliteTestDatabase db, Guid scheduleId)
    {
        var executionId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var ctx = db.Factory.CreateDbContext();
        var project = ProjectTestData.AddDefaultProject(ctx, TestTenantIds.DefaultTenantId, "default");
        var seeded = DefinitionTestData.AddDefinitionWithVersion(
            ctx,
            TestTenantIds.DefaultTenantId,
            definitionId,
            "dispatch-overlap",
            project.ProjectId);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.DefaultTenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = seeded.Version.DefinitionVersionId,
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now
        });
        ctx.ExecutionScheduleRuns.Add(new ExecutionScheduleRunRow
        {
            ScheduleRunId = Guid.NewGuid(),
            ScheduleId = scheduleId,
            TenantId = TestTenantIds.DefaultTenantId,
            ScheduledFireAt = SlotUtc.AddDays(-1),
            Manual = false,
            Outcome = ExecutionScheduleRunOutcomes.Started,
            ExecutionId = executionId,
            CreatedAt = now
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedSystemScheduleAsync(SqliteTestDatabase db)
    {
        await using var ctx = db.Factory.CreateDbContext();
        ctx.ExecutionSchedules.Add(new ExecutionScheduleRow
        {
            ScheduleId = Guid.NewGuid(),
            TenantId = TestTenantIds.DefaultTenantId,
            JobKey = SystemScheduleRows.StuckExecutionReportJobKey,
            Name = SystemScheduleRows.StuckExecutionReportJobKey,
            CronExpression = "0 * * * *",
            TimeZone = "UTC",
            OverlapPolicy = "skip",
            Enabled = true,
            NextFireAt = SlotUtc,
            CreatedAt = SlotUtc,
            UpdatedAt = SlotUtc
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedSystemReportFixtureAsync(SqliteTestDatabase db)
    {
        await using var ctx = db.Factory.CreateDbContext();
        var project = ProjectTestData.AddDefaultProject(ctx, TestTenantIds.DefaultTenantId, "default");
        var seeded = DefinitionTestData.AddDefinitionWithVersion(
            ctx,
            TestTenantIds.DefaultTenantId,
            Guid.NewGuid(),
            "report-fixture",
            project.ProjectId);
        var systemScheduleId = Guid.NewGuid();
        var tenantScheduleId = Guid.NewGuid();
        ctx.ExecutionSchedules.Add(new ExecutionScheduleRow
        {
            ScheduleId = systemScheduleId,
            TenantId = TestTenantIds.DefaultTenantId,
            JobKey = SystemScheduleRows.StuckExecutionReportJobKey,
            Name = SystemScheduleRows.StuckExecutionReportJobKey,
            CronExpression = "0 * * * *",
            TimeZone = "UTC",
            OverlapPolicy = "skip",
            Enabled = true,
            NextFireAt = SlotUtc,
            CreatedAt = SlotUtc,
            UpdatedAt = SlotUtc
        });
        ctx.ExecutionSchedules.Add(new ExecutionScheduleRow
        {
            ScheduleId = tenantScheduleId,
            TenantId = TestTenantIds.DefaultTenantId,
            DefinitionId = seeded.Definition.DefinitionId,
            RunAsPrincipalId = Guid.NewGuid(),
            CreatedByPrincipalId = Guid.NewGuid(),
            Name = "tenant-job",
            CronExpression = "0 3 * * *",
            TimeZone = "UTC",
            OverlapPolicy = "skip",
            Enabled = true,
            NextFireAt = SlotUtc.AddDays(1),
            CreatedAt = SlotUtc,
            UpdatedAt = SlotUtc
        });
        var stuckId = Guid.NewGuid();
        var waitingId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        ctx.Executions.AddRange(
            CreateRunning(stuckId, seeded.Version.DefinitionVersionId, DueNowUtc.AddMinutes(-2)),
            CreateRunning(waitingId, seeded.Version.DefinitionVersionId, DueNowUtc.AddMinutes(-2)),
            CreateRunning(freshId, seeded.Version.DefinitionVersionId, DueNowUtc));
        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = waitingId,
            NodeId = "wait",
            WaitKind = ExecutionWaitKind.EventWait,
            AllowedEvents = ["go"],
            CreatedAt = DueNowUtc
        });
        ctx.ExecutionScheduleRuns.Add(new ExecutionScheduleRunRow
        {
            ScheduleRunId = Guid.NewGuid(),
            ScheduleId = tenantScheduleId,
            TenantId = TestTenantIds.DefaultTenantId,
            ScheduledFireAt = SlotUtc.AddHours(-2),
            Outcome = ExecutionScheduleRunOutcomes.Failed,
            CreatedAt = DueNowUtc.AddMinutes(-30)
        });
        ctx.ExecutionScheduleRuns.Add(new ExecutionScheduleRunRow
        {
            ScheduleRunId = Guid.NewGuid(),
            ScheduleId = systemScheduleId,
            TenantId = TestTenantIds.DefaultTenantId,
            ScheduledFireAt = SlotUtc.AddHours(-2),
            Outcome = ExecutionScheduleRunOutcomes.Failed,
            ErrorCode = SystemScheduleRows.ReportFailedErrorCode,
            CreatedAt = DueNowUtc.AddMinutes(-20)
        });
        await ctx.SaveChangesAsync();
    }

    private static ExecutionRow CreateRunning(Guid executionId, Guid definitionVersionId, DateTime updatedAt) =>
        new()
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.DefaultTenantId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = definitionVersionId,
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = updatedAt,
            UpdatedAt = updatedAt
        };

    private static ExecutionScheduleDispatchService CreateSut(
        SqliteTestDatabase db,
        Guid runAs,
        FakeExecutionService executions,
        bool principalActive = true,
        TenantLifecycle tenantLifecycle = TenantLifecycle.Active,
        EffectiveActionTimeoutSettings? actionTimeout = null)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var principals = new StubPrincipalDataAccess();
        principals.Add(runAs, TestTenantIds.DefaultTenantId, PrincipalType.ServiceAccount, principalActive);
        principals.TenantLifecycle = tenantLifecycle;
        return new ExecutionScheduleDispatchService(
            new ExecutionScheduleRepository(db.Factory),
            executions,
            principals,
            db.TenantAccessor,
            new TestCoreTransactionExecutor(uowFactory),
            new DefaultIdGenerator(),
            new ExecutionScheduleFireSupport(
                NullLogger<ExecutionScheduleFireSupport>.Instance,
                actionTimeout ?? EffectiveActionTimeoutSettings.PlatformDefault));
    }

    private sealed class SingleDefinitionGrantStore(Guid principalId, Guid definitionId) : IPrincipalResourceGrantStore
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            IReadOnlyList<PrincipalResourceGrantRow> rows = id == principalId
                ?
                [
                    new PrincipalResourceGrantRow
                    {
                        PrincipalId = principalId,
                        ResourceKind = PrincipalResourceGrantKinds.Definition,
                        ResourceId = definitionId,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
                : [];
            return Task.FromResult(rows);
        }

        /// <inheritdoc />
        public Task ReplaceAsync(
            Guid id,
            IReadOnlyCollection<Guid> projectIds,
            IReadOnlyCollection<Guid> definitionIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubPrincipalDataAccess : IPrincipalDataAccess
    {
        private readonly Dictionary<Guid, PrincipalInfo> _principals = [];

        public TenantLifecycle TenantLifecycle { get; set; } = TenantLifecycle.Active;

        public void Add(Guid principalId, Guid tenantId, PrincipalType type, bool isActive) =>
            _principals[principalId] = new PrincipalInfo(principalId, tenantId, type, isActive, null, null);

        public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(_principals.TryGetValue(principalId, out var info) ? info : null);

        public Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantInfo?>(new TenantInfo(tenantId, "default", TenantLifecycle));

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([WellKnownPermissionKeys.ExecutionsWrite]);

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GroupSnapshot>>([]);
    }

    private sealed class FakeExecutionService(ITenantContextAccessor accessor) : IExecutionService
    {
        public int StartCount { get; private set; }
        public Guid StartedExecutionId { get; } = Guid.NewGuid();
        public Guid? LastPrincipalId { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public CommandRequestContext? LastContext { get; private set; }

        public Func<CancellationToken, Task>? OnStart { get; set; }

        public async Task<ExecutionResponse> StartAsync(
            StartExecutionRequest request,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct)
        {
            _ = request;
            if (OnStart is not null)
                await OnStart(ct).ConfigureAwait(false);
            StartCount++;
            LastPrincipalId = accessor.PrincipalId;
            LastIdempotencyKey = idempotencyKey;
            LastContext = requestContext;
            return new ExecutionResponse
            {
                ResourceId = StartedExecutionId,
                DisplayId = StartedExecutionId.ToString("D"),
                Status = ExecutionProjectionStatuses.Running
            };
        }

        public Task<ExecutionResponse> ExecuteQueuedStartAsync(
            Guid executionId,
            StartExecutionRequest request,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(
            ExecutionListPageQuery query,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(
            string idOrUuid,
            long afterSeq,
            int limit,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(
            string idOrUuid,
            string nodeId,
            string? resumeKey,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task RecoverExecutionAsync(
            Guid executionId,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(
            string idOrUuid,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(
            string idOrUuid,
            string eventName,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PersistCheckpointAndUnloadByEngineIdAsync(
            string engineExecutionId,
            string nodeId,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<long?> BeginOwnedSessionAsync(
            Guid executionId,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<bool> RenewOwnedSessionLeaseAsync(
            Guid executionId,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task AbandonLocalOwnedSessionAsync(Guid executionId) =>
            throw new NotSupportedException();

        public Task AwaitLocalExecutionLoadAsync(
            Guid executionId,
            TimeSpan noProgressTimeout,
            CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
