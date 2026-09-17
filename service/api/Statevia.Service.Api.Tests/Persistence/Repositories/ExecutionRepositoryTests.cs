using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

public sealed class ExecutionRepositoryTests
{
    /// <summary>
    /// 未存在の識別子では空値を返す。
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var res = await repo.GetByIdAsync(uow, TestTenantIds.T1TenantId, Guid.NewGuid(), default);
        // Assert
        Assert.Null(res);
    }

    /// <summary>GetByExecutionIdAsync はテナントフィルタなしで execution 行を返す。</summary>
    [Fact]
    public async Task GetByExecutionIdAsync_ReturnsRow_WithoutTenantFilter()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var executionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.OtherTenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var row = await repo.GetByExecutionIdAsync(uow, executionId, CancellationToken.None);

        // Assert
        Assert.NotNull(row);
        Assert.Equal(TestTenantIds.OtherTenantId, row!.TenantId);
    }

    /// <summary>
    /// 開始時刻の降順で並べて表示用識別子を結合する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_OrdersByUpdatedAtDesc_AndUsesLeftJoin()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = TestTenantIds.T1TenantId;
        var defId = Guid.NewGuid();
        var wfId1 = Guid.NewGuid();
        var wfId2 = Guid.NewGuid();

        var t1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.AddRange(
                new ExecutionRow
                {
                    ExecutionId = wfId1,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = t1,
                    UpdatedAt = t1,
                    CancelRequested = false,
                    RestartLost = false
                },
                new ExecutionRow
                {
                    ExecutionId = wfId2,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Completed",
                    StartedAt = t2,
                    UpdatedAt = t2,
                    CancelRequested = false,
                    RestartLost = false
                });

            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "execution",
                DisplayId = "WF-DISP-2",
                ResourceId = wfId2,
                CreatedAt = t2
            });

            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        await using var uow = await uowFactory.CreateAsync();
        var (_, items) = await repo.ListWithDisplayIdsPageAsync(
            uow, tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: null,
                NameContains: null),
            default);
        Assert.Equal(2, items.Count);
        Assert.Equal("WF-DISP-2", items[0].DisplayId);
        Assert.Null(items[1].DisplayId);
        Assert.Equal(wfId2, items[0].Execution.ExecutionId);
    }

    /// <summary>
    /// ステータス条件で絞り込む の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByStatusFilter()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = TestTenantIds.T1TenantId;
        var defId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.AddRange(
                new ExecutionRow
                {
                    ExecutionId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                },
                new ExecutionRow
                {
                    ExecutionId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Completed",
                    StartedAt = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                });

            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow, tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: "Completed",
                DefinitionIdFilter: null,
                NameContains: null),
            default);
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("Completed", items[0].Execution.Status);
    }

    /// <summary>definitionId で 1 件に絞り込む。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByDefinitionId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var tenantId = TestTenantIds.T1TenantId;
        var def1 = Guid.NewGuid();
        var def2 = Guid.NewGuid();
        var wf1 = Guid.NewGuid();
        var wf2 = Guid.NewGuid();
        var started = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.AddRange(
                new ExecutionRow
                {
                    ExecutionId = wf1,
                    TenantId = tenantId,
                    DefinitionId = def1,
                    Status = "Running",
                    StartedAt = started,
                    UpdatedAt = started,
                    CancelRequested = false,
                    RestartLost = false
                },
                new ExecutionRow
                {
                    ExecutionId = wf2,
                    TenantId = tenantId,
                    DefinitionId = def2,
                    Status = "Running",
                    StartedAt = started.AddDays(1),
                    UpdatedAt = started.AddDays(1),
                    CancelRequested = false,
                    RestartLost = false
                });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow, tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: def1,
                NameContains: null),
            default);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(wf1, items[0].Execution.ExecutionId);
    }

    /// <summary>name に displayId の部分一致のワークフローのみ含める。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByNameDisplayIdContains()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var tenantId = TestTenantIds.T1TenantId;
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();
        var started = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(
                new ExecutionRow
                {
                    ExecutionId = wfId,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = started,
                    UpdatedAt = started,
                    CancelRequested = false,
                    RestartLost = false
                });
            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "execution",
                DisplayId = "acme-orders-99",
                ResourceId = wfId,
                CreatedAt = started
            });
            var otherWf = Guid.NewGuid();
            ctx.Executions.Add(
                new ExecutionRow
                {
                    ExecutionId = otherWf,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = started.AddDays(1),
                    UpdatedAt = started.AddDays(1),
                    CancelRequested = false,
                    RestartLost = false
                });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow, tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: null,
                NameContains: "orders"),
            default);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(wfId, items[0].Execution.ExecutionId);
    }

    /// <summary>
    /// 両方の行を永続化する の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task AddExecutionAndSnapshotAsync_PersistsBoth()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = TestTenantIds.T1TenantId;
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        var execution = new ExecutionRow
        {
            ExecutionId = wfId,
            TenantId = tenantId,
            DefinitionId = defId,
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false
        };
        var snapshot = new ExecutionGraphSnapshotRow
        {
            ExecutionId = wfId,
            GraphJson = "{\"nodes\":[]}",
            UpdatedAt = DateTime.UtcNow
        };

        await using var uow = await uowFactory.CreateAsync();
        await repo.AddExecutionAndSnapshotAsync(uow, execution, snapshot, default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        // Assert
        Assert.Equal(1, await ctx.Executions.CountAsync(x => x.ExecutionId == wfId));
        Assert.Equal(1, await ctx.ExecutionGraphSnapshots.CountAsync(x => x.ExecutionId == wfId));
    }

    /// <summary>
    /// スナップショットがないとき空値を返す。
    /// </summary>
    [Fact]
    public async Task GetSnapshotByExecutionIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var wfId = Guid.NewGuid();
        // Assert
        await using var uow = await uowFactory.CreateAsync();
        var snapshot = await repo.GetSnapshotByExecutionIdAsync(uow, wfId, default);
        Assert.Null(snapshot);
    }

    /// <summary>
    /// 取消要求指定が空値なら既存値を維持して更新する。
    /// </summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_UpdatesStatusAndGraphJson_AndKeepsCancelRequested_WhenNull()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var wfId = Guid.NewGuid();
        await SeedExecutionForSnapshotUpdateAsync(
            db,
            wfId,
            graphJson: "{\"nodes\":[1]}");

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", null, "{\"nodes\":[2]}", default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        var w = await verify.Executions.FirstAsync(x => x.ExecutionId == wfId);
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.ExecutionId == wfId);

        // Assert
        Assert.Equal("Completed", w.Status);
        Assert.False(w.CancelRequested);
        Assert.Equal("{\"nodes\":[2]}", g.GraphJson);
    }

    /// <summary>graph JSON が未変化なら snapshot 行を UPDATE しない。</summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_WhenGraphUnchanged_DoesNotTouchSnapshotUpdatedAt()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var wfId = Guid.NewGuid();
        var originalGraph = "{\"nodes\":[1]}";
        var originalSnapshotAt = new DateTime(2026, 5, 16, 11, 0, 0, DateTimeKind.Utc);
        await SeedExecutionForSnapshotUpdateAsync(
            db,
            wfId,
            graphJson: originalGraph,
            snapshotUpdatedAt: originalSnapshotAt);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", null, originalGraph, default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        var w = await verify.Executions.FirstAsync(x => x.ExecutionId == wfId);
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.ExecutionId == wfId);

        // Assert
        Assert.Equal("Completed", w.Status);
        Assert.Equal(originalGraph, g.GraphJson);
        Assert.Equal(originalSnapshotAt, g.UpdatedAt);
    }

    /// <summary>
    /// ワークフローが存在しない の場合は スナップショット更新は継続する。
    /// </summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_WhenExecutionMissing_StillUpdatesSnapshot()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var wfId = Guid.NewGuid();
        await using (var ctx = db.Factory.CreateDbContext())
        {
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = wfId,
                GraphJson = "{\"nodes\":[1]}",
                UpdatedAt = new DateTime(2026, 5, 16, 11, 0, 0, DateTimeKind.Utc)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", true, "{\"nodes\":[2]}", default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.ExecutionId == wfId);

        // Assert
        Assert.Equal("{\"nodes\":[2]}", g.GraphJson);
    }

    /// <summary>
    /// スナップショットが存在しない の場合は 実行行のみ更新する。
    /// </summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_WhenSnapshotMissing_StillUpdatesExecutionOnly()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var wfId = Guid.NewGuid();
        await SeedExecutionForSnapshotUpdateAsync(db, wfId, graphJson: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", true, "{\"nodes\":[2]}", default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        var w = await verify.Executions.FirstAsync(x => x.ExecutionId == wfId);
        var snapshotCount = await verify.ExecutionGraphSnapshots.CountAsync(x => x.ExecutionId == wfId);

        // Assert
        Assert.Equal("Completed", w.Status);
        Assert.True(w.CancelRequested);
        Assert.Equal(0, snapshotCount);
    }

    /// <summary>
    /// ExecuteUpdate 検証用に、定義 FK 付きの実行行と任意の snapshot を投入する。
    /// </summary>
    private static async Task SeedExecutionForSnapshotUpdateAsync(
        SqliteTestDatabase db,
        Guid executionId,
        string? graphJson,
        DateTime? snapshotUpdatedAt = null)
    {
        var tenantId = TestTenantIds.T1TenantId;
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = new DateTime(2026, 5, 16, 12, 0, 0, DateTimeKind.Utc);

        await using var ctx = db.Factory.CreateDbContext();
        ProjectTestData.AddDefaultProject(ctx, tenantId, "t1", projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            ctx,
            tenantId,
            definitionId,
            "wf-exec-repo",
            projectId,
            versionId: versionId);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = versionId,
            Status = "Running",
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        });
        if (graphJson is not null)
        {
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = executionId,
                GraphJson = graphJson,
                UpdatedAt = snapshotUpdatedAt ?? now
            });
        }

        await ctx.SaveChangesAsync(CancellationToken.None);
    }
}

