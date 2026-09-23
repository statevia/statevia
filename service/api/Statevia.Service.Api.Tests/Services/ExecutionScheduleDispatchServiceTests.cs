using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static async Task<Guid> SeedDueScheduleAsync(
        SqliteTestDatabase db,
        Guid runAs,
        string overlap = ExecutionScheduleOverlapPolicies.Skip)
    {
        var scheduleId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var repository = new ExecutionScheduleRepository(db.Factory);
        await repository.AddAsync(
            new ExecutionScheduleRow
            {
                ScheduleId = scheduleId,
                TenantId = TestTenantIds.DefaultTenantId,
                DefinitionId = Guid.NewGuid(),
                RunAsPrincipalId = runAs,
                CreatedByPrincipalId = Guid.NewGuid(),
                Name = "dispatch-job",
                CronExpression = "0 3 * * *",
                TimeZone = "UTC",
                OverlapPolicy = overlap,
                Enabled = true,
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

    private static ExecutionScheduleDispatchService CreateSut(
        SqliteTestDatabase db,
        Guid runAs,
        FakeExecutionService executions,
        bool principalActive = true,
        TenantLifecycle tenantLifecycle = TenantLifecycle.Active)
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
            NullLogger<ExecutionScheduleDispatchService>.Instance);
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

        public Task<ExecutionResponse> StartAsync(
            StartExecutionRequest request,
            string? idempotencyKey,
            CommandRequestContext requestContext,
            CancellationToken ct)
        {
            _ = request;
            _ = ct;
            StartCount++;
            LastPrincipalId = accessor.PrincipalId;
            LastIdempotencyKey = idempotencyKey;
            LastContext = requestContext;
            return Task.FromResult(new ExecutionResponse
            {
                ResourceId = StartedExecutionId,
                DisplayId = StartedExecutionId.ToString("D"),
                Status = ExecutionProjectionStatuses.Running
            });
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
