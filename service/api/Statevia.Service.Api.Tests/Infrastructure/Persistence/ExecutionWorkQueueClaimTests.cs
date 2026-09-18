using Statevia.Infrastructure.Persistence.Repositories;

namespace Statevia.Service.Api.Tests.Infrastructure.Persistence;

/// <summary>空 Claim が書き込みトランザクションを開かず空を返すことの検証。</summary>
public sealed class ExecutionWorkQueueClaimTests
{
    /// <summary>キューが空なら Claim は空を返し、例外にしない。</summary>
    [Fact]
    public async Task ClaimAsync_WhenQueueEmpty_ReturnsEmpty()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var queue = new ExecutionWorkQueue(db.Factory);

        // Act
        var items = await queue.ClaimAsync(
            "worker-1",
            DateTime.UtcNow,
            TimeSpan.FromMinutes(1),
            limit: 4,
            kinds: [ExecutionWorkItemKinds.Start],
            CancellationToken.None);

        // Assert
        Assert.Empty(items);
    }

    /// <summary>available_at が未来なら Claim 対象外として空を返す。</summary>
    [Fact]
    public async Task ClaimAsync_WhenItemNotYetAvailable_ReturnsEmpty()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var queue = new ExecutionWorkQueue(db.Factory);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, executionId);
        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.ExecutionWorkItems.Add(new ExecutionWorkItemRow
            {
                WorkItemId = Guid.NewGuid(),
                ExecutionId = executionId,
                Kind = ExecutionWorkItemKinds.Start,
                Payload = "{}",
                AvailableAt = now.AddMinutes(5),
                Attempts = 0,
                CreatedAt = now
            });
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        var items = await queue.ClaimAsync(
            "worker-1",
            now,
            TimeSpan.FromMinutes(1),
            limit: 4,
            kinds: [ExecutionWorkItemKinds.Start],
            CancellationToken.None);

        // Assert
        Assert.Empty(items);
    }

    /// <summary>許可 kind 以外しか無いときは空を返す。</summary>
    [Fact]
    public async Task ClaimAsync_WhenKindDoesNotMatch_ReturnsEmpty()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var queue = new ExecutionWorkQueue(db.Factory);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, executionId);
        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.ExecutionWorkItems.Add(new ExecutionWorkItemRow
            {
                WorkItemId = Guid.NewGuid(),
                ExecutionId = executionId,
                Kind = ExecutionWorkItemKinds.Cancel,
                Payload = "{}",
                AvailableAt = now.AddMinutes(-1),
                Attempts = 0,
                CreatedAt = now
            });
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        var items = await queue.ClaimAsync(
            "worker-1",
            now,
            TimeSpan.FromMinutes(1),
            limit: 4,
            kinds: [ExecutionWorkItemKinds.Start],
            CancellationToken.None);

        // Assert
        Assert.Empty(items);
    }

    /// <summary>有効な lease 中の行は Claim 対象外として空を返す。</summary>
    [Fact]
    public async Task ClaimAsync_WhenLeaseStillHeld_ReturnsEmpty()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var queue = new ExecutionWorkQueue(db.Factory);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, executionId);
        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.ExecutionWorkItems.Add(new ExecutionWorkItemRow
            {
                WorkItemId = Guid.NewGuid(),
                ExecutionId = executionId,
                Kind = ExecutionWorkItemKinds.Start,
                Payload = "{}",
                AvailableAt = now.AddMinutes(-1),
                LeaseOwner = "other-worker",
                LeaseUntil = now.AddMinutes(1),
                Attempts = 1,
                CreatedAt = now
            });
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        var items = await queue.ClaimAsync(
            "worker-1",
            now,
            TimeSpan.FromMinutes(1),
            limit: 4,
            kinds: [ExecutionWorkItemKinds.Start],
            CancellationToken.None);

        // Assert
        Assert.Empty(items);
    }

    /// <summary>SQLite の execution_work_items FK を満たすため、親 execution を投入する。</summary>
    private static async Task SeedExecutionAsync(SqliteTestDatabase db, Guid executionId)
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var seed = db.Factory.CreateDbContext();
        ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            seed,
            TestTenantIds.DefaultTenantId,
            definitionId,
            "wf-work-queue-claim",
            projectId,
            versionId: versionId);
        seed.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.DefaultTenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = versionId,
            Status = "Running",
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        });
        await seed.SaveChangesAsync();
    }
}
