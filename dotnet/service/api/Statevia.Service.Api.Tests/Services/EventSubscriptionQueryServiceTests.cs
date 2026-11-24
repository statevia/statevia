using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="EventSubscriptionQueryService"/> が read 権限のあとで候補を列挙する。</summary>
public sealed class EventSubscriptionQueryServiceTests
{
    /// <summary>executions.read を要求し、呼び出し元が閲覧者として所属する Project の実行の候補を返す。</summary>
    [Fact]
    public async Task ListActiveAsync_RequiresRead_AndReturnsCandidates()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var (definitionId, readerPrincipalId) = await SeedReaderProjectAsync(db);
        await SeedSubscriptionAsync(db, definitionId);

        var authorization = new RecordingPermissionAuthorization();
        var service = CreateService(db, authorization, CallerContext(readerPrincipalId));

        // Act
        var candidates = await service.ListActiveAsync(CancellationToken.None);

        // Assert
        Assert.Equal(RuntimePermissionRequirements.ExecutionsRead, authorization.PermissionKey);
        Assert.Equal([new EventSubscriptionCandidate("orders.updated", "")], candidates);
    }

    /// <summary>どの Project にも所属しない呼び出し元には、テナント内に購読があっても空を返す。</summary>
    [Fact]
    public async Task ListActiveAsync_WhenCallerHasNoMembership_ReturnsEmpty()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var (definitionId, _) = await SeedReaderProjectAsync(db);
        await SeedSubscriptionAsync(db, definitionId);
        var outsiderPrincipalId = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ProjectTestData.AddPrincipal(ctx, TestTenantIds.T1TenantId, outsiderPrincipalId);
            await ctx.SaveChangesAsync();
        }

        var service = CreateService(db, new RecordingPermissionAuthorization(), CallerContext(outsiderPrincipalId));

        // Act
        var candidates = await service.ListActiveAsync(CancellationToken.None);

        // Assert
        Assert.Empty(candidates);
    }

    /// <summary>テナント文脈に Principal が無いと範囲を絞れないので、空を返す。</summary>
    [Fact]
    public async Task ListActiveAsync_WhenPrincipalMissing_ReturnsEmpty()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var (definitionId, _) = await SeedReaderProjectAsync(db);
        await SeedSubscriptionAsync(db, definitionId);
        var tenant = new SettableTenantContextAccessor();
        tenant.Set(TestTenantIds.T1Context);
        var service = CreateService(db, new RecordingPermissionAuthorization(), tenant);

        // Act
        var candidates = await service.ListActiveAsync(CancellationToken.None);

        // Assert
        Assert.Empty(candidates);
    }

    /// <summary>T1 に Project・定義・その Project の閲覧者を作る。</summary>
    private static async Task<(Guid DefinitionId, Guid ReaderPrincipalId)> SeedReaderProjectAsync(InMemoryTestDatabase db)
    {
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        await using var ctx = new CoreDbContext(db.Options);
        ctx.Projects.Add(new ProjectRow
        {
            ProjectId = projectId,
            TenantId = TestTenantIds.T1TenantId,
            Slug = $"p-{projectId:N}",
            DisplayName = "project",
            CreatedAt = DateTime.UtcNow
        });
        DefinitionTestData.AddDefinitionWithVersion(ctx, TestTenantIds.T1TenantId, definitionId, $"def-{definitionId:N}", projectId);
        var readerPrincipalId = ProjectTestData.AddMemberPrincipal(ctx, TestTenantIds.T1TenantId, projectId, ProjectAccessRole.Reader);
        await ctx.SaveChangesAsync();
        return (definitionId, readerPrincipalId);
    }

    private static ITenantContextAccessor CallerContext(Guid principalId)
    {
        var tenant = new SettableTenantContextAccessor();
        tenant.Set(new TenantContextState(TestTenantIds.T1TenantId, "t1", principalId, TenantLifecycle.Active));
        return tenant;
    }

    private static async Task SeedSubscriptionAsync(InMemoryTestDatabase db, Guid definitionId)
    {
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false,
            });
            ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = Guid.NewGuid(),
                ExecutionId = executionId,
                NodeId = "wait-a",
                Topic = "orders.updated",
                CorrelationKey = "",
                ResumeEventName = "statevia.event.subscribe.0",
                CreatedAt = now,
            });
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>テナント未解決では空を返し、他テナントの購読は読まない。</summary>
    [Fact]
    public async Task ListActiveAsync_WhenTenantUnresolved_ReturnsEmpty()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var authorization = new RecordingPermissionAuthorization();
        var service = CreateService(db, authorization, new SettableTenantContextAccessor());

        // Act
        var candidates = await service.ListActiveAsync(CancellationToken.None);

        // Assert
        Assert.Equal(RuntimePermissionRequirements.ExecutionsRead, authorization.PermissionKey);
        Assert.Empty(candidates);
    }

    private static EventSubscriptionQueryService CreateService(
        InMemoryTestDatabase db,
        IRuntimePermissionAuthorization authorization,
        ITenantContextAccessor tenantContext)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        return new EventSubscriptionQueryService(
            new TestCoreTransactionExecutor(uowFactory),
            new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()),
            authorization,
            tenantContext);
    }

    private sealed class RecordingPermissionAuthorization : IRuntimePermissionAuthorization
    {
        public string? PermissionKey { get; private set; }

        public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
        {
            PermissionKey = permissionKey;
            return Task.CompletedTask;
        }
    }
}
