using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary><see cref="PublishActionState"/> の ingress 接続と購読再開。</summary>
public sealed class PublishActionStateTests
{
    /// <summary>発行した実行の ID を添えて ingress を呼び、PublishTopic には配送を任せない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTopicPresent_CallsIngressWithPublishingExecutionAndDoesNotPublishTopic()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var events = new TrackingEventProvider();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "payment.completed" });
        var publishingExecutionId = Guid.NewGuid();

        // Act
        var result = await sut.ExecuteAsync(MakeContext(events, publishingExecutionId), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("payment.completed", map["topic"]);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Equal(1, ingress.CallCount);
        Assert.Equal(publishingExecutionId, ingress.LastPublishingExecutionId);
        Assert.Equal("payment.completed", ingress.LastTopic);
        Assert.Equal(string.Empty, ingress.LastKey);
        Assert.Equal(0, events.PublishTopicCalls);
    }

    /// <summary>key 省略は空文字で ingress に渡す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenKeyOmitted_PassesEmptyKey()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "orders.updated" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), Guid.NewGuid()), input, CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, ingress.LastKey);
    }

    /// <summary>key を指定すると trim 前の値を ingress に渡す（正規化は ingress 側）。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenKeyPresent_PassesKeyToIngress()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "orders.updated", key = "SKU-1" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), Guid.NewGuid()), input, CancellationToken.None);

        // Assert
        Assert.Equal("SKU-1", ingress.LastKey);
    }

    /// <summary>topic 欠落は ingress を呼ばず失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTopicMissing_ThrowsAndDoesNotCallIngress()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { key = "SKU-1" });

        // Act
        var act = () => sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), Guid.NewGuid()), input, CancellationToken.None);

        // Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(act);
        Assert.Equal(0, ingress.CallCount);
    }

    /// <summary>実行 ID が UUID でなければ、開始者を引けないので ingress を呼ばずに失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenExecutionIdIsNotGuid_ThrowsAndDoesNotCallIngress()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "payment.completed" });
        var context = new StateContext
        {
            Events = new TrackingEventProvider(),
            Store = new NoopStore(),
            ExecutionId = "not-a-guid",
            StateName = "Publish"
        };

        // Act
        var act = () => sut.ExecuteAsync(context, input, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Equal(0, ingress.CallCount);
    }

    /// <summary>開始者が所属する Project の subscribe Wait が、publish で Resume ワークを受ける。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionMatches_EnqueuesResume()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var publisher = await SeedPublisherAsync(db.Factory);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db.Factory, workQueue, publisher);
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(db.Factory, new SubscriptionSeed(executionId, TestTenantIds.T1TenantId, publisher.DefinitionId, "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0"));
        var input = JsonSerializer.SerializeToElement(new { topic = "inventory.received", key = "SKU-1" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), publisher.ExecutionId), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Single(workQueue.Items);
        Assert.Equal(executionId, workQueue.Items[0].ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Resume, workQueue.Items[0].Kind);
    }

    /// <summary>一致 0 件でも Action は成功し enqueue しない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoMatch_SucceedsWithoutEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var publisher = await SeedPublisherAsync(db.Factory);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db.Factory, workQueue, publisher);
        var input = JsonSerializer.SerializeToElement(new { topic = "missing.topic" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), publisher.ExecutionId), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Empty(workQueue.Items);
    }

    /// <summary>同一 topic/key の購読 2 件が両方進む。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTwoSubscriptionsMatch_EnqueuesBoth()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var publisher = await SeedPublisherAsync(db.Factory);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db.Factory, workQueue, publisher);
        var execution1 = Guid.NewGuid();
        var execution2 = Guid.NewGuid();
        await SeedSubscriptionAsync(db.Factory, new SubscriptionSeed(execution1, TestTenantIds.T1TenantId, publisher.DefinitionId, "wait-a", "inventory.received", "SKU-1", "statevia.event.subscribe.0"));
        await SeedSubscriptionAsync(db.Factory, new SubscriptionSeed(execution2, TestTenantIds.T1TenantId, publisher.DefinitionId, "wait-b", "inventory.received", "SKU-1", "statevia.event.subscribe.0"));
        var input = JsonSerializer.SerializeToElement(new { topic = "inventory.received", key = "SKU-1" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), publisher.ExecutionId), input, CancellationToken.None);

        // Assert
        Assert.Equal(2, workQueue.Items.Count);
        Assert.Contains(workQueue.Items, item => item.ExecutionId == execution1);
        Assert.Contains(workQueue.Items, item => item.ExecutionId == execution2);
    }

    /// <summary>開始者が所属しない Project の購読は、publish しても Resume を積まない（Action は成功する）。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionOutsideStarterProjects_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var publisher = await SeedPublisherAsync(db.Factory);
        var otherProject = await SeedPublisherAsync(db.Factory);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db.Factory, workQueue, publisher);
        var otherExecution = Guid.NewGuid();
        await SeedSubscriptionAsync(db.Factory, new SubscriptionSeed(otherExecution, TestTenantIds.T1TenantId, otherProject.DefinitionId, "wait-a", "inventory.received", "SKU-1", "statevia.event.subscribe.0"));
        var input = JsonSerializer.SerializeToElement(new { topic = "inventory.received", key = "SKU-1" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), publisher.ExecutionId), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Empty(workQueue.Items);
    }

    /// <summary>wait.events のみの Wait は購読照合の対象にならない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSignalWaitOnly_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var publisher = await SeedPublisherAsync(db.Factory);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db.Factory, workQueue, publisher);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var ctx = db.Factory.CreateDbContext())
        {
            ctx.Executions.Add(CreateExecution(executionId, TestTenantIds.T1TenantId, publisher.DefinitionId, now));
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "SignalWait",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["ChildCompleted"],
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        var input = JsonSerializer.SerializeToElement(new { topic = "child.done", key = "parent-1" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), publisher.ExecutionId), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Empty(workQueue.Items);
    }

    /// <summary>他テナントの購読は現在テナントの publish では進まない（範囲に入る定義 ID を持っていても、テナントの条件で除く）。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOtherTenantSubscription_DoesNotEnqueue()
    {
        // Arrange
        var tenantAccessor = new SettableTenantContextAccessor();
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("statevia-publish-tenant-" + Guid.NewGuid())
            .Options;
        var disabledFactory = new FilterToggleDbContextFactory(options, tenantAccessor, filterEnabled: false);
        var enabledFactory = new FilterToggleDbContextFactory(options, tenantAccessor, filterEnabled: true);
        var publisher = await SeedPublisherAsync(disabledFactory);
        var t1Execution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        await SeedSubscriptionAsync(disabledFactory, new SubscriptionSeed(t1Execution, TestTenantIds.T1TenantId, publisher.DefinitionId, "t1-wait", "shared.topic", "k1", "statevia.event.subscribe.0"));
        await SeedSubscriptionAsync(disabledFactory, new SubscriptionSeed(otherExecution, TestTenantIds.OtherTenantId, publisher.DefinitionId, "other-wait", "shared.topic", "k1", "statevia.event.subscribe.0"));
        tenantAccessor.Set(TestTenantIds.T1Context);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(enabledFactory, workQueue, publisher);
        var input = JsonSerializer.SerializeToElement(new { topic = "shared.topic", key = "k1" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider(), publisher.ExecutionId), input, CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(t1Execution, workQueue.Items[0].ExecutionId);
        Assert.DoesNotContain(workQueue.Items, item => item.ExecutionId == otherExecution);
    }

    /// <summary>発行した実行と、その開始者が実行者として所属する Project・定義。</summary>
    /// <param name="ExecutionId">発行した実行。</param>
    /// <param name="DefinitionId">開始者が所属する Project の定義。</param>
    /// <param name="StarterPrincipalId">発行した実行の開始者。</param>
    private sealed record Publisher(Guid ExecutionId, Guid DefinitionId, Guid StarterPrincipalId);

    private static PublishActionState CreateSutWithIngress(
        IDbContextFactory<CoreDbContext> dbFactory,
        CapturingWorkQueue workQueue,
        Publisher publisher)
    {
        var workerContext = new SettableTenantContextAccessor();
        workerContext.Set(TestTenantIds.T1Context);
        var ingress = new EventIngressService(
            new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(dbFactory)),
            new ExecutionWaitRepository(dbFactory, new DefaultIdGenerator()),
            new ExecutionRepository(),
            new FixedUserPrincipalDataAccess(publisher.StarterPrincipalId, TestTenantIds.T1TenantId),
            workQueue,
            new DefaultIdGenerator(),
            new AllowAllRuntimePermissionAuthorization(),
            workerContext,
            NullLogger<EventIngressService>.Instance);
        return new PublishActionState(BuildScopeFactory(ingress));
    }

    private static IServiceScopeFactory BuildScopeFactory(IEventIngressService ingress)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ingress);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static StateContext MakeContext(IEventProvider events, Guid executionId) =>
        new()
        {
            Events = events,
            Store = new NoopStore(),
            ExecutionId = executionId.ToString("D"),
            StateName = "Publish"
        };

    /// <summary>T1 に Project・定義・実行者の User と、その User が開始した実行（Snapshot つき）を作る。</summary>
    private static async Task<Publisher> SeedPublisherAsync(IDbContextFactory<CoreDbContext> factory)
    {
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var ctx = factory.CreateDbContext();
        ctx.Projects.Add(new ProjectRow
        {
            ProjectId = projectId,
            TenantId = TestTenantIds.T1TenantId,
            Slug = $"p-{projectId:N}",
            DisplayName = "project",
            CreatedAt = now
        });
        DefinitionTestData.AddDefinitionWithVersion(ctx, TestTenantIds.T1TenantId, definitionId, $"def-{definitionId:N}", projectId);
        var starterPrincipalId = ProjectTestData.AddMemberPrincipal(ctx, TestTenantIds.T1TenantId, projectId, ProjectAccessRole.Executor);
        var execution = CreateExecution(executionId, TestTenantIds.T1TenantId, definitionId, now);
        execution.SecuritySnapshotJson = ExecutionSecuritySnapshotJson.Serialize(CreateSnapshot(starterPrincipalId, now));
        ctx.Executions.Add(execution);
        await ctx.SaveChangesAsync();
        return new Publisher(executionId, definitionId, starterPrincipalId);
    }

    private static ExecutionSecuritySnapshot CreateSnapshot(Guid startedByPrincipalId, DateTime capturedAt)
    {
        var keys = new[] { WellKnownPermissionKeys.ExecutionsWrite };
        return new ExecutionSecuritySnapshot
        {
            TenantId = TestTenantIds.T1TenantId,
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

    /// <summary>購読シードの入力。</summary>
    /// <param name="ExecutionId">対象 execution。</param>
    /// <param name="TenantId">execution のテナント。</param>
    /// <param name="DefinitionId">execution の定義。</param>
    /// <param name="NodeId">Wait ノード ID。</param>
    /// <param name="Topic">購読トピック。</param>
    /// <param name="CorrelationKey">購読 key。</param>
    /// <param name="ResumeEventName">Resume に渡す内部イベント名。</param>
    private sealed record SubscriptionSeed(
        Guid ExecutionId,
        Guid TenantId,
        Guid DefinitionId,
        string NodeId,
        string Topic,
        string CorrelationKey,
        string ResumeEventName);

    private static async Task SeedSubscriptionAsync(IDbContextFactory<CoreDbContext> factory, SubscriptionSeed seed)
    {
        var createdAt = DateTime.UtcNow;
        await using var ctx = factory.CreateDbContext();
        if (!await ctx.Executions.IgnoreQueryFilters().AnyAsync(x => x.ExecutionId == seed.ExecutionId))
            ctx.Executions.Add(CreateExecution(seed.ExecutionId, seed.TenantId, seed.DefinitionId, createdAt));

        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = seed.ExecutionId,
            NodeId = seed.NodeId,
            WaitKind = ExecutionWaitKind.EventWait,
            AllowedEvents = [seed.ResumeEventName],
            CreatedAt = createdAt
        });
        ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
        {
            SubscriptionId = Guid.NewGuid(),
            ExecutionId = seed.ExecutionId,
            NodeId = seed.NodeId,
            Topic = seed.Topic,
            CorrelationKey = seed.CorrelationKey,
            ResumeEventName = seed.ResumeEventName,
            CreatedAt = createdAt
        });
        await ctx.SaveChangesAsync();
    }

    private static ExecutionRow CreateExecution(Guid executionId, Guid tenantId, Guid definitionId, DateTime createdAt) =>
        new()
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = createdAt,
            UpdatedAt = createdAt,
            CancelRequested = false,
            RestartLost = false
        };

    private sealed class FilterToggleDbContextFactory(
        DbContextOptions<CoreDbContext> options,
        ITenantContextAccessor tenantAccessor,
        bool filterEnabled) : IDbContextFactory<CoreDbContext>
    {
        public CoreDbContext CreateDbContext() =>
            new(
                options,
                tenantAccessor,
                filterEnabled
                    ? EnabledTenantQueryFilterOptions.Instance
                    : DisabledTenantQueryFilterOptions.Instance);

        public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    /// <summary>Builtin が呼ぶ <see cref="IEventIngressService.PublishFromExecutionAsync"/> を記録する。HTTP 用の入口は呼ばれない想定。</summary>
    private sealed class CapturingEventIngressService : IEventIngressService
    {
        public Guid? LastPublishingExecutionId { get; private set; }
        public string? LastTopic { get; private set; }
        public string? LastKey { get; private set; }
        public int CallCount { get; private set; }

        public Task PublishAsync(string topic, string key, CancellationToken ct) =>
            throw new InvalidOperationException("event.publish must not use the HTTP entry point.");

        public Task PublishFromExecutionAsync(Guid publishingExecutionId, string topic, string key, CancellationToken ct)
        {
            CallCount++;
            LastPublishingExecutionId = publishingExecutionId;
            LastTopic = topic;
            LastKey = key;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingEventProvider : IEventProvider
    {
        public int PublishTopicCalls { get; private set; }

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Resume(string nodeId, string eventName) { }

        public void PublishTopic(string topic, object? payloadSummary) => PublishTopicCalls++;
    }

    private sealed class NoopStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
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
