using Statevia.Core.Application.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionAuthorizationGuard"/> の横断認可呼び出し。</summary>
public sealed class ExecutionAuthorizationGuardTests
{
    /// <summary>read は executions:read を要求する。</summary>
    [Fact]
    public async Task EnsureExecutionsReadAsync_RequestsExecutionsRead()
    {
        // Arrange
        var runtimeAuth = new RecordingRuntimePermissionAuthorization();
        var sut = CreateSut(runtimeAuth: runtimeAuth);

        // Act
        await sut.EnsureExecutionsReadAsync(CancellationToken.None);

        // Assert
        Assert.Equal([RuntimePermissionRequirements.ExecutionsRead], runtimeAuth.RequestedKeys);
    }

    /// <summary>write は executions:write を要求する。</summary>
    [Fact]
    public async Task EnsureExecutionsWriteAsync_RequestsExecutionsWrite()
    {
        // Arrange
        var runtimeAuth = new RecordingRuntimePermissionAuthorization();
        var sut = CreateSut(runtimeAuth: runtimeAuth);

        // Act
        await sut.EnsureExecutionsWriteAsync(CancellationToken.None);

        // Assert
        Assert.Equal([RuntimePermissionRequirements.ExecutionsWrite], runtimeAuth.RequestedKeys);
    }

    /// <summary>mutation は snapshot 付きで executions:write を要求する。</summary>
    [Fact]
    public async Task EnsureExecutionMutationWriteAsync_RequestsMutationWrite()
    {
        // Arrange
        var mutationAuth = new RecordingExecutionMutationAuthorization();
        var sut = CreateSut(mutationAuth: mutationAuth);
        var execution = new ExecutionRow
        {
            ExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecuritySnapshotJson = null
        };

        // Act
        await sut.EnsureExecutionMutationWriteAsync(execution, CancellationToken.None);

        // Assert
        Assert.Single(mutationAuth.Calls);
        Assert.Equal(RuntimePermissionRequirements.ExecutionsWrite, mutationAuth.Calls[0].PermissionKey);
    }

    /// <summary>execution が null なら ArgumentNullException。</summary>
    [Fact]
    public async Task EnsureExecutionMutationWriteAsync_WhenExecutionNull_Throws()
    {
        // Arrange
        var sut = CreateSut();

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.EnsureExecutionMutationWriteAsync(null!, CancellationToken.None));
    }

    /// <summary>定義のプロジェクトが無ければ NotFound。</summary>
    [Fact]
    public async Task EnsureCanExecuteOnDefinitionAsync_WhenProjectMissing_ThrowsNotFound()
    {
        // Arrange
        var sut = CreateSut(definitions: new StubDefinitionRepository(projectId: null));

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.EnsureCanExecuteOnDefinitionAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>プロジェクト解決後に EnsureCanExecute を呼ぶ。</summary>
    [Fact]
    public async Task EnsureCanExecuteOnDefinitionAsync_WhenProjectResolved_CallsProjectAuth()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var projectAuth = new RecordingProjectAuthorizationService();
        var sut = CreateSut(
            projectAuth: projectAuth,
            definitions: new StubDefinitionRepository(projectId));

        // Act
        await sut.EnsureCanExecuteOnDefinitionAsync(tenantId, definitionId, CancellationToken.None);

        // Assert
        Assert.Single(projectAuth.ExecuteCalls);
        Assert.Equal(tenantId, projectAuth.ExecuteCalls[0].TenantId);
        Assert.Equal(projectId, projectAuth.ExecuteCalls[0].ProjectId);
    }

    /// <summary>論理削除済み定義は EnsureCanExecute せず NotFound。</summary>
    [Fact]
    public async Task EnsureCanExecuteOnDefinitionAsync_WhenDefinitionSoftDeleted_ThrowsNotFoundWithoutExecute()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var tenantId = TestTenantIds.DefaultTenantId;
        var deletedAt = DateTime.UtcNow;

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, tenantId, "default", projectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, tenantId, definitionId, "def", projectId);
            var definition = await seed.Definitions.FindAsync(definitionId);
            definition!.DeletedAt = deletedAt;
            await seed.SaveChangesAsync();
        }

        var projectAuth = new RecordingProjectAuthorizationService();
        var sut = new ExecutionAuthorizationGuard(
            new AllowAllRuntimePermissionAuthorization(),
            new AllowAllExecutionMutationAuthorization(),
            projectAuth,
            TestRepositoryFactory.CreateDefinitionRepository(),
            new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)),
            new PrincipalResourceGrantAuthorization(
                new EmptyPrincipalResourceGrantStore(),
                db.TenantAccessor,
                new MissingPrincipalDataAccess()));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.EnsureCanExecuteOnDefinitionAsync(tenantId, definitionId, CancellationToken.None));
        Assert.Empty(projectAuth.ExecuteCalls);
    }

    /// <summary>許可行が無い Principal はプロジェクト実行権だけで通る。</summary>
    [Fact]
    public async Task EnsureResourceGrantAsync_WhenNoGrants_Allows()
    {
        // Arrange
        var sut = CreateGrantSut(grants: []);

        // Act
        var act = () => sut.Guard.EnsureResourceGrantAsync(sut.TenantId, sut.DefinitionId, CancellationToken.None);

        // Assert
        await act();
    }

    /// <summary>project 許可の外は RESOURCE_GRANT_DENIED。</summary>
    [Fact]
    public async Task EnsureResourceGrantAsync_WhenProjectOutsideGrant_Denies()
    {
        // Arrange
        var sut = CreateGrantSut(grants:
        [
            Grant(PrincipalResourceGrantKinds.Project, Guid.NewGuid())
        ]);

        // Act
        var act = () => sut.Guard.EnsureResourceGrantAsync(sut.TenantId, sut.DefinitionId, CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
    }

    /// <summary>definition 許可の外は RESOURCE_GRANT_DENIED。</summary>
    [Fact]
    public async Task EnsureResourceGrantAsync_WhenDefinitionOutsideGrant_Denies()
    {
        // Arrange
        var sut = CreateGrantSut(grants:
        [
            Grant(PrincipalResourceGrantKinds.Definition, Guid.NewGuid())
        ]);

        // Act
        var act = () => sut.Guard.EnsureResourceGrantAsync(sut.TenantId, sut.DefinitionId, CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
    }

    /// <summary>両方の許可があるときは、片方だけでは通らない。</summary>
    [Fact]
    public async Task EnsureResourceGrantAsync_WhenBothKindsAndOneMisses_Denies()
    {
        // Arrange
        var sut = CreateGrantSut(grants:
        [
            Grant(PrincipalResourceGrantKinds.Project),
            Grant(PrincipalResourceGrantKinds.Definition, Guid.NewGuid())
        ]);

        // Act
        var act = () => sut.Guard.EnsureResourceGrantAsync(sut.TenantId, sut.DefinitionId, CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
    }

    /// <summary>両方の許可に含まれる定義は通る。</summary>
    [Fact]
    public async Task EnsureResourceGrantAsync_WhenBothKindsMatch_Allows()
    {
        // Arrange
        var sut = CreateGrantSut(grants:
        [
            Grant(PrincipalResourceGrantKinds.Project),
            Grant(PrincipalResourceGrantKinds.Definition)
        ]);

        // Act
        var act = () => sut.Guard.EnsureResourceGrantAsync(sut.TenantId, sut.DefinitionId, CancellationToken.None);

        // Assert
        await act();
    }

    /// <summary>System Principal は許可行があっても評価しない。</summary>
    [Fact]
    public async Task EnsureResourceGrantAsync_WhenSystemPrincipal_SkipsGrants()
    {
        // Arrange
        var store = new RecordingGrantStore(
        [
            Grant(PrincipalResourceGrantKinds.Project, Guid.NewGuid())
        ]);
        var sut = CreateGrantSut(store, PrincipalType.System);

        // Act
        await sut.Guard.EnsureResourceGrantAsync(sut.TenantId, sut.DefinitionId, CancellationToken.None);

        // Assert
        Assert.Equal(0, store.ListCount);
    }

    private static GrantSut CreateGrantSut(
        IReadOnlyList<PrincipalResourceGrantRow> grants,
        PrincipalType principalType = PrincipalType.User) =>
        CreateGrantSut(new RecordingGrantStore(grants), principalType);

    private static GrantSut CreateGrantSut(
        RecordingGrantStore store,
        PrincipalType principalType)
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(new TenantContextState(tenantId, "tenant", principalId, TenantLifecycle.Active));
        store.Bind(tenantId, principalId, projectId, definitionId);
        var guard = CreateSut(
            definitions: new StubDefinitionRepository(projectId),
            resourceGrants: store,
            tenantContext: accessor,
            principals: new FixedPrincipalDataAccess(principalId, tenantId, principalType));
        return new GrantSut(guard, tenantId, definitionId, projectId);
    }

    private static PrincipalResourceGrantRow Grant(string kind, Guid? resourceId = null) =>
        new()
        {
            ResourceKind = kind,
            ResourceId = resourceId ?? Guid.Empty
        };

    private sealed record GrantSut(
        ExecutionAuthorizationGuard Guard,
        Guid TenantId,
        Guid DefinitionId,
        Guid ProjectId);

    private static ExecutionAuthorizationGuard CreateSut(
        IRuntimePermissionAuthorization? runtimeAuth = null,
        IExecutionMutationAuthorization? mutationAuth = null,
        IProjectAuthorizationService? projectAuth = null,
        IDefinitionRepository? definitions = null,
        IPrincipalResourceGrantStore? resourceGrants = null,
        ITenantContextAccessor? tenantContext = null,
        IPrincipalDataAccess? principals = null) =>
        new(
            runtimeAuth ?? new AllowAllRuntimePermissionAuthorization(),
            mutationAuth ?? new AllowAllExecutionMutationAuthorization(),
            projectAuth ?? new AllowAllProjectAuthorizationService(),
            definitions ?? new StubDefinitionRepository(Guid.NewGuid()),
            new ImmediateReadOnlyExecutor(),
            new PrincipalResourceGrantAuthorization(
                resourceGrants ?? new EmptyPrincipalResourceGrantStore(),
                tenantContext ?? new SettableTenantContextAccessor(),
                principals ?? new MissingPrincipalDataAccess()));

    private sealed class RecordingGrantStore(IReadOnlyList<PrincipalResourceGrantRow> rows) : IPrincipalResourceGrantStore
    {
        private Guid _principalId;
        private Guid _projectId;
        private Guid _definitionId;

        public int ListCount { get; private set; }

        public void Bind(Guid tenantId, Guid principalId, Guid projectId, Guid definitionId)
        {
            _ = tenantId;
            _principalId = principalId;
            _projectId = projectId;
            _definitionId = definitionId;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
            Guid principalId,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            ListCount++;
            IReadOnlyList<PrincipalResourceGrantRow> matched = rows
                .Select(row => new PrincipalResourceGrantRow
                {
                    PrincipalId = _principalId,
                    ResourceKind = row.ResourceKind,
                    ResourceId = ResolveResourceId(row),
                    CreatedAt = DateTime.UtcNow
                })
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

        private Guid ResolveResourceId(PrincipalResourceGrantRow row)
        {
            if (row.ResourceId != Guid.Empty)
                return row.ResourceId;
            return row.ResourceKind == PrincipalResourceGrantKinds.Project ? _projectId : _definitionId;
        }
    }

    private sealed class FixedPrincipalDataAccess(Guid principalId, Guid tenantId, PrincipalType principalType) : IPrincipalDataAccess
    {
        /// <inheritdoc />
        public Task<PrincipalInfo?> FindPrincipalAsync(Guid id, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            PrincipalInfo? info = id == principalId
                ? new PrincipalInfo(principalId, tenantId, principalType, true, null, null)
                : null;
            return Task.FromResult(info);
        }

        /// <inheritdoc />
        public Task<TenantInfo?> FindTenantAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<bool> IsTenantAdminAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRuntimePermissionAuthorization : IRuntimePermissionAuthorization
    {
        public List<string> RequestedKeys { get; } = [];

        public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
        {
            RequestedKeys.Add(permissionKey);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExecutionMutationAuthorization : IExecutionMutationAuthorization
    {
        public List<(ExecutionSecuritySnapshot? Snapshot, string PermissionKey)> Calls { get; } = [];

        public Task EnsureMutationPermissionAsync(
            ExecutionSecuritySnapshot? snapshot,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            Calls.Add((snapshot, permissionKey));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProjectAuthorizationService : IProjectAuthorizationService
    {
        public List<(Guid TenantId, Guid ProjectId)> ExecuteCalls { get; } = [];

        public Task EnsureCanReadAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid projectId,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnsureCanExecuteAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid projectId,
            CancellationToken ct)
        {
            ExecuteCalls.Add((tenantId, projectId));
            return Task.CompletedTask;
        }

        public Task EnsureCanPublishAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid projectId,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class StubDefinitionRepository(Guid? projectId) : IDefinitionRepository
    {
        public Task<Guid?> ResolveProjectIdAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid definitionId,
            CancellationToken ct) =>
            Task.FromResult(projectId);

        public Task<DefinitionDetail?> GetLatestForApiAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionRow?> GetLatestForMutationAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionVersionRow?> GetVersionForExecutionAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, int version, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionVersionRow?> GetVersionForExecutionByIdAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionVersionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionVersionRow?> GetVersionForApiAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, int version, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionRow?> GetDeletedCatalogEntryAsync(
            ICoreUnitOfWork uow, Guid tenantId, Guid definitionId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task AddWithInitialVersionAsync(
            ICoreUnitOfWork uow, DefinitionRow definition, DefinitionVersionRow version, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionDetail?> PublishVersionAsync(
            ICoreUnitOfWork uow,
            DefinitionVersionPublishCommand command,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            DefinitionListPageQuery query,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionSoftDeleteOutcome> SoftDeleteAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid definitionId,
            DateTime deletedAt,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<DefinitionDetail?> RestoreAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid definitionId,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> ExistsActiveSlugInProjectAsync(
            ICoreUnitOfWork uow,
            Guid projectId,
            string slug,
            Guid excludingDefinitionId,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class ImmediateReadOnlyExecutor : ICoreTransactionExecutor
    {
        public Task ExecuteReadCommittedAsync(
            Func<ICoreUnitOfWork, CancellationToken, Task> work,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<T> ExecuteReadCommittedAsync<T>(
            Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task ExecuteReadOnlyAsync(
            Func<ICoreUnitOfWork, CancellationToken, Task> work,
            CancellationToken cancellationToken = default) =>
            work(StubUnitOfWork.Instance, cancellationToken);

        public Task<T> ExecuteReadOnlyAsync<T>(
            Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
            CancellationToken cancellationToken = default) =>
            work(StubUnitOfWork.Instance, cancellationToken);
    }

    private sealed class StubUnitOfWork : ICoreUnitOfWork
    {
        public static readonly StubUnitOfWork Instance = new();
        public ICoreDatabase Db => throw new NotImplementedException();
        public Task BeginTransactionAsync(System.Data.IsolationLevel level, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
