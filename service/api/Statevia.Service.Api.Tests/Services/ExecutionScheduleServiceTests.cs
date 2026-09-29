using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Common;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionScheduleService"/> の CRUD と認可境界。</summary>
public sealed class ExecutionScheduleServiceTests
{
    /// <summary>作成で next_fire_at が UTC の未来になり、定義 YAML は変わらない。</summary>
    [Fact]
    public async Task CreateAsync_PersistsNextFireWithoutChangingYaml()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        var caller = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, sourceYaml: "states: []");
        var sut = CreateSut(db, caller, runAs);

        // Act
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, created.ScheduleId);
        Assert.Equal(DateTimeKind.Utc, created.NextFireAt.Kind);
        Assert.True(created.NextFireAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(caller, created.CreatedByPrincipalId);
        Assert.Null(created.DefinitionVersionId);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Equal("states: []", (await verify.DefinitionVersions.SingleAsync()).SourceYaml);
        Assert.Equal(0, await verify.ServiceAccounts.CountAsync());
    }

    /// <summary>一覧は input を持たず、単票は input を返す。</summary>
    [Fact]
    public async Task ListAsync_OmitsInput_GetAsync_IncludesInput()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.Input = JsonDocument.Parse("""{"k":1}""").RootElement.Clone();
        var created = await sut.CreateAsync(request, CancellationToken.None);

        // Act
        var list = await sut.ListAsync(CancellationToken.None);
        var detail = await sut.GetAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        Assert.Single(list);
        Assert.IsType<ExecutionScheduleListItemDto>(list[0]);
        Assert.Equal(JsonValueKind.Object, detail.Input?.ValueKind);
        Assert.Equal(1, detail.Input?.GetProperty("k").GetInt32());
    }

    /// <summary>User を run-as にすると 422。</summary>
    [Fact]
    public async Task CreateAsync_WhenRunAsUser_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var userPrincipal = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var principals = new StubPrincipalDataAccess();
        principals.Add(userPrincipal, TestTenantIds.DefaultTenantId, PrincipalType.User, isActive: true);
        var sut = CreateSut(db, Guid.NewGuid(), userPrincipal, principals);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, userPrincipal), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Contains("runAsPrincipalId", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>run-as の許可に無い定義ではスケジュールを保存しない。</summary>
    [Fact]
    public async Task CreateAsync_WhenRunAsLacksGrant_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var grants = new MutableGrantStore();
        grants.Rows.Add(Grant(runAs, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));
        var sut = CreateSut(db, Guid.NewGuid(), runAs, resourceGrants: grants);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Empty(verify.ExecutionSchedules.IgnoreQueryFilters());
    }

    /// <summary>割り当て後に許可を狭めると、定義を変えない更新も拒否する。</summary>
    [Fact]
    public async Task UpdateAsync_WhenGrantNarrowed_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var grants = new MutableGrantStore();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, resourceGrants: grants);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        grants.Rows.Add(Grant(runAs, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));

        // Act
        var act = () => sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Name = "renamed-job" },
            CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
        var stored = await sut.GetAsync(created.ScheduleId, CancellationToken.None);
        Assert.Equal("nightly-job", stored.Name);
    }

    /// <summary>不正 cron は 422 で field=cronExpression。</summary>
    [Fact]
    public async Task CreateAsync_WhenCronInvalid_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.CronExpression = "not-a-cron";

        // Act
        var act = () => sut.CreateAsync(request, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("cronExpression", ReadField(ex));
    }

    /// <summary>不明 TZ は 422 で field=timeZone。</summary>
    [Fact]
    public async Task CreateAsync_WhenTimeZoneUnknown_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.TimeZone = "Not/AZone";

        // Act
        var act = () => sut.CreateAsync(request, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("timeZone", ReadField(ex));
    }

    /// <summary>存在しない版指定は 404。</summary>
    [Fact]
    public async Task CreateAsync_WhenVersionMissing_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.DefinitionVersion = 99;

        // Act
        var act = () => sut.CreateAsync(request, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    /// <summary>他テナントの行は 404。</summary>
    [Fact]
    public async Task GetAsync_WhenOtherTenantRow_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, tenantId: TestTenantIds.T1TenantId);
        var otherSut = CreateSut(
            db,
            Guid.NewGuid(),
            runAs,
            tenant: TestTenantIds.T1Context with { PrincipalId = Guid.NewGuid() },
            runAsTenantId: TestTenantIds.T1TenantId);
        var created = await otherSut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        var defaultSut = CreateSut(db, Guid.NewGuid(), runAs);

        // Act
        var act = () => defaultSut.GetAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    /// <summary>enabled=false にすると行は残るが Dispatcher 対象外。</summary>
    [Fact]
    public async Task UpdateAsync_WhenDisabled_PersistsEnabledFalse()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        var updated = await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Assert
        Assert.False(updated.Enabled);
        Assert.Equal(created.NextFireAt, updated.NextFireAt);
    }

    /// <summary>論理削除後は取得できない。</summary>
    [Fact]
    public async Task DeleteAsync_HidesScheduleFromGet()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        await sut.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync(created.ScheduleId, CancellationToken.None));
    }

    /// <summary>手動実行は Dispatcher に委譲し、cron の next は変えない。</summary>
    [Fact]
    public async Task RunNowAsync_WhenEnabled_DelegatesWithoutAdvancingNextFire()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        var started = await sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        Assert.Equal(dispatch.StartedExecutionId, started.ResourceId);
        Assert.Equal(1, dispatch.RunCount);
        Assert.Equal("manual-1", dispatch.LastIdempotencyKey);
        var detail = await sut.GetAsync(created.ScheduleId, CancellationToken.None);
        Assert.Equal(created.NextFireAt, detail.NextFireAt);
    }

    /// <summary>無効スケジュールの手動実行は 422 で Start しない。</summary>
    [Fact]
    public async Task RunNowAsync_WhenDisabled_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("enabled", ReadField(ex));
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>論理削除後の手動実行は 404。</summary>
    [Fact]
    public async Task RunNowAsync_WhenDeleted_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>同一テナントの重複名は 422。</summary>
    [Fact]
    public async Task CreateAsync_WhenNameDuplicated_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("name", ReadField(ex));
    }

    private static CreateExecutionScheduleRequest NewCreateRequest(Guid definitionId, Guid runAs) =>
        new()
        {
            Name = "nightly-job",
            DefinitionId = definitionId.ToString("D"),
            CronExpression = "0 3 * * *",
            TimeZone = "Asia/Tokyo",
            RunAsPrincipalId = runAs
        };

    private static string? ReadField(ApiValidationException exception)
    {
        var field = exception.Details?.GetType().GetProperty("field")?.GetValue(exception.Details);
        return field as string;
    }

    private static async Task SeedDefinitionAsync(
        SqliteTestDatabase db,
        Guid definitionId,
        string sourceYaml = "x",
        Guid? tenantId = null)
    {
        var resolvedTenant = tenantId ?? TestTenantIds.DefaultTenantId;
        var tenantKey = resolvedTenant == TestTenantIds.T1TenantId ? "t1" : "default";
        await using var ctx = db.Factory.CreateDbContext();
        var projectId = Guid.NewGuid();
        ProjectTestData.AddDefaultProject(ctx, resolvedTenant, tenantKey, projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            ctx,
            resolvedTenant,
            definitionId,
            "wf-schedule",
            projectId,
            sourceYaml: sourceYaml);
        await ctx.SaveChangesAsync();
    }

    private static ExecutionScheduleService CreateSut(
        SqliteTestDatabase db,
        Guid callerPrincipalId,
        Guid runAsPrincipalId,
        StubPrincipalDataAccess? principals = null,
        TenantContextState? tenant = null,
        Guid? runAsTenantId = null,
        IExecutionScheduleDispatchService? dispatch = null,
        IPrincipalResourceGrantStore? resourceGrants = null)
    {
        var tenantState = tenant ?? TestTenantIds.DefaultContext with { PrincipalId = callerPrincipalId };
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(tenantState);
        var principalAccess = principals ?? new StubPrincipalDataAccess();
        if (principals is null)
        {
            principalAccess.Add(
                runAsPrincipalId,
                runAsTenantId ?? tenantState.TenantId,
                PrincipalType.ServiceAccount,
                isActive: true);
        }

        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var guard = new ExecutionAuthorizationGuard(
            new AllowAllRuntimePermissionAuthorization(),
            new AllowAllExecutionMutationAuthorization(),
            new AllowAllProjectAuthorizationService(),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new TestCoreTransactionExecutor(uowFactory),
            new PrincipalResourceGrantAuthorization(
                resourceGrants ?? new EmptyPrincipalResourceGrantStore(),
                accessor,
                principalAccess));

        var transactionExecutor = new TestCoreTransactionExecutor(uowFactory);
        return new ExecutionScheduleService(
            new ExecutionScheduleRepository(db.Factory),
            guard,
            new ExecutionScheduleDefinitionResolver(
                new GuidParsingDisplayIdService(),
                TestRepositoryFactory.CreateDefinitionRepository(),
                transactionExecutor),
            principalAccess,
            accessor,
            new DefaultIdGenerator(),
            dispatch ?? new RecordingDispatchService());
    }

    private sealed class GuidParsingDisplayIdService : IDisplayIdService
    {
        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            _ = kind;
            return Task.FromResult(Guid.TryParse(idOrUuid, out var id) ? id : (Guid?)null);
        }

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(
            string kind,
            IEnumerable<Guid> resourceIds,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPrincipalDataAccess : IPrincipalDataAccess
    {
        private readonly Dictionary<Guid, PrincipalInfo> _principals = [];

        public void Add(Guid principalId, Guid tenantId, PrincipalType type, bool isActive) =>
            _principals[principalId] = new PrincipalInfo(principalId, tenantId, type, isActive, null, null);

        public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(_principals.TryGetValue(principalId, out var info) ? info : null);

        public Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantInfo?>(new TenantInfo(tenantId, "default", TenantLifecycle.Active));

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GroupSnapshot>>([]);
    }

    private static PrincipalResourceGrantRow Grant(Guid principalId, string kind, Guid resourceId) =>
        new()
        {
            PrincipalId = principalId,
            ResourceKind = kind,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class MutableGrantStore : IPrincipalResourceGrantStore
    {
        public List<PrincipalResourceGrantRow> Rows { get; } = [];

        /// <inheritdoc />
        public Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
            Guid principalId,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            IReadOnlyList<PrincipalResourceGrantRow> matched = Rows
                .Where(row => row.PrincipalId == principalId)
                .ToList();
            return Task.FromResult(matched);
        }

        /// <inheritdoc />
        public Task ReplaceAsync(
            Guid principalId,
            IReadOnlyCollection<Guid> projectIds,
            IReadOnlyCollection<Guid> definitionIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDispatchService : IExecutionScheduleDispatchService
    {
        public int RunCount { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public Guid StartedExecutionId { get; } = Guid.NewGuid();

        public Task<int> DispatchDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<ExecutionResponse> RunManuallyAsync(
            Guid scheduleId,
            string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            _ = scheduleId;
            _ = cancellationToken;
            RunCount++;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(new ExecutionResponse
            {
                DisplayId = "exec-manual",
                ResourceId = StartedExecutionId,
                Status = "Running"
            });
        }
    }
}
