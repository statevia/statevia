using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionWaitRepository"/> の永続化シナリオ。</summary>
public sealed class ExecutionWaitRepositoryTests
{
    /// <summary>ReplaceWaitsAsync は不在 wait を削除し、指定 wait を upsert する。</summary>
    [Fact]
    public async Task ReplaceWaitsAsync_ReplacesRowsForExecution()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
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
                RestartLost = false
            });
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "stale",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["old"],
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        var desired = new[]
        {
            new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "wait-node",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["approve", "reject"],
                CreatedAt = now
            }
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.ReplaceWaitsAsync(uow, executionId, desired, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var rows = await verify.ExecutionWaits.Where(x => x.ExecutionId == executionId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal("wait-node", rows[0].NodeId);
        Assert.Equal(["approve", "reject"], rows[0].AllowedEvents);
    }

    /// <summary>DeleteByNodeIdAsync は node_id 一致行のみ削除する。</summary>
    [Fact]
    public async Task DeleteByNodeIdAsync_RemovesMatchingRows()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
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
                RestartLost = false
            });
            ctx.ExecutionWaits.AddRange(
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n1",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["approve"],
                    CreatedAt = now
                },
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n2",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["other"],
                    CreatedAt = now
                });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.DeleteByNodeIdAsync(uow, executionId, "n1", CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var nodeIds = await verify.ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .Select(x => x.NodeId)
            .ToListAsync();
        Assert.Single(nodeIds);
        Assert.Equal("n2", nodeIds[0]);
    }

    /// <summary>ListByExecutionIdAsync は created_at 順で wait 行を返す。</summary>
    [Fact]
    public async Task ListByExecutionIdAsync_ReturnsRowsOrdered()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var t1 = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 5, 26, 0, 0, 1, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = t1,
                UpdatedAt = t1,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionWaits.AddRange(
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n2",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["second"],
                    CreatedAt = t2
                },
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n1",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["first"],
                    CreatedAt = t1
                });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var rows = await repo.ListByExecutionIdAsync(uow, executionId, CancellationToken.None);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(["first"], rows[0].AllowedEvents);
        Assert.Equal(["second"], rows[1].AllowedEvents);
    }

    /// <summary>ReplaceWaitsAsync は親 wait と同時に子購読を全置換する。</summary>
    [Fact]
    public async Task ReplaceWaitsAsync_ReplacesSubscriptionsForExecution()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(CreateExecution(executionId, now));
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "old-wait",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["statevia.event.subscribe.0"],
                CreatedAt = now
            });
            ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = Guid.NewGuid(),
                ExecutionId = executionId,
                NodeId = "old-wait",
                Topic = "stale.topic",
                CorrelationKey = "",
                ResumeEventName = "statevia.event.subscribe.0",
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        var desired = new[]
        {
            new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "wait-node",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["statevia.event.subscribe.0", "statevia.event.subscribe.1"],
                CreatedAt = now,
                Subscriptions =
                [
                    new ExecutionWaitSubscriptionRow
                    {
                        SubscriptionId = Guid.NewGuid(),
                        ExecutionId = executionId,
                        NodeId = "wait-node",
                        Topic = "inventory.received",
                        CorrelationKey = "SKU-1",
                        ResumeEventName = "statevia.event.subscribe.0",
                        CreatedAt = now
                    },
                    new ExecutionWaitSubscriptionRow
                    {
                        SubscriptionId = Guid.NewGuid(),
                        ExecutionId = executionId,
                        NodeId = "wait-node",
                        Topic = "inventory.returned",
                        CorrelationKey = "",
                        ResumeEventName = "statevia.event.subscribe.1",
                        CreatedAt = now
                    }
                ]
            }
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.ReplaceWaitsAsync(uow, executionId, desired, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var waits = await verify.ExecutionWaits.Where(x => x.ExecutionId == executionId).ToListAsync();
        var subscriptions = await verify.ExecutionWaitSubscriptions
            .Where(x => x.ExecutionId == executionId)
            .OrderBy(x => x.ResumeEventName)
            .ToListAsync();
        Assert.Single(waits);
        Assert.Equal("wait-node", waits[0].NodeId);
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal("inventory.received", subscriptions[0].Topic);
        Assert.Equal("SKU-1", subscriptions[0].CorrelationKey);
        Assert.Equal("inventory.returned", subscriptions[1].Topic);
        Assert.Equal("", subscriptions[1].CorrelationKey);
    }

    /// <summary>topic+空 key は厳密一致のみ返す。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_MatchesEmptyKeyExactly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db,
            executionId,
            "wait-a",
            "orders.updated",
            "",
            "statevia.event.subscribe.0",
            now);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var emptyKeyMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "", Guid.NewGuid(), CancellationToken.None);
        var keyedMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "order-1", Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Single(emptyKeyMatches);
        Assert.Equal(executionId, emptyKeyMatches[0].ExecutionId);
        Assert.Equal("statevia.event.subscribe.0", emptyKeyMatches[0].ResumeEventName);
        Assert.Empty(keyedMatches);
    }

    /// <summary>候補はテナント内の一意な topic / key だけを昇順で返す。子実行の購読を含み、他テナントは含まない。</summary>
    [Fact]
    public async Task ListDistinctSubscriptionCandidatesAsync_ReturnsUniqueTenantPairsIncludingChild()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var now = DateTime.UtcNow;
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Reader);
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var otherTenantExecutionId = Guid.NewGuid();
        await SeedSubscriptionAsync(db, parentId, "parent-wait", "orders.updated", "", "statevia.event.subscribe.0", now, definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(db, childId, "child-wait", "orders.updated", "", "statevia.event.subscribe.0", now.AddMinutes(1), definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(db, childId, "child-wait-b", "zeta.ready", "box", "statevia.event.subscribe.1", now.AddMinutes(2), definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(
            db,
            otherTenantExecutionId,
            "other-wait",
            "secret.topic",
            "hidden",
            "statevia.event.subscribe.0",
            now,
            TestTenantIds.OtherTenantId,
            member.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var candidates = await repo.ListDistinctSubscriptionCandidatesAsync(
            uow,
            TestTenantIds.T1TenantId,
            member.PrincipalId,
            limit: 10,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            [
                new EventSubscriptionCandidate("orders.updated", ""),
                new EventSubscriptionCandidate("zeta.ready", "box"),
            ],
            candidates);
    }

    /// <summary>上限を超える候補は topic / key 昇順の先頭だけを返す。</summary>
    [Fact]
    public async Task ListDistinctSubscriptionCandidatesAsync_WhenOverLimit_ReturnsLeadingPairs()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var now = DateTime.UtcNow;
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Reader);
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(db, executionId, "wait-a", "alpha.ready", "sku", "statevia.event.subscribe.0", now, definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(db, executionId, "wait-b", "orders.updated", "", "statevia.event.subscribe.1", now, definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(db, executionId, "wait-c", "zeta.ready", "box", "statevia.event.subscribe.2", now, definitionId: member.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var candidates = await repo.ListDistinctSubscriptionCandidatesAsync(
            uow,
            TestTenantIds.T1TenantId,
            member.PrincipalId,
            limit: 2,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            [
                new EventSubscriptionCandidate("alpha.ready", "sku"),
                new EventSubscriptionCandidate("orders.updated", ""),
            ],
            candidates);
    }

    /// <summary>topic+非空 key は厳密一致のみ返す。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_MatchesNonEmptyKeyExactly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db,
            executionId,
            "wait-a",
            "orders.updated",
            "order-1",
            "statevia.event.subscribe.0",
            now);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var keyedMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "order-1", Guid.NewGuid(), CancellationToken.None);
        var emptyKeyMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "", Guid.NewGuid(), CancellationToken.None);
        var otherKeyMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "order-2", Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Single(keyedMatches);
        Assert.Equal("wait-a", keyedMatches[0].NodeId);
        Assert.Empty(emptyKeyMatches);
        Assert.Empty(otherKeyMatches);
    }

    /// <summary>同一 topic+key の複数購読をすべて返す。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_ReturnsMultipleMatches()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var execution1 = Guid.NewGuid();
        var execution2 = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db, execution1, "wait-a", "inventory.received", "SKU-1", "statevia.event.subscribe.0", now);
        await SeedSubscriptionAsync(
            db, execution2, "wait-b", "inventory.received", "SKU-1", "statevia.event.subscribe.0", now.AddSeconds(1));

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "inventory.received", "SKU-1", Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.ExecutionId == execution1 && m.NodeId == "wait-a");
        Assert.Contains(matches, m => m.ExecutionId == execution2 && m.NodeId == "wait-b");
    }

    /// <summary>DisabledTenantQueryFilterOptions でも他テナントの同一 topic/key は返さない。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_WhenOtherTenant_DoesNotMatch()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var t1Execution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db, t1Execution, "t1-wait", "shared.topic", "k1", "statevia.event.subscribe.0", now);
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(CreateExecution(otherExecution, now, TestTenantIds.OtherTenantId));
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = otherExecution,
                NodeId = "other-wait",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["statevia.event.subscribe.0"],
                CreatedAt = now
            });
            ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = Guid.NewGuid(),
                ExecutionId = otherExecution,
                NodeId = "other-wait",
                Topic = "shared.topic",
                CorrelationKey = "k1",
                ResumeEventName = "statevia.event.subscribe.0",
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }
        await using (var unfiltered = new CoreDbContext(
            db.Options,
            NullTenantContextAccessor.Instance,
            DisabledTenantQueryFilterOptions.Instance))
        {
            Assert.Equal(2, await unfiltered.Executions.CountAsync());
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "shared.topic", "k1", Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Single(matches);
        Assert.Equal(t1Execution, matches[0].ExecutionId);
    }

    /// <summary>実行者として所属する Project の定義の購読だけが範囲に入り、所属の無い Project の購読は範囲の外になる。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_MarksOnlyMemberProjectSubscriptionsInScope()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var now = DateTime.UtcNow;
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var otherProject = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var memberExecution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        await SeedSubscriptionAsync(db, memberExecution, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", now, definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(db, otherExecution, "wait-b", "shared.topic", "k1", "statevia.event.subscribe.0", now.AddSeconds(1), definitionId: otherProject.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "shared.topic", "k1", member.PrincipalId, CancellationToken.None);

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.True(matches.Single(m => m.ExecutionId == memberExecution).InScope);
        Assert.False(matches.Single(m => m.ExecutionId == otherExecution).InScope);
    }

    /// <summary>閲覧者だけの所属では、その Project の購読も範囲の外になる（集合配送は実行者以上が要る）。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_WhenReaderOnly_MarksOutOfScope()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var reader = await SeedProjectMemberAsync(db, ProjectAccessRole.Reader);
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(db, executionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", DateTime.UtcNow, definitionId: reader.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "shared.topic", "k1", reader.PrincipalId, CancellationToken.None);

        // Assert
        Assert.False(Assert.Single(matches).InScope);
    }

    /// <summary>操作できる定義が定義 X だけのとき、同じ Project の定義 Y の購読は範囲の外になる。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_WhenDefinitionNotGranted_MarksOutOfScope()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var now = DateTime.UtcNow;
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var otherDefinitionId = await SeedDefinitionAsync(db, member.ProjectId);
        await GrantDefinitionAsync(db, member.PrincipalId, member.DefinitionId);
        var grantedExecution = Guid.NewGuid();
        var notGrantedExecution = Guid.NewGuid();
        await SeedSubscriptionAsync(db, grantedExecution, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", now, definitionId: member.DefinitionId);
        await SeedSubscriptionAsync(db, notGrantedExecution, "wait-b", "shared.topic", "k1", "statevia.event.subscribe.0", now.AddSeconds(1), definitionId: otherDefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "shared.topic", "k1", member.PrincipalId, CancellationToken.None);

        // Assert
        Assert.True(matches.Single(m => m.ExecutionId == grantedExecution).InScope);
        Assert.False(matches.Single(m => m.ExecutionId == notGrantedExecution).InScope);
    }

    /// <summary>
    /// どの Project にも所属しない Principal では、すべての購読が範囲の外になる。
    /// テナント管理者もリポジトリでは特別扱いしないので、所属が無ければ同じ結果になる。
    /// </summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_WhenPrincipalHasNoMembership_MarksAllOutOfScope()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Admin);
        var outsiderPrincipalId = await SeedPrincipalAsync(db);
        await SeedSubscriptionAsync(db, Guid.NewGuid(), "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", DateTime.UtcNow, definitionId: member.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "shared.topic", "k1", outsiderPrincipalId, CancellationToken.None);

        // Assert
        Assert.False(Assert.Single(matches).InScope);
    }

    /// <summary>
    /// 並列分岐の物理子の購読は、親と同じ定義（Project）で判定される。
    /// 子の実行の行は親の definition_id を写すので、子の定義で判定すればルートで判定したのと同じになる。
    /// </summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_WhenSubscriptionOnForkChild_JudgesByParentDefinition()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var now = DateTime.UtcNow;
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var outsider = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(CreateExecution(parentId, now, definitionId: member.DefinitionId));
            ctx.ExecutionBranches.Add(new ExecutionBranchRow
            {
                ParentExecutionId = parentId,
                ExecutionId = childId,
                ForkNodeId = "fork",
                JoinState = "join",
                BranchState = "branch-a",
                Status = ExecutionBranchStatuses.Running,
                CreatedAt = now,
                UpdatedAt = now
            });
            await ctx.SaveChangesAsync();
        }
        await SeedSubscriptionAsync(db, childId, "child-wait", "approval.needed", "req-1", "statevia.event.subscribe.0", now, definitionId: member.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var memberMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "approval.needed", "req-1", member.PrincipalId, CancellationToken.None);
        var outsiderMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "approval.needed", "req-1", outsider.PrincipalId, CancellationToken.None);

        // Assert
        var memberMatch = Assert.Single(memberMatches);
        Assert.Equal(childId, memberMatch.ExecutionId);
        Assert.True(memberMatch.InScope);
        Assert.False(Assert.Single(outsiderMatches).InScope);
    }

    /// <summary>候補は閲覧者として所属する範囲だけから作り、範囲の外の topic / key は上限の枠を使わない。</summary>
    [Fact]
    public async Task ListDistinctSubscriptionCandidatesAsync_ExcludesOutOfScopePairsBeforeLimit()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var now = DateTime.UtcNow;
        var reader = await SeedProjectMemberAsync(db, ProjectAccessRole.Reader);
        var otherProject = await SeedProjectMemberAsync(db, ProjectAccessRole.Reader);
        var otherExecution = Guid.NewGuid();
        var readerExecution = Guid.NewGuid();
        await SeedSubscriptionAsync(db, otherExecution, "wait-a", "aaa.secret", "order-1", "statevia.event.subscribe.0", now, definitionId: otherProject.DefinitionId);
        await SeedSubscriptionAsync(db, otherExecution, "wait-b", "aab.secret", "order-2", "statevia.event.subscribe.1", now, definitionId: otherProject.DefinitionId);
        await SeedSubscriptionAsync(db, readerExecution, "wait-c", "zeta.ready", "box", "statevia.event.subscribe.0", now, definitionId: reader.DefinitionId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var candidates = await repo.ListDistinctSubscriptionCandidatesAsync(
            uow,
            TestTenantIds.T1TenantId,
            reader.PrincipalId,
            limit: 1,
            CancellationToken.None);

        // Assert
        Assert.Equal([new EventSubscriptionCandidate("zeta.ready", "box")], candidates);
    }

    private sealed record ProjectMember(Guid ProjectId, Guid DefinitionId, Guid PrincipalId);

    /// <summary>T1 に Project・定義・その Project に所属する User を 1 つずつ作る。</summary>
    private static async Task<ProjectMember> SeedProjectMemberAsync(InMemoryTestDatabase db, ProjectAccessRole role)
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
        var principalId = ProjectTestData.AddMemberPrincipal(ctx, TestTenantIds.T1TenantId, projectId, role);
        await ctx.SaveChangesAsync();
        return new ProjectMember(projectId, definitionId, principalId);
    }

    /// <summary>既存の Project に定義を 1 つ足す。</summary>
    private static async Task<Guid> SeedDefinitionAsync(InMemoryTestDatabase db, Guid projectId)
    {
        var definitionId = Guid.NewGuid();
        await using var ctx = new CoreDbContext(db.Options);
        DefinitionTestData.AddDefinitionWithVersion(ctx, TestTenantIds.T1TenantId, definitionId, $"def-{definitionId:N}", projectId);
        await ctx.SaveChangesAsync();
        return definitionId;
    }

    /// <summary>どの Project にも所属しない User を作る。</summary>
    private static async Task<Guid> SeedPrincipalAsync(InMemoryTestDatabase db)
    {
        var principalId = Guid.NewGuid();
        await using var ctx = new CoreDbContext(db.Options);
        ProjectTestData.AddPrincipal(ctx, TestTenantIds.T1TenantId, principalId);
        await ctx.SaveChangesAsync();
        return principalId;
    }

    /// <summary>Principal の操作できる定義に、定義を 1 つ足す。</summary>
    private static async Task GrantDefinitionAsync(InMemoryTestDatabase db, Guid principalId, Guid definitionId)
    {
        await using var ctx = new CoreDbContext(db.Options);
        ctx.PrincipalResourceGrants.Add(new PrincipalResourceGrantRow
        {
            TenantId = TestTenantIds.T1TenantId,
            PrincipalId = principalId,
            ResourceKind = PrincipalResourceGrantKinds.Definition,
            ResourceId = definitionId,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    private static ExecutionRow CreateExecution(
        Guid executionId,
        DateTime now,
        Guid? tenantId = null,
        Guid? definitionId = null) =>
        new()
        {
            ExecutionId = executionId,
            TenantId = tenantId ?? TestTenantIds.T1TenantId,
            DefinitionId = definitionId ?? Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        };

    private static async Task SeedSubscriptionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        string nodeId,
        string topic,
        string correlationKey,
        string resumeEventName,
        DateTime createdAt,
        Guid? tenantId = null,
        Guid? definitionId = null)
    {
        await using var ctx = new CoreDbContext(db.Options);
        if (!await ctx.Executions.AnyAsync(x => x.ExecutionId == executionId))
            ctx.Executions.Add(CreateExecution(executionId, createdAt, tenantId, definitionId));

        if (!await ctx.ExecutionWaits.AnyAsync(x => x.ExecutionId == executionId && x.NodeId == nodeId))
        {
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = nodeId,
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = [resumeEventName],
                CreatedAt = createdAt
            });
        }

        ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
        {
            SubscriptionId = Guid.NewGuid(),
            ExecutionId = executionId,
            NodeId = nodeId,
            Topic = topic,
            CorrelationKey = correlationKey,
            ResumeEventName = resumeEventName,
            CreatedAt = createdAt
        });
        await ctx.SaveChangesAsync();
    }
}
