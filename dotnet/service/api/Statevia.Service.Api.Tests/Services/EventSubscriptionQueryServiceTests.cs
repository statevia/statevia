using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="EventSubscriptionQueryService"/> が read 権限のあとで候補を列挙する。</summary>
public sealed class EventSubscriptionQueryServiceTests
{
    /// <summary>executions.read を要求し、テナント内の候補を返す。</summary>
    [Fact]
    public async Task ListActiveAsync_RequiresRead_AndReturnsCandidates()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
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

        var authorization = new RecordingPermissionAuthorization();
        var tenant = new SettableTenantContextAccessor();
        tenant.Set(TestTenantIds.T1Context);
        var service = CreateService(db, authorization, tenant);

        // Act
        var candidates = await service.ListActiveAsync(CancellationToken.None);

        // Assert
        Assert.Equal(RuntimePermissionRequirements.ExecutionsRead, authorization.PermissionKey);
        Assert.Equal([new EventSubscriptionCandidate("orders.updated", "")], candidates);
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
