using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionSummaryRepository"/> の件数・最後の実行・待機中の判定を SQLite（リレーショナル）で確かめる。</summary>
public sealed class ExecutionSummaryRepositoryTests
{
    private static readonly DateTime Day1 = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);
    private static readonly DateTime Day5 = Day1.AddDays(4);

    /// <summary>状態ごと・待機中・要対応の件数を数える。DelayWait だけの実行は待機中にしない。物理子の Wait はルートに数え、子は件数に入れない。</summary>
    [Fact]
    public async Task CountAsync_CountsByStatusWaitingAndNeedsAttention()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        Guid principalId;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            var data = new ExecutionWaitingTestData(ctx);
            var projectId = Guid.NewGuid();
            principalId = data.AddProjectWithMember(projectId);
            data.AddDefinition(definitionId, projectId);
            var eventWaiting = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            var delayWaiting = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Failed, Day1);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Completed, Day1);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Cancelled, Day1);
            var forkParent = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            var forkChild = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            data.AddWait(eventWaiting, ExecutionWaitKind.EventWait);
            data.AddWait(delayWaiting, ExecutionWaitKind.DelayWait);
            data.AddBranch(forkParent, forkChild);
            data.AddWait(forkChild, ExecutionWaitKind.EventWait);
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();

        // Act
        var counts = await repo.CountAsync(
            uow,
            ExecutionWaitingTestData.TenantId,
            principalId,
            new ExecutionCountFilter(null, null, null, null, null),
            CancellationToken.None);

        // Assert
        Assert.Equal(new ExecutionCounts(Total: 7, Running: 4, Completed: 1, Failed: 1, Cancelled: 1, Waiting: 2, NeedsAttention: 3), counts);
    }

    /// <summary>メンバーでない Project の実行は件数に入れない。</summary>
    [Fact]
    public async Task CountAsync_WhenProjectIsNotAccessible_ExcludesItsExecutions()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        Guid principalId;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            var data = new ExecutionWaitingTestData(ctx);
            var memberProjectId = Guid.NewGuid();
            var otherProjectId = Guid.NewGuid();
            principalId = data.AddProjectWithMember(memberProjectId);
            data.AddProject(otherProjectId);
            var memberDefinitionId = Guid.NewGuid();
            var otherDefinitionId = Guid.NewGuid();
            data.AddDefinition(memberDefinitionId, memberProjectId);
            data.AddDefinition(otherDefinitionId, otherProjectId);
            data.AddExecution(memberDefinitionId, ExecutionProjectionStatuses.Failed, Day1);
            var otherWaiting = data.AddExecution(otherDefinitionId, ExecutionProjectionStatuses.Running, Day1);
            data.AddWait(otherWaiting, ExecutionWaitKind.EventWait);
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();

        // Act
        var counts = await repo.CountAsync(
            uow,
            ExecutionWaitingTestData.TenantId,
            principalId,
            new ExecutionCountFilter(null, null, null, null, null),
            CancellationToken.None);

        // Assert
        Assert.Equal(new ExecutionCounts(Total: 1, Running: 0, Completed: 0, Failed: 1, Cancelled: 0, Waiting: 0, NeedsAttention: 1), counts);
    }

    /// <summary>開始日時の期間は下限を含み、上限を含まない。</summary>
    [Fact]
    public async Task CountAsync_WhenStartedRangeIsSet_CountsFromInclusiveToExclusive()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        Guid principalId;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            var data = new ExecutionWaitingTestData(ctx);
            var projectId = Guid.NewGuid();
            principalId = data.AddProjectWithMember(projectId);
            var definitionId = Guid.NewGuid();
            data.AddDefinition(definitionId, projectId);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Completed, Day1);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Completed, Day2);
            data.AddExecution(definitionId, ExecutionProjectionStatuses.Completed, Day3);
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();

        // Act
        var counts = await repo.CountAsync(
            uow,
            ExecutionWaitingTestData.TenantId,
            principalId,
            new ExecutionCountFilter(null, null, null, StartedFrom: Day2, StartedTo: Day3),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, counts.Total);
    }

    /// <summary>対象が無いときはすべて 0 を返す。</summary>
    [Fact]
    public async Task CountAsync_WhenNoExecutions_ReturnsZeros()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        Guid principalId;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            principalId = new ExecutionWaitingTestData(ctx).AddProjectWithMember(Guid.NewGuid());
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();

        // Act
        var counts = await repo.CountAsync(
            uow,
            ExecutionWaitingTestData.TenantId,
            principalId,
            new ExecutionCountFilter(null, null, null, null, null),
            CancellationToken.None);

        // Assert
        Assert.Equal(new ExecutionCounts(0, 0, 0, 0, 0, 0, 0), counts);
    }

    /// <summary>最後の実行は開始日時のいちばん新しいルートの実行。物理子は選ばず、実行の無い定義はキーを持たない。</summary>
    [Fact]
    public async Task ListLatestRootByDefinitionIdsAsync_ReturnsNewestRoot_IgnoresChildAndEmptyDefinition()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var emptyDefinitionId = Guid.NewGuid();
        Guid newestRoot;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            var data = new ExecutionWaitingTestData(ctx);
            var projectId = Guid.NewGuid();
            data.AddProjectWithMember(projectId);
            data.AddDefinition(definitionId, projectId);
            data.AddDefinition(emptyDefinitionId, projectId);
            var olderRoot = data.AddExecution(definitionId, ExecutionProjectionStatuses.Completed, Day1, "exe-old");
            newestRoot = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day3, "exe-new");
            var child = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day5, "exe-child");
            data.AddBranch(olderRoot, child);
            data.AddWait(newestRoot, ExecutionWaitKind.EventWait);
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();

        // Act
        var latest = await repo.ListLatestRootByDefinitionIdsAsync(
            uow,
            ExecutionWaitingTestData.TenantId,
            [definitionId, emptyDefinitionId],
            CancellationToken.None);

        // Assert
        var entry = Assert.Single(latest);
        Assert.Equal(definitionId, entry.Key);
        Assert.Equal(newestRoot, entry.Value.ExecutionId);
        Assert.Equal("exe-new", entry.Value.DisplayId);
        Assert.True(entry.Value.Waiting);
    }

    /// <summary>子孫の物理子 Execution が EventWait を持つ Running の実行は待機中。</summary>
    [Fact]
    public async Task IsWaitingAsync_WhenGrandchildHasEventWait_ReturnsTrue()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        Guid root;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            var data = new ExecutionWaitingTestData(ctx);
            var projectId = Guid.NewGuid();
            data.AddProjectWithMember(projectId);
            var definitionId = Guid.NewGuid();
            data.AddDefinition(definitionId, projectId);
            root = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            var child = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            var grandchild = data.AddExecution(definitionId, ExecutionProjectionStatuses.Running, Day1);
            data.AddBranch(root, child);
            data.AddBranch(child, grandchild);
            data.AddWait(grandchild, ExecutionWaitKind.EventWait);
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();
        var execution = (await new ExecutionRepository().GetByIdAsync(uow, ExecutionWaitingTestData.TenantId, root, CancellationToken.None))!;

        // Act
        var waiting = await repo.IsWaitingAsync(uow, execution, CancellationToken.None);

        // Assert
        Assert.True(waiting);
    }

    /// <summary>終端した実行は Wait が残っていても待機中にしない。</summary>
    [Fact]
    public async Task IsWaitingAsync_WhenTerminal_ReturnsFalse()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        Guid failed;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            var data = new ExecutionWaitingTestData(ctx);
            var projectId = Guid.NewGuid();
            data.AddProjectWithMember(projectId);
            var definitionId = Guid.NewGuid();
            data.AddDefinition(definitionId, projectId);
            failed = data.AddExecution(definitionId, ExecutionProjectionStatuses.Failed, Day1);
            data.AddWait(failed, ExecutionWaitKind.EventWait);
            await ctx.SaveChangesAsync();
        }

        var repo = new ExecutionSummaryRepository();
        await using var uow = await new TestCoreUnitOfWorkFactory(db.Factory).CreateAsync();
        var execution = (await new ExecutionRepository().GetByIdAsync(uow, ExecutionWaitingTestData.TenantId, failed, CancellationToken.None))!;

        // Act
        var waiting = await repo.IsWaitingAsync(uow, execution, CancellationToken.None);

        // Assert
        Assert.False(waiting);
    }
}
