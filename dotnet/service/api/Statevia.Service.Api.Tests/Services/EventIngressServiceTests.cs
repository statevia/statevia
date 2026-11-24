using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="EventIngressService"/> の集合配送シナリオ。</summary>
public sealed class EventIngressServiceTests
{
    /// <summary>一致購読ごとに Resume ワークを enqueue する。</summary>
    [Fact]
    public async Task PublishAsync_EnqueuesResumeForEachMatch()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(db, executionId, member.DefinitionId, "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await harness.Service.PublishAsync("inventory.received", "SKU-1", CancellationToken.None);

        // Assert
        Assert.Single(harness.WorkQueue.Items);
        var item = harness.WorkQueue.Items[0];
        Assert.Equal(executionId, item.ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Resume, item.Kind);
        var payload = JsonSerializer.Deserialize<ExecutionResumeWorkItemPayload>(
            item.Payload,
            ExecutionWorkItemPayloadJson.Options);
        Assert.NotNull(payload);
        Assert.Equal(ExecutionResumeWorkItemModes.Event, payload.Mode);
        Assert.Equal("WaitNode", payload.NodeId);
        Assert.Equal("statevia.event.subscribe.0", payload.EventName);
    }

    /// <summary>key 省略・空白は空文字として照合する。</summary>
    [Fact]
    public async Task PublishAsync_NormalizesBlankKeyToEmptyString()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(db, executionId, member.DefinitionId, "WaitNode", "orders.updated", "", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await harness.Service.PublishAsync("orders.updated", "   ", CancellationToken.None);

        // Assert
        Assert.Single(harness.WorkQueue.Items);
        Assert.Equal(executionId, harness.WorkQueue.Items[0].ExecutionId);
    }

    /// <summary>一致 0 件でも例外なく完了し enqueue しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenNoMatch_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));

        // Act
        await harness.Service.PublishAsync("missing.topic", "", CancellationToken.None);

        // Assert
        Assert.Empty(harness.WorkQueue.Items);
    }

    /// <summary>購読は子 execution_id に紐づき、親 ID へ Resume しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenSubscriptionOnChild_EnqueuesResumeForChildNotParent()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, parentId, member.DefinitionId, now, TestTenantIds.T1TenantId);
        await SeedSubscriptionAsync(db, childId, member.DefinitionId, "ChildWait", "approval.needed", "req-1", "Approve", now);

        // Act
        await harness.Service.PublishAsync("approval.needed", "req-1", CancellationToken.None);

        // Assert
        Assert.Single(harness.WorkQueue.Items);
        Assert.Equal(childId, harness.WorkQueue.Items[0].ExecutionId);
        Assert.NotEqual(parentId, harness.WorkQueue.Items[0].ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Resume, harness.WorkQueue.Items[0].Kind);
    }

    /// <summary>購読が空 key のとき key 付き配信は一致しない。</summary>
    [Fact]
    public async Task PublishAsync_EmptyKeySubscription_DoesNotMatchNonEmptyKey()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        await SeedSubscriptionAsync(db, Guid.NewGuid(), member.DefinitionId, "WaitNode", "orders.updated", "", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await harness.Service.PublishAsync("orders.updated", "order-1", CancellationToken.None);

        // Assert
        Assert.Empty(harness.WorkQueue.Items);
    }

    /// <summary>write なしは 403 相当で enqueue しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenWritePermissionMissing_ThrowsForbiddenAndDoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId), runtimeAuth: new DenyingRuntimePermissionAuthorization());
        await SeedSubscriptionAsync(db, Guid.NewGuid(), member.DefinitionId, "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => harness.Service.PublishAsync("inventory.received", "SKU-1", CancellationToken.None));

        // Assert
        Assert.Equal("PERMISSION_DENIED", ex.Code);
        Assert.Empty(harness.WorkQueue.Items);
    }

    /// <summary>テナント未解決なら 0 件として enqueue しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenTenantUnresolved_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, NullTenantContextAccessor.Instance);
        await SeedSubscriptionAsync(db, Guid.NewGuid(), member.DefinitionId, "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await harness.Service.PublishAsync("inventory.received", "SKU-1", CancellationToken.None);

        // Assert
        Assert.Empty(harness.WorkQueue.Items);
    }

    /// <summary>
    /// DisabledTenantQueryFilterOptions でも他テナント購読は明示 TenantId で除外する。
    /// 他テナントの実行にも範囲に入る定義 ID を持たせ、範囲ではなくテナントの条件で除かれることを確かめる。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenQueryFilterDisabled_DoesNotEnqueueOtherTenant()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        var t1Execution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(db, t1Execution, member.DefinitionId, "t1-wait", "shared.topic", "k1", "statevia.event.subscribe.0", now);
        await SeedSubscriptionAsync(db, otherExecution, member.DefinitionId, "other-wait", "shared.topic", "k1", "statevia.event.subscribe.0", now, TestTenantIds.OtherTenantId);
        await using (var unfiltered = new CoreDbContext(
            db.Options,
            NullTenantContextAccessor.Instance,
            DisabledTenantQueryFilterOptions.Instance))
        {
            Assert.Equal(2, await unfiltered.Executions.CountAsync());
        }

        // Act
        await harness.Service.PublishAsync("shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Single(harness.WorkQueue.Items);
        Assert.Equal(t1Execution, harness.WorkQueue.Items[0].ExecutionId);
        Assert.DoesNotContain(harness.WorkQueue.Items, item => item.ExecutionId == otherExecution);
    }

    /// <summary>呼び出し元が所属しない Project の購読は Resume を積まず、除いた件数を情報ログに残す。</summary>
    [Fact]
    public async Task PublishAsync_WhenSubscriptionOutOfScope_SkipsItAndLogsCount()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var otherProject = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        var memberExecution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(db, memberExecution, member.DefinitionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", now);
        await SeedSubscriptionAsync(db, otherExecution, otherProject.DefinitionId, "wait-b", "shared.topic", "k1", "statevia.event.subscribe.0", now.AddSeconds(1));

        // Act
        await harness.Service.PublishAsync("shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Equal(memberExecution, Assert.Single(harness.WorkQueue.Items).ExecutionId);
        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(30501, entry.EventId);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Origin=http", entry.Message, StringComparison.Ordinal);
        Assert.Contains("MatchedCount=2", entry.Message, StringComparison.Ordinal);
        Assert.Contains("OutOfScopeCount=1", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("shared.topic", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>範囲の外の購読が無ければ、除いた件数のログは出さない。</summary>
    [Fact]
    public async Task PublishAsync_WhenAllSubscriptionsInScope_DoesNotLog()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, CallerContext(member.PrincipalId));
        await SeedSubscriptionAsync(db, Guid.NewGuid(), member.DefinitionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await harness.Service.PublishAsync("shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Single(harness.WorkQueue.Items);
        Assert.Empty(harness.Logger.Entries);
    }

    /// <summary>テナント文脈に Principal が無いと 401 で、範囲を絞らずに照合することはしない。</summary>
    [Fact]
    public async Task PublishAsync_WhenPrincipalMissing_ThrowsUnauthorizedAndDoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var member = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, WorkerContext());
        await SeedSubscriptionAsync(db, Guid.NewGuid(), member.DefinitionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => harness.Service.PublishAsync("shared.topic", "k1", CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", ex.Code);
        Assert.Empty(harness.WorkQueue.Items);
    }

    /// <summary>event.publish は、発行した実行の Snapshot の開始者の範囲で照合する（Worker 文脈に Principal が無くてもよい）。</summary>
    [Fact]
    public async Task PublishFromExecutionAsync_JudgesByStarterOfPublishingExecution()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var starter = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var otherProject = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, WorkerContext(), new FixedUserPrincipalDataAccess(starter.PrincipalId, TestTenantIds.T1TenantId));
        var now = DateTime.UtcNow;
        var publishingExecution = Guid.NewGuid();
        await SeedExecutionAsync(db, publishingExecution, starter.DefinitionId, now, TestTenantIds.T1TenantId, starter.PrincipalId);
        var starterWait = Guid.NewGuid();
        var otherWait = Guid.NewGuid();
        await SeedSubscriptionAsync(db, starterWait, starter.DefinitionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", now);
        await SeedSubscriptionAsync(db, otherWait, otherProject.DefinitionId, "wait-b", "shared.topic", "k1", "statevia.event.subscribe.0", now.AddSeconds(1));

        // Act
        await harness.Service.PublishFromExecutionAsync(publishingExecution, "shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Equal(starterWait, Assert.Single(harness.WorkQueue.Items).ExecutionId);
        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(30501, entry.EventId);
        Assert.Contains("Origin=execution", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>開始者が無効化されていれば照合せず、警告ログを残して成功する。</summary>
    [Fact]
    public async Task PublishFromExecutionAsync_WhenStarterInactive_SkipsAndLogsWarning()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var starter = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, WorkerContext(), new InactivePrincipalDataAccess());
        var now = DateTime.UtcNow;
        var publishingExecution = Guid.NewGuid();
        await SeedExecutionAsync(db, publishingExecution, starter.DefinitionId, now, TestTenantIds.T1TenantId, starter.PrincipalId);
        await SeedSubscriptionAsync(db, Guid.NewGuid(), starter.DefinitionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", now);

        // Act
        await harness.Service.PublishFromExecutionAsync(publishingExecution, "shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Empty(harness.WorkQueue.Items);
        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(30502, entry.EventId);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(publishingExecution.ToString(), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>発行した実行に Snapshot が無いのはデータの不整合なので、例外にして何も積まない。</summary>
    [Fact]
    public async Task PublishFromExecutionAsync_WhenSnapshotMissing_ThrowsInvalidOperation()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var starter = await SeedProjectMemberAsync(db, ProjectAccessRole.Executor);
        var harness = CreateHarness(db, WorkerContext(), new FixedUserPrincipalDataAccess(starter.PrincipalId, TestTenantIds.T1TenantId));
        var now = DateTime.UtcNow;
        var publishingExecution = Guid.NewGuid();
        await SeedExecutionAsync(db, publishingExecution, starter.DefinitionId, now, TestTenantIds.T1TenantId);
        await SeedSubscriptionAsync(db, Guid.NewGuid(), starter.DefinitionId, "wait-a", "shared.topic", "k1", "statevia.event.subscribe.0", now);

        // Act
        var act = () => harness.Service.PublishFromExecutionAsync(publishingExecution, "shared.topic", "k1", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Empty(harness.WorkQueue.Items);
    }

    private sealed record Harness(EventIngressService Service, CapturingWorkQueue WorkQueue, ListLogger Logger);

    private sealed record ProjectMember(Guid ProjectId, Guid DefinitionId, Guid PrincipalId);

    private static Harness CreateHarness(
        InMemoryTestDatabase db,
        ITenantContextAccessor tenantContext,
        IPrincipalDataAccess? principals = null,
        IRuntimePermissionAuthorization? runtimeAuth = null)
    {
        var transactions = new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory));
        var workQueue = new CapturingWorkQueue();
        var logger = new ListLogger();
        var service = new EventIngressService(
            transactions,
            new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()),
            new ExecutionRepository(),
            principals ?? new MissingPrincipalDataAccess(),
            workQueue,
            new DefaultIdGenerator(),
            runtimeAuth ?? new AllowAllRuntimePermissionAuthorization(),
            tenantContext,
            logger);
        return new Harness(service, workQueue, logger);
    }

    /// <summary>HTTP の呼び出し元として、T1 に Principal を持つ文脈。</summary>
    private static ITenantContextAccessor CallerContext(Guid principalId)
    {
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(new TenantContextState(TestTenantIds.T1TenantId, "t1", principalId, TenantLifecycle.Active));
        return accessor;
    }

    /// <summary>Worker のように Principal の無い T1 の文脈。</summary>
    private static ITenantContextAccessor WorkerContext()
    {
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.T1Context);
        return accessor;
    }

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

    private static async Task SeedSubscriptionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        Guid definitionId,
        string nodeId,
        string topic,
        string correlationKey,
        string resumeEventName,
        DateTime createdAt,
        Guid? tenantId = null)
    {
        await SeedExecutionAsync(db, executionId, definitionId, createdAt, tenantId ?? TestTenantIds.T1TenantId);
        await using var ctx = new CoreDbContext(db.Options);
        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            WaitKind = ExecutionWaitKind.EventWait,
            AllowedEvents = [resumeEventName],
            CreatedAt = createdAt
        });
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

    /// <summary>実行の行を作る。<paramref name="startedByPrincipalId"/> を渡すと、その開始者の Snapshot を付ける。</summary>
    private static async Task SeedExecutionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        Guid definitionId,
        DateTime createdAt,
        Guid tenantId,
        Guid? startedByPrincipalId = null)
    {
        await using var ctx = new CoreDbContext(db.Options);
        if (await ctx.Executions.AnyAsync(x => x.ExecutionId == executionId))
            return;

        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = createdAt,
            UpdatedAt = createdAt,
            CancelRequested = false,
            RestartLost = false,
            SecuritySnapshotJson = startedByPrincipalId is { } starter
                ? ExecutionSecuritySnapshotJson.Serialize(CreateSnapshot(tenantId, starter, createdAt))
                : null
        });
        await ctx.SaveChangesAsync();
    }

    private static ExecutionSecuritySnapshot CreateSnapshot(Guid tenantId, Guid startedByPrincipalId, DateTime capturedAt)
    {
        var keys = new[] { WellKnownPermissionKeys.ExecutionsWrite };
        return new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = startedByPrincipalId,
            PrincipalType = "User",
            EffectivePermissionKeys = keys,
            PermissionSetHash = PermissionSetHash.Compute(keys),
            AuthorizationContext = new AuthorizationContextSnapshot
            {
                ProjectId = Guid.NewGuid(),
                ProjectRole = "executor",
                GroupSnapshots = Array.Empty<GroupSnapshot>(),
                IsTenantAdmin = false
            },
            EvaluationMode = SecurityEvaluationMode.Snapshot,
            CapturedAt = capturedAt
        };
    }

    private sealed class DenyingRuntimePermissionAuthorization : IRuntimePermissionAuthorization
    {
        public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
        {
            _ = permissionKey;
            _ = cancellationToken;
            throw new ForbiddenException("Insufficient permission.", "PERMISSION_DENIED");
        }
    }

    /// <summary>どの Principal も無効化済みとして返す。</summary>
    private sealed class InactivePrincipalDataAccess : IPrincipalDataAccess
    {
        public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult<PrincipalInfo?>(new PrincipalInfo(
                principalId,
                TestTenantIds.T1TenantId,
                PrincipalType.User,
                IsActive: false,
                DisabledAt: DateTime.UtcNow,
                DeletedAt: null));

        public Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed record LogEntry(int EventId, LogLevel Level, string Message);

    private sealed class ListLogger : ILogger<EventIngressService>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(eventId.Id, logLevel, formatter(state, exception)));
    }

    private sealed class CapturingWorkQueue : IExecutionWorkQueue
    {
        public List<ExecutionWorkItemRow> Items { get; } = [];

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            EnqueueAsync(item, ct);

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct)
        {
            Items.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueManyAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionWorkItemRow> items,
            CancellationToken ct) =>
            EnqueueManyAsync(items, ct);

        public Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            int limit,
            IReadOnlyList<string>? kinds,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> RenewLeaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteIncompleteStartItemsAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task ReleaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime availableAt,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
