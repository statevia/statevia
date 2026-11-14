using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary>実行の一覧の待機・要対応・期間の絞り込みと <c>startedAt</c> の並び順を SQLite（リレーショナル）で確かめる。</summary>
public sealed class ExecutionRepositoryWaitingListTests
{
    private static readonly DateTime Day1 = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);

    private sealed record Seeded(Guid PrincipalId, Guid EventWaiting, Guid Plain, Guid Failed, Guid ForkParent);

    /// <summary>待機中の実行だけに絞り、各要素の waiting を返す。物理子の Wait はルートに数える。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_WhenWaitingTrue_ReturnsOnlyWaitingRoots()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        // Act
        var (total, items) = await ListAsync(db, seeded.PrincipalId, waiting: true);

        // Assert
        Assert.Equal(2, total);
        Assert.Equal(
            new[] { seeded.EventWaiting, seeded.ForkParent }.Order(),
            items.Select(item => item.Execution.ExecutionId).Order());
        Assert.All(items, item => Assert.True(item.Waiting));
    }

    /// <summary>waiting=false は待機中でないルートの実行だけを返す。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_WhenWaitingFalse_ReturnsNotWaitingRoots()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        // Act
        var (_, items) = await ListAsync(db, seeded.PrincipalId, waiting: false);

        // Assert
        Assert.Equal(
            new[] { seeded.Plain, seeded.Failed }.Order(),
            items.Select(item => item.Execution.ExecutionId).Order());
        Assert.All(items, item => Assert.False(item.Waiting));
    }

    /// <summary>needsAttention=true は待機中と失敗の和を返す。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_WhenNeedsAttentionTrue_ReturnsWaitingAndFailed()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        // Act
        var (total, items) = await ListAsync(db, seeded.PrincipalId, needsAttention: true);

        // Assert
        Assert.Equal(3, total);
        Assert.DoesNotContain(items, item => item.Execution.ExecutionId == seeded.Plain);
    }

    /// <summary>startedAt の降順に並べ、期間は下限を含み上限を含まない。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_WhenSortedByStartedAtWithRange_ReturnsNewestFirstInRange()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var seeded = await SeedAsync(db);

        // Act
        var (_, items) = await ListAsync(
            db,
            seeded.PrincipalId,
            sort: new SortQuery(ExecutionListSortKeys.StartedAt, ListSortOrders.Desc),
            startedFrom: Day2,
            startedTo: Day3.AddHours(1));

        // Assert
        Assert.Equal(
            [seeded.Failed, seeded.Plain],
            items.Select(item => item.Execution.ExecutionId));
    }

    private static async Task<(int Total, List<(ExecutionRow Execution, string? DisplayId, bool Waiting)> Items)> ListAsync(
        SqliteTestDatabase db,
        Guid principalId,
        bool? waiting = null,
        bool? needsAttention = null,
        SortQuery? sort = null,
        DateTime? startedFrom = null,
        DateTime? startedTo = null)
    {
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();
        return await new ExecutionRepository().ListWithDisplayIdsPageAsync(
            uow,
            ExecutionWaitingTestData.TenantId,
            principalId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 50),
                Sort: sort ?? new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: null,
                NameContains: null,
                StartedFrom: startedFrom,
                StartedTo: startedTo,
                Waiting: waiting,
                NeedsAttention: needsAttention),
            CancellationToken.None);
    }

    /// <summary>EventWait の実行（Day1）、待機なし（Day2）、失敗（Day3）、子に EventWait を持つ親（Day1）と子を投入する。</summary>
    private static async Task<Seeded> SeedAsync(SqliteTestDatabase db)
    {
        await using var ctx = db.Factory.CreateDbContext();
        var data = new ExecutionWaitingTestData(ctx);
        var projectId = Guid.NewGuid();
        var principalId = data.AddProjectWithMember(projectId);
        var definitionId = Guid.NewGuid();
        data.AddDefinition(definitionId, projectId);
        var eventWaiting = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
        var plain = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day2);
        var failed = data.AddExecution(definitionId, ExecutionProjectionStatuses.Failed, Day3);
        var forkParent = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
        var forkChild = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
        data.AddWait(eventWaiting, ExecutionWaitKind.EventWait);
        data.AddBranch(forkParent, forkChild);
        data.AddWait(forkChild, ExecutionWaitKind.EventWait);
        await ctx.SaveChangesAsync();
        return new Seeded(principalId, eventWaiting, plain, failed, forkParent);
    }
}
