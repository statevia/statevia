using Microsoft.Extensions.Logging;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Scheduler;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="IExecutionEngine"/> の実装。定義駆動型実行エンジンの中核クラスです。
/// コンパイル済みワークフロー定義に基づき、事実駆動型 FSM、Fork/Join、Wait/Resume、協調的キャンセルをサポートします。
/// </summary>
/// <remarks>
/// <para>
/// ホストが <see cref="IExecutionEngine"/> を Singleton として登録する場合、コンストラクタに渡した
/// <see cref="IScheduler"/> はプロセス内の実行インスタンスで共有される（グローバル並列制御の単一窓口）。
/// </para>
/// <para>
/// 例外を広く捕捉する <c>#pragma warning disable CA1031</c> 付きの try/catch は、複数実行インスタンスへのイベントブロードキャストで
/// 失敗を集約するため、状態実行失敗をグラフへ記録するため、およびログ実装の失敗を遷移へ伝播させないためである。
/// </para>
/// </remarks>
public sealed partial class ExecutionEngine : IExecutionEngine, IDisposable
{
    private readonly IScheduler _scheduler;
    private readonly IExecutionInstanceFactory _instanceFactory;
    private readonly IExecutionIdGenerator _executionIdGenerator;
    private readonly ExecutionEngineLogger _executionLog;
    private readonly string _workerId;
    private readonly ConcurrentDictionary<string, ExecutionInstance> _instances = new();
    private readonly ConcurrentDictionary<string, EventProvider> _eventProviders = new();
    private Func<string, Task>? _nodeCompletedHandler;
    private Func<string, string, Task>? _suspendHandler;
    private Func<ForkExpansionEvent, Task>? _forkExpansionHandler;

    /// <summary>
    /// 依存を注入してエンジンを構築する。
    /// </summary>
    /// <param name="scheduler">状態実行のスケジューリング（並列度は実装側で解釈）。</param>
    /// <param name="instanceFactory">実行インスタンスの組み立て。</param>
    /// <param name="executionIdGenerator"><see cref="IExecutionEngine.Start"/> で ID 未指定のときに使う生成器。</param>
    /// <param name="loggerFactory">実行ログ用 <see cref="ExecutionEngineLogger"/> の生成に使うファクトリ。</param>
    public ExecutionEngine(
        IScheduler scheduler,
        IExecutionInstanceFactory instanceFactory,
        IExecutionIdGenerator executionIdGenerator,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(instanceFactory);
        ArgumentNullException.ThrowIfNull(executionIdGenerator);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _scheduler = scheduler;
        _instanceFactory = instanceFactory;
        _executionIdGenerator = executionIdGenerator;
        _executionLog = new ExecutionEngineLogger(loggerFactory.CreateLogger<ExecutionEngineLogger>());
        _workerId = Guid.NewGuid().ToString("D");
    }

    /// <inheritdoc />
    public string Start(
        CompiledWorkflowDefinition definition,
        string? executionId = null,
        object? input = null,
        string? initialState = null,
        ForkBranchStart? forkBranch = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        executionId ??= _executionIdGenerator.NewExecutionId();
        var startState = string.IsNullOrWhiteSpace(initialState)
            ? definition.InitialState
            : initialState.Trim();
        var eventProvider = CreateEventProvider(executionId);
        var instance = _instanceFactory.Create(definition, executionId);
        _instances[executionId] = instance;
        _eventProviders[executionId] = eventProvider;
        _executionLog.LogExecutionStarted(executionId, definition.Name, startState);
        _ = RunExecutionAsync(instance, eventProvider, input, startState, forkBranch);
        return executionId;
    }

    private EventProvider CreateEventProvider(string executionId)
    {
        var eventProvider = new EventProvider(executionId);
        eventProvider.OnNodeWaitRegistered = nodeId => NotifySuspend(executionId, nodeId);
        return eventProvider;
    }

    private void NotifySuspend(string executionId, string nodeId)
    {
        var handler = _suspendHandler;
        if (handler is null)
        {
            return;
        }

        _ = NotifySuspendAsync(handler, executionId, nodeId);
    }

    private async Task NotifySuspendAsync(
        Func<string, string, Task> handler,
        string executionId,
        string nodeId)
    {
        try
        {
            await handler(executionId, nodeId).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // suspend 通知失敗は実行継続を妨げない
        catch (Exception ex)
        {
            _executionLog.LogExecutionRunFailed(ex, executionId, "suspend-handler");
        }
#pragma warning restore CA1031
    }

    private void NotifyForkExpansion(
        string executionId,
        string forkState,
        string sourceNodeId,
        IReadOnlyList<ForkBranchExpansionPlan> branches,
        WorkflowExecutionContext parentContext)
    {
        var handler = _forkExpansionHandler;
        if (handler is null)
            return;

        // 各枝は Fork 到達時点の親の vars / states を引き継ぐ。以後の親の書き込みは写しに影響しない。
        var evt = new ForkExpansionEvent(
            executionId,
            forkState,
            sourceNodeId,
            branches,
            parentContext.Input,
            parentContext.ExportForkParentContext());
        _ = NotifyForkExpansionAsync(handler, evt);
    }

    private async Task NotifyForkExpansionAsync(
        Func<ForkExpansionEvent, Task> handler,
        ForkExpansionEvent evt)
    {
        try
        {
            await handler(evt).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Fork 展開通知失敗は呼び出し元の再試行／親 Failed に委ね、Engine ループは止めない
        catch (Exception ex)
        {
            _executionLog.LogForkExpansionHandlerFailed(ex, evt.ExecutionId);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public void ResumeWaitNode(string executionId, string nodeId, string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        if (!_eventProviders.TryGetValue(executionId, out var eventProvider)
            || !_instances.TryGetValue(executionId, out var instance))
        {
            throw new InvalidOperationException($"Execution '{executionId}' was not found.");
        }

        var node = instance.Graph.GetNodesSnapshot()
            .FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            throw new InvalidOperationException($"Node '{nodeId}' was not found in execution '{executionId}'.");
        }

        if (!string.Equals(node.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
            || node.CompletedAt is not null)
        {
            throw new InvalidOperationException(
                $"Node '{nodeId}' is not an active Wait node in execution '{executionId}'.");
        }

        // EventProvider.Resume と route table キー（Load 時 Trim）に合わせて正規化する。
        var trimmedEventName = eventName.Trim();
        if (!instance.Definition.WaitEventRouteTable.TryGetValue(node.NodeName, out var routes)
            || !routes.ContainsKey(trimmedEventName))
        {
            throw new InvalidOperationException(
                $"Event '{trimmedEventName}' is not allowed for Wait state '{node.NodeName}'.");
        }

        eventProvider.Resume(nodeId, trimmedEventName);
    }

    /// <inheritdoc />
    public void PublishEvent(string executionId, string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        if (!_instances.TryGetValue(executionId, out var instance))
        {
            return;
        }

        var activeWaits = instance.Graph.GetNodesSnapshot()
            .Where(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && n.CompletedAt is null)
            .ToList();
        if (activeWaits.Count != 1)
        {
            throw new InvalidOperationException(
                $"PublishEvent requires exactly one active Wait node (found {activeWaits.Count}).");
        }

        var waitNode = activeWaits[0];
        if (!instance.Definition.WaitEventRouteTable.TryGetValue(waitNode.NodeName, out var routes)
            || routes.Count != 1)
        {
            throw new InvalidOperationException(
                $"PublishEvent requires the active Wait state '{waitNode.NodeName}' to have exactly one event.");
        }

        // シムは「唯一の許可イベント」と呼び出し eventName が一致するときだけ委譲する。
        var soleAllowedEvent = routes.Keys.First();
        var trimmedEventName = eventName.Trim();
        if (!string.Equals(soleAllowedEvent, trimmedEventName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PublishEvent event '{trimmedEventName}' does not match the sole allowed event '{soleAllowedEvent}' for Wait state '{waitNode.NodeName}'.");
        }

        ResumeWaitNode(executionId, waitNode.NodeId, soleAllowedEvent);
    }

    /// <inheritdoc />
    public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId)
    {
        if (!_instances.TryGetValue(executionId, out var instance))
        {
            return ApplyResult.Applied;
        }

        if (!instance.TryRegisterPublishClientEventId(clientEventId))
        {
            return ApplyResult.AlreadyApplied;
        }

        try
        {
            PublishEvent(executionId, eventName);
            return ApplyResult.Applied;
        }
        catch
        {
            instance.RemovePublishClientEventId(clientEventId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CancelAsync(string executionId)
    {
        if (_instances.TryGetValue(executionId, out var instance))
        {
            instance.MarkCancelled();
            _executionLog.LogExecutionCancelRequested(executionId);
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId)
    {
        if (!_instances.TryGetValue(executionId, out var instance))
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return ApplyResult.Applied;
        }

        if (!instance.TryRegisterCancelClientEventId(clientEventId))
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return ApplyResult.AlreadyApplied;
        }

        instance.MarkCancelled();
        _executionLog.LogExecutionCancelRequested(executionId);
        await Task.CompletedTask.ConfigureAwait(false);
        return ApplyResult.Applied;
    }

    /// <inheritdoc />
    public ExecutionSnapshot? GetSnapshot(string executionId) =>
        _instances.TryGetValue(executionId, out var instance) ? instance.ToSnapshot() : null;

    /// <inheritdoc />
    public string ExportExecutionGraph(string executionId) =>
        _instances.TryGetValue(executionId, out var instance) ? instance.Graph.ExportJson() : "{}";

    /// <inheritdoc />
    public void SetNodeCompletedHandler(Func<string, Task>? handler)
    {
        _nodeCompletedHandler = handler;
    }

    /// <inheritdoc />
    public void SetSuspendHandler(Func<string, string, Task>? handler)
    {
        _suspendHandler = handler;
    }

    /// <inheritdoc />
    public void SetForkExpansionHandler(Func<ForkExpansionEvent, Task>? handler)
    {
        _forkExpansionHandler = handler;
    }

    /// <inheritdoc />
    public ExecutionRuntimeCheckpoint? ExportCheckpoint(string executionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        return _instances.TryGetValue(executionId, out var instance)
            ? instance.ExportRuntimeCheckpoint()
            : null;
    }

    /// <inheritdoc />
    public void ImportCheckpoint(CompiledWorkflowDefinition definition, ExecutionRuntimeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.SchemaVersion != ExecutionRuntimeCheckpoint.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported checkpoint schema version {checkpoint.SchemaVersion}.");
        }

        if (!string.Equals(definition.Name, checkpoint.DefinitionName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Definition name mismatch: checkpoint='{checkpoint.DefinitionName}', provided='{definition.Name}'.");
        }

        var executionId = checkpoint.ExecutionId;
        if (_instances.ContainsKey(executionId))
        {
            throw new InvalidOperationException($"Execution '{executionId}' is already loaded.");
        }

        var eventProvider = CreateEventProvider(executionId);
        var instance = _instanceFactory.Create(definition, executionId);
        instance.ApplyRuntimeCheckpoint(checkpoint);
        _instances[executionId] = instance;
        _eventProviders[executionId] = eventProvider;

        if (checkpoint.IsTerminal)
        {
            return;
        }

        foreach (var wait in checkpoint.PendingWaits)
        {
            _ = ContinueRestoredWaitAsync(instance, eventProvider, wait);
        }

        // NodeCompleted 時点の checkpoint は次遷移前のため、完了済みで出辺のないノードから継続する。
        ContinueCompletedFrontierAfterImport(instance, eventProvider);
    }

    /// <inheritdoc />
    public bool Unload(string executionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        if (!_instances.TryRemove(executionId, out var instance))
        {
            _eventProviders.TryRemove(executionId, out _);
            return false;
        }

        // Dispose が ActionCancellationToken をキャンセルするより先に Unload 例外で waiter を閉じる。
        // 逆順だと RegisterWaitCancellation が TaskCanceledException を先に立て、
        // Wait が Fact=Cancelled で実行終端になる。
        if (_eventProviders.TryRemove(executionId, out var eventProvider))
        {
            eventProvider.OnNodeWaitRegistered = null;
            eventProvider.AbortForUnload();
        }

        instance.Dispose();

        return true;
    }

    private async Task ContinueRestoredWaitAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        CheckpointPendingWait wait)
    {
        instance.AddActiveState(wait.NodeName);
        var scope = instance.GetNodeScope(wait.NodeId);
        try
        {
            var (fact, output) = await _scheduler.RunAsync(async ct =>
            {
                try
                {
                    var eventName = await eventProvider
                        .WaitForEventAsync(wait.NodeId, wait.AllowedEvents, notifySuspend: false, ct)
                        .ConfigureAwait(false);
                    object result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["event"] = eventName
                    };
                    instance.SetOutput(wait.NodeName, result, scope);
                    instance.Graph.CompleteNode(wait.NodeId, Fact.Completed, result);
                    await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                    return (Fact.Completed, result);
                }
                catch (OperationCanceledException exception) when (IsParkUnloadCancellation(instance, exception))
                {
                    return ((string?)null, (object?)null);
                }
                catch (OperationCanceledException)
                {
                    instance.Graph.CompleteNode(wait.NodeId, Fact.Cancelled, null);
                    await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                    return (Fact.Cancelled, (object?)null);
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    instance.Graph.CompleteNode(wait.NodeId, Fact.Failed, null);
                    await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                    _executionLog.LogStateExecuteFailed(
                        ex,
                        instance.ExecutionId,
                        wait.NodeName,
                        wait.NodeId,
                        ex.GetType().Name);
                    return (Fact.Failed, (object?)null);
                }
#pragma warning restore CA1031
            }, instance.ActionCancellationToken).ConfigureAwait(false);

            if (fact is null)
            {
                return;
            }

            ProcessFact(instance, eventProvider, wait.NodeName, fact, output, wait.NodeId, scope);
        }
        finally
        {
            instance.RemoveActiveState(wait.NodeName);
        }
    }

    /// <summary>
    /// Import 後、完了済みで出辺のないノードから遷移を再開する（recovery / ステップ checkpoint 用）。
    /// </summary>
    /// <remarks>
    /// <see cref="NotifyNodeCompletedAsync"/> 時点の checkpoint は <see cref="ProcessFact"/> 前のため
    /// 出辺が無い。Wait 未完了は <see cref="ContinueRestoredWaitAsync"/> が担当する。
    /// </remarks>
    private void ContinueCompletedFrontierAfterImport(ExecutionInstance instance, EventProvider eventProvider)
    {
        var nodes = instance.Graph.GetNodesSnapshot();
        var nodesWithOutgoing = instance.Graph.GetEdgesSnapshot()
            .Select(edge => edge.From)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node.CompletedAt is null || string.IsNullOrWhiteSpace(node.Fact))
            {
                continue;
            }

            if (nodesWithOutgoing.Contains(node.NodeId))
            {
                continue;
            }

            // 未完了 Wait は PendingWaits 経路。完了済み Wait で出辺なしなら ProcessFact が必要。
            instance.RemoveActiveState(node.NodeName);
            instance.TryGetOutput(node.NodeName, out var output);
            ProcessFact(instance, eventProvider, node.NodeName, node.Fact, output, node.NodeId, instance.GetNodeScope(node.NodeId));
        }
    }

    private async Task RunExecutionAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        object? input,
        string startState,
        ForkBranchStart? forkBranch)
    {
        if (forkBranch is { } branch)
            instance.InitializeForkBranchContext(branch);
        else
            instance.InitializeContext(input);
        // Fork の枝の input は親の Context で評価済み。子の Context で再評価すると値を失う。
        var initialInput = forkBranch is null ? ApplyStateInput(instance, startState, input, scope: null) : input;
        try
        {
            await ScheduleStateAsync(instance, eventProvider, startState, null, null, initialInput, scope: null).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 例外種別に依らず捕捉し、実行失敗は MarkFailed により観測する
        catch (Exception ex)
        {
            instance.MarkFailed();
            _executionLog.LogExecutionRunFailed(ex, instance.ExecutionId, instance.Definition.Name);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Unload park 由来のキャンセルか。明示 Cancel の <see cref="OperationCanceledException"/> とは区別する。
    /// </summary>
    /// <param name="instance">実行インスタンス。</param>
    /// <param name="exception">捕捉したキャンセル例外。</param>
    /// <returns>park（Unload）として完了事実を出さないとき <see langword="true"/>。</returns>
    private static bool IsParkUnloadCancellation(ExecutionInstance instance, OperationCanceledException exception) =>
        exception is ExecutionUnloadException || instance.IsUnloaded;

    /// <summary>
    /// 状態を 1 つ実行し、完了事実から次へ進む。Join 状態なら Join への到着として扱う。
    /// </summary>
    /// <param name="instance">実行インスタンス。</param>
    /// <param name="eventProvider">実行のイベントプロバイダ。</param>
    /// <param name="stateName">実行する状態名。</param>
    /// <param name="fromNodeId">直前のノード ID（先頭は null）。</param>
    /// <param name="edgeType">直前のノードからの辺の種類（先頭は null）。</param>
    /// <param name="input">状態の input（Join 状態では入ってきた状態の output）。</param>
    /// <param name="scope">状態が属する枝の範囲（Engine 単体の Fork の中だけ。外は null）。</param>
    private async Task ScheduleStateAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string stateName,
        string? fromNodeId,
        EdgeType? edgeType,
        object? input,
        InProcessBranchScope? scope)
    {
        var def = instance.Definition;
        if (def.JoinTable.ContainsKey(stateName))
        {
            if (_forkExpansionHandler is null)
            {
                await ArriveAtJoinAsync(instance, eventProvider, stateName, fromNodeId, input, scope).ConfigureAwait(false);
                return;
            }

            // Hosted 物理子: 親側 Join に到達してもローカル事実が揃わないときは
            // Join を実行せず子 execution として終端する（ネスト Fork の内側→外側 Join）。
            // 通常のアクション完了時の「next が Join なら MarkCompleted」と同契約。
            if (!instance.JoinTracker.IsJoinReadyToExecute(stateName))
            {
                instance.MarkCompleted(input);
                _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                return;
            }

            await RunJoinStateAsync(instance, eventProvider, stateName, fromNodeId, edgeType, scope: null).ConfigureAwait(false);
            return;
        }

        var executor = def.StateExecutorFactory.GetExecutor(stateName);
        if (executor is null)
        {
            // 実行器の無い状態は定義と登録の食い違い（業務の失敗ではない）。on.Failed / error は辿らず、
            // ノードも作らずに実行を Failed にする。黙って戻ると実行中の状態が無いまま Running で止まる。
            instance.MarkFailed();
            instance.HaltActiveActions();
            _executionLog.LogExecutorNotFound(instance.ExecutionId, def.Name, stateName);
            return;
        }

        var attempt = instance.NextAttempt(stateName);
        var nodeType = ResolveNodeType(def, stateName);
        var waitMetadata = BuildWaitNodeGraphMetadata(def, stateName);

        var nodeId = instance.Graph.AddNode(
            stateName,
            nodeType: nodeType,
            input: input,
            attempt: attempt,
            workerId: _workerId,
            wait: waitMetadata);
        if (fromNodeId != null && edgeType != null)
        {
            instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        }
        instance.RegisterNodeScope(nodeId, scope);
        instance.AddActiveState(stateName);

        _executionLog.LogStateScheduled(instance.ExecutionId, stateName, nodeId);

        var ctx = new StateContext
        {
            Events = eventProvider,
            Store = new ExecutionStateStore(instance),
            ExecutionId = instance.ExecutionId,
            StateName = stateName,
            NodeId = nodeId,
            Logger = _executionLog.CreateStateContextLogger(instance.ExecutionId, stateName)
        };

        var (fact, output) = await _scheduler.RunAsync(async ct =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var o = await executor.ExecuteAsync(ctx, input, ct).ConfigureAwait(false);
                instance.SetOutput(stateName, o, scope);
                instance.Graph.CompleteNode(nodeId, Fact.Completed, o);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                sw.Stop();
                _executionLog.LogStateCompleted(instance.ExecutionId, stateName, nodeId, Fact.Completed, sw.ElapsedMilliseconds);
                return (Fact.Completed, o);
            }
            catch (OperationCanceledException exception) when (IsParkUnloadCancellation(instance, exception))
            {
                sw.Stop();
                return ((string?)null, (object?)null);
            }
            catch (OperationCanceledException)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Cancelled, null);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                sw.Stop();
                _executionLog.LogStateCompleted(instance.ExecutionId, stateName, nodeId, Fact.Cancelled, sw.ElapsedMilliseconds);
                return (Fact.Cancelled, (object?)null);
            }
#pragma warning disable CA1031 // 例外種別に依らず捕捉し、状態失敗は実行グラフへ記録する
            catch (Exception ex)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Failed, null);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                sw.Stop();
                _executionLog.LogStateExecuteFailed(ex, instance.ExecutionId, stateName, nodeId, ex.GetType().Name);
                _executionLog.LogStateCompleted(instance.ExecutionId, stateName, nodeId, Fact.Failed, sw.ElapsedMilliseconds);
                return (Fact.Failed, (object?)null);
            }
#pragma warning restore CA1031
            finally { instance.RemoveActiveState(stateName); }
        }, instance.ActionCancellationToken).ConfigureAwait(false);

        if (fact is null)
        {
            return;
        }

        ProcessFact(instance, eventProvider, stateName, fact, output, nodeId, scope);
    }

    /// <summary>
    /// Engine 単体で、枝が Join に入ったことを記録する。全枝がそろえば、枝の完了順に書き込みを外側の Context へマージし、Join を実行する。
    /// </summary>
    /// <remarks>
    /// <para>記録のキーは枝の先頭の名前、値は Join に入ってきた状態の output（Hosted の子の終端と同じ）。
    /// 2 ノード以上の枝でも、枝の最後の状態が Join に入るまで Join は成立しない。</para>
    /// <para>範囲の外、または範囲と別の Join に入るのは検証を通らない定義だけ。そのときは実行を終端失敗にする。</para>
    /// <para>同じ枝の 2 回目の到着（checkpoint の復元で遷移をやり直した場合）は無視する。</para>
    /// </remarks>
    /// <param name="instance">実行インスタンス。</param>
    /// <param name="eventProvider">実行のイベントプロバイダ。</param>
    /// <param name="joinStateName">入った Join の状態名。</param>
    /// <param name="fromNodeId">Join に入ったノードの ID（Join の辺の元）。</param>
    /// <param name="output">Join に入ってきた状態の output。</param>
    /// <param name="scope">Join に入った枝の範囲。</param>
    private async Task ArriveAtJoinAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string joinStateName,
        string? fromNodeId,
        object? output,
        InProcessBranchScope? scope)
    {
        if (scope is null || !string.Equals(scope.Episode.JoinState, joinStateName, StringComparison.OrdinalIgnoreCase))
        {
            instance.MarkFailed();
            instance.HaltActiveActions();
            _executionLog.LogJoinReachedOutsideBranch(instance.ExecutionId, instance.Definition.Name, joinStateName, scope?.Episode.JoinState);
            return;
        }

        if (!scope.Episode.TryArrive(scope))
        {
            return;
        }

        var readyJoin = instance.JoinTracker.RecordFact(scope.BranchHead, Fact.Completed, output, fromNodeId);
        if (readyJoin is null || !instance.JoinTracker.TryBeginJoinExecution(joinStateName))
        {
            return;
        }

        var outer = instance.ContextOf(scope.Episode.Parent);
        foreach (var arrived in scope.Episode.GetArrivals())
        {
            outer.ApplyJoinFragment(arrived.Context.ExportBranchWrites());
        }

        instance.CompleteInProcessFork(scope.Episode);
        await RunStartedJoinStateAsync(instance, eventProvider, joinStateName, fromNodeId, EdgeType.Join, scope.Episode.Parent)
            .ConfigureAwait(false);
    }

    /// <summary>Join の成立を確かめてから Join 状態を実行する（Hosted の物理 Join 用）。</summary>
    private async Task RunJoinStateAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string joinStateName,
        string? fromNodeId,
        EdgeType? edgeType,
        InProcessBranchScope? scope)
    {
        if (!instance.JoinTracker.TryBeginJoinExecution(joinStateName))
        {
            return;
        }

        await RunStartedJoinStateAsync(instance, eventProvider, joinStateName, fromNodeId, edgeType, scope).ConfigureAwait(false);
    }

    /// <summary>
    /// 開始済み（<see cref="Join.IJoinTracker.TryBeginJoinExecution"/> 済み）の Join 状態を実行し、Join の後へ進む。
    /// </summary>
    /// <param name="instance">実行インスタンス。</param>
    /// <param name="eventProvider">実行のイベントプロバイダ。</param>
    /// <param name="joinStateName">Join の状態名。</param>
    /// <param name="fromNodeId">直前のノード ID。</param>
    /// <param name="edgeType">直前のノードからの辺の種類。<see cref="EdgeType.Join"/> なら各枝の到着ノードから辺を張る。</param>
    /// <param name="scope">Join の外側の枝の範囲（最外の Fork では null）。Join の後はこの範囲で続く。</param>
    private async Task RunStartedJoinStateAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string joinStateName,
        string? fromNodeId,
        EdgeType? edgeType,
        InProcessBranchScope? scope)
    {
        var joinInputs = instance.JoinTracker.GetJoinInputs(joinStateName);
        var joinSourceNodeIds = instance.JoinTracker.GetJoinSourceNodeIds(joinStateName);
        var attempt = instance.NextAttempt(joinStateName);
        var nodeId = instance.Graph.AddNode(joinStateName, nodeType: "Join", input: joinInputs, attempt: attempt, workerId: _workerId);
        if (edgeType == EdgeType.Join)
        {
            foreach (var sourceNodeId in joinSourceNodeIds)
            {
                instance.Graph.AddEdge(sourceNodeId, nodeId, EdgeType.Join);
            }
        }
        else if (fromNodeId != null && edgeType != null)
        {
            instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        }
        instance.RegisterNodeScope(nodeId, scope);

        _executionLog.LogJoinStateScheduled(instance.ExecutionId, joinStateName, nodeId);

        instance.Graph.CompleteNode(nodeId, Fact.Joined, null);
        await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);

        _executionLog.LogJoinStateCompleted(instance.ExecutionId, joinStateName, nodeId, Fact.Joined);

        // Join の集約 input を当該 State の output として Context へ記録する。
        // これにより when.path の $.states.<Join>.output… 参照と output: $.vars… 代入が
        // 通常 State と同じ規則で機能する（未記録だと条件が常に path_not_found になる）。
        instance.SetOutput(joinStateName, joinInputs, scope);

        var (transition, routingDiag) = EvaluateTransition(instance, joinStateName, Fact.Joined, scope);
        if (routingDiag is not null)
        {
            instance.Graph.SetNodeConditionRouting(nodeId, routingDiag);
        }

        if (transition.HasTransition && transition.Next != null)
        {
            var mappedJoinInput = ApplyStateInput(instance, transition.Next, joinInputs, scope);
            await ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, mappedJoinInput, scope).ConfigureAwait(false);
        }
        else if (transition.End)
        {
            instance.MarkCompleted(joinInputs);
            _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
        }
        else
        {
            _executionLog.LogWarningNoTransition(instance.ExecutionId, joinStateName, Fact.Joined);
        }
    }

    /// <summary>
    /// ノード完了通知ハンドラへベストエフォートで通知する。
    /// </summary>
    private async Task NotifyNodeCompletedAsync(string executionId)
    {
        var handler = _nodeCompletedHandler;
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(executionId).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 外部ハンドラの失敗は遷移へ伝播させずログのみ（ログ失敗を遷移に伝播させないベストエフォート通知）
        catch (Exception exception)
        {
            _executionLog.LogWarningNodeCompletedHandlerFailed(exception, executionId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Failed 終端は Error、Cancelled 終端は Information。エンジンは Cancel 理由を区別しない。
    /// </summary>
    /// <param name="instance">実行インスタンス。</param>
    /// <param name="stateName">終端になった状態名。</param>
    /// <param name="fact"><see cref="Fact.Failed"/> または <see cref="Fact.Cancelled"/>。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fact"/> が Failed / Cancelled 以外。</exception>
    private void LogTerminalFact(ExecutionInstance instance, string stateName, string fact)
    {
        switch (fact)
        {
            case Fact.Failed:
                _executionLog.LogExecutionTerminalFailure(instance.ExecutionId, instance.Definition.Name, stateName, fact);
                return;
            case Fact.Cancelled:
                _executionLog.LogExecutionCancelled(instance.ExecutionId, instance.Definition.Name, stateName, fact);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(fact), fact, "Expected Failed or Cancelled.");
        }
    }

    /// <summary>
    /// 状態の完了事実から次の遷移を決める。
    /// </summary>
    /// <remarks>
    /// <para>順序: 終端失敗の後なら抑止 → Cancelled は取消として終端（明示 Cancel 後も終端ログを出す） →
    /// 取消の後なら抑止 → Completed / Failed は同じ遷移評価（<c>on.Failed</c> / Nodes の <c>error</c> を含む）。</para>
    /// <para>状態の完了は Join に記録しない。Join への記録は、遷移で Join に入ったとき（<see cref="ArriveAtJoinAsync"/>）だけ行う。
    /// <c>join.all</c> は枝の先頭なので、先頭の完了で記録すると 2 ノード以上の枝で Join が早く成立するため。</para>
    /// <para>Failed で遷移が無いときは終端失敗とし、並行する状態を止めるが <see cref="ExecutionInstance.IsCancelled"/> は立てない。
    /// 止めた状態が返す Cancelled 事実は終端失敗の後として抑止されるため、実行は Failed のまま残る。</para>
    /// </remarks>
    /// <param name="instance">実行インスタンス。</param>
    /// <param name="eventProvider">実行のイベントプロバイダ。</param>
    /// <param name="stateName">完了した状態名。</param>
    /// <param name="fact">完了事実（<see cref="Fact"/>）。</param>
    /// <param name="output">状態の output（失敗・取消では null）。</param>
    /// <param name="nodeId">完了したノード ID。</param>
    /// <param name="scope">状態が属する枝の範囲（外は null）。</param>
    private void ProcessFact(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string stateName,
        string fact,
        object? output,
        string nodeId,
        InProcessBranchScope? scope)
    {
        if (instance.IsFailed)
        {
            _executionLog.LogExecutionFailureSuppressedTransition(
                instance.ExecutionId,
                instance.Definition.Name,
                stateName,
                fact);
            return;
        }

        if (fact is Fact.Cancelled)
        {
            instance.MarkCancelled();
            LogTerminalFact(instance, stateName, fact);
            return;
        }

        if (instance.IsCancelled)
        {
            _executionLog.LogExecutionCancelSuppressedTransition(
                instance.ExecutionId,
                instance.Definition.Name,
                stateName,
                fact);
            return;
        }

        ConditionRoutingDiagnostics? routingDiag = null;
        TransitionResult transition;
        var resumedEvent = eventProvider.TryTakeResumedEvent(nodeId, out var recordedEvent) ? recordedEvent : null;
        switch (ResolveWaitEventRoute(instance.Definition, stateName, fact, resumedEvent, output, out var waitTransition, out var waitEvent))
        {
            case WaitRouteResolution.Resolved:
                transition = waitTransition;
                break;
            case WaitRouteResolution.Unresolved:
                // 遷移先の決まらない Wait は黙って止めず、実行を Failed にする。
                instance.MarkFailed();
                instance.HaltActiveActions();
                _executionLog.LogWaitRouteUnresolved(instance.ExecutionId, instance.Definition.Name, stateName, nodeId, waitEvent);
                return;
            default:
                (transition, routingDiag) = EvaluateTransition(instance, stateName, fact, scope);
                break;
        }

        if (routingDiag is not null)
        {
            instance.Graph.SetNodeConditionRouting(nodeId, routingDiag);
        }

        if (!transition.HasTransition)
        {
            if (fact is Fact.Failed)
            {
                instance.MarkFailed();
                instance.HaltActiveActions();
                LogTerminalFact(instance, stateName, fact);
                return;
            }

            if (!transition.End)
            {
                _executionLog.LogWarningNoTransition(instance.ExecutionId, stateName, fact);
            }
            return;
        }
        if (transition.End)
        {
            instance.MarkCompleted(output);
            _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
            return;
        }

        ScheduleTransitionContinuation(instance, eventProvider, stateName, transition, output, nodeId, scope);
    }

    /// <summary>
    /// Fork / Next 遷移を Hosted 展開または同一インスタンス Schedule に振り分ける。
    /// </summary>
    /// <remarks>
    /// Engine 単体の Fork では、枝ごとに範囲を作り（Context は外側の写し）、枝の先頭をその範囲で走らせる。
    /// 枝の先頭の input 写像は写す前に外側の Context で評価する（Hosted と同じ）。
    /// </remarks>
    private void ScheduleTransitionContinuation(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string stateName,
        TransitionResult transition,
        object? output,
        string nodeId,
        InProcessBranchScope? scope)
    {
        if (transition.Fork != null)
        {
            // Broadcast: 同一 output を各分岐の先頭状態へ写像（workflow-input-output-spec §3.3）。
            var plans = transition.Fork
                .Select(nextState => new ForkBranchExpansionPlan(
                    nextState,
                    ApplyStateInput(instance, nextState, output, scope)))
                .ToList();

            if (_forkExpansionHandler is not null)
            {
                // Hosted: 物理子展開。同一インスタンス上では分岐を走らせない。
                NotifyForkExpansion(instance.ExecutionId, stateName, nodeId, plans, instance.ContextOf(scope));
                return;
            }

            ScheduleInProcessFork(instance, eventProvider, stateName, transition.Fork, plans, nodeId, scope);
            return;
        }

        if (transition.Next is null)
        {
            return;
        }

        // Hosted 物理子: Join は親が集約する。子は分岐終端として完了する。
        if (_forkExpansionHandler is not null
            && instance.Definition.JoinTable.ContainsKey(transition.Next))
        {
            instance.MarkCompleted(output);
            _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
            return;
        }

        var mappedInput = ApplyStateInput(instance, transition.Next, output, scope);
        _ = ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, mappedInput, scope);
    }

    /// <summary>
    /// Engine 単体の Fork で、対になる Join を決めて枝ごとに範囲を作り、各枝の先頭を走らせる。
    /// </summary>
    /// <remarks>
    /// <para>対になる Join は、<c>join.all</c> が Fork の枝の集合と一致するもの（検証の Fork 領域の対応付けと同じ）。
    /// 見つからないのは検証を通らない定義だけで、そのときは実行を終端失敗にする。</para>
    /// <para>同じ Join に再び入る（循環で Fork に戻る）ときに前回の到着が残らないよう、Join の記録を捨ててから始める。</para>
    /// </remarks>
    private void ScheduleInProcessFork(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string forkStateName,
        IReadOnlyList<string> branchHeads,
        IReadOnlyList<ForkBranchExpansionPlan> plans,
        string nodeId,
        InProcessBranchScope? scope)
    {
        var joinState = FindPairedJoinState(instance.Definition, branchHeads);
        if (joinState is null)
        {
            instance.MarkFailed();
            instance.HaltActiveActions();
            _executionLog.LogForkWithoutPairedJoin(instance.ExecutionId, instance.Definition.Name, forkStateName);
            return;
        }

        instance.JoinTracker.ResetForPhysicalJoinReentry(joinState);
        var branchScopes = instance.BeginInProcessFork(nodeId, joinState, scope, branchHeads);
        foreach (var (plan, branchScope) in plans.Zip(branchScopes))
        {
            _ = ScheduleStateAsync(
                instance,
                eventProvider,
                plan.BranchState,
                nodeId,
                EdgeType.Fork,
                plan.MappedInput,
                branchScope);
        }
    }

    /// <summary><c>join.all</c> が枝の集合と一致する Join の状態名を返す。</summary>
    /// <param name="definition">コンパイル済み定義。</param>
    /// <param name="branchHeads">Fork の枝の先頭。</param>
    /// <returns>Join の状態名。一致する Join が無ければ null。</returns>
    private static string? FindPairedJoinState(CompiledWorkflowDefinition definition, IReadOnlyList<string> branchHeads)
    {
        var heads = branchHeads.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return definition.JoinTable
            .Where(join => heads.SetEquals(join.Value))
            .Select(join => join.Key)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public void CompletePhysicalJoin(
        string executionId,
        string joinStateName,
        IReadOnlyDictionary<string, object?> branchOutputs,
        IReadOnlyList<PhysicalJoinContextFragment>? contextMerges = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(joinStateName);
        ArgumentNullException.ThrowIfNull(branchOutputs);

        if (!_instances.TryGetValue(executionId, out var instance)
            || !_eventProviders.TryGetValue(executionId, out var eventProvider))
        {
            throw new InvalidOperationException($"Execution '{executionId}' is not loaded.");
        }

        if (!instance.Definition.JoinTable.ContainsKey(joinStateName))
        {
            throw new InvalidOperationException(
                $"Join state '{joinStateName}' is not defined for execution '{executionId}'.");
        }

        foreach (var fragment in contextMerges ?? [])
        {
            instance.Context.ApplyJoinFragment(fragment);
        }

        // 循環再入場: 前回訪問の startedJoins / 観測事実を捨ててから物理 Join を再実行する。
        instance.JoinTracker.ResetForPhysicalJoinReentry(joinStateName);

        foreach (var (branchState, branchOutput) in branchOutputs)
        {
            instance.JoinTracker.RecordFact(branchState, Fact.Completed, branchOutput);
        }

        _ = RunJoinStateAsync(instance, eventProvider, joinStateName, fromNodeId: null, edgeType: null, scope: null);
    }

    /// <summary>状態の <c>input</c> 写像を範囲の Context で評価する。写像が無ければ候補 input をそのまま返す。</summary>
    private object? ApplyStateInput(ExecutionInstance instance, string targetState, object? rawInput, InProcessBranchScope? scope)
    {
        if (!instance.Definition.StateInputs.TryGetValue(targetState, out var spec))
        {
            return rawInput;
        }

        var evaluated = StateInputEvaluator.ApplyWithDiagnostics(spec, instance.ContextOf(scope), rawInput);
        foreach (var warning in evaluated.Warnings)
        {
            _executionLog.LogWarningInputEvaluation(
                instance.ExecutionId,
                targetState,
                warning.InputKey,
                warning.Reason);
        }

        return evaluated.Value;
    }

    /// <summary>次の遷移を決める。条件遷移の <c>when.path</c> は範囲の Context で評価する。</summary>
    private (TransitionResult Transition, ConditionRoutingDiagnostics? RoutingDiagnostics) EvaluateTransition(
        ExecutionInstance instance,
        string stateName,
        string fact,
        InProcessBranchScope? scope)
    {
        if (instance.Definition.ConditionalTransitions.TryGetValue(stateName, out var stateTransitions)
            && stateTransitions.TryGetValue(fact, out var compiledTransition))
        {
            var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
                compiledTransition,
                fact,
                instance.ContextOf(scope),
                onPathWarning: (path, reason) =>
                    _executionLog.LogWarningConditionPathResolution(instance.ExecutionId, stateName, fact, path, reason));
            return (transition, diagnostics);
        }

        return (instance.Fsm.Evaluate(stateName, fact), null);
    }

    /// <summary>Wait 完了時の遷移先の決まり方。</summary>
    private enum WaitRouteResolution
    {
        /// <summary>Wait ではない、または Completed 以外の事実。通常の遷移評価に進む。</summary>
        NotWaitRoute,

        /// <summary>route table の <c>Next</c> が空（旧 <c>wait.event</c> + <c>on.Completed</c>）。通常の遷移評価に進む。</summary>
        UseFsm,

        /// <summary>route table から次状態が決まった。</summary>
        Resolved,

        /// <summary>再開したイベントが分からない、または route table に無い。実行を Failed にする。</summary>
        Unresolved,
    }

    /// <summary>
    /// Wait 完了時に <see cref="CompiledWorkflowDefinition.WaitEventRouteTable"/> から次状態を解決する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 次状態は route table が正本。引くキーは、Engine が <see cref="EventProvider"/> で記録した再開イベント名を正とする。
    /// 状態の実行器が返す output の形（<c>{ event }</c> を返すかどうか）には依らない。
    /// 記録が無いとき（checkpoint の復元で、完了済みの Wait の遷移をやり直す場合）は、記録済み output の <c>event</c> を使う。
    /// </para>
    /// <para>ルートにイベントはあるが <c>Next</c> が空のときは <see cref="WaitRouteResolution.UseFsm"/> を返し、通常の FSM（on.Completed）に委ねる。</para>
    /// </remarks>
    /// <param name="definition">コンパイル済み定義。</param>
    /// <param name="stateName">完了した状態名。</param>
    /// <param name="fact">完了の事実。</param>
    /// <param name="resumedEvent">Engine が記録した再開イベント名。無ければ null。</param>
    /// <param name="output">状態の output。</param>
    /// <param name="transition"><see cref="WaitRouteResolution.Resolved"/> のときの遷移。</param>
    /// <param name="eventName">ルックアップに使ったイベント名（分からなければ null）。</param>
    private static WaitRouteResolution ResolveWaitEventRoute(
        CompiledWorkflowDefinition definition,
        string stateName,
        string fact,
        string? resumedEvent,
        object? output,
        out TransitionResult transition,
        out string? eventName)
    {
        transition = TransitionResult.None;
        eventName = null;
        if (!string.Equals(fact, Fact.Completed, StringComparison.Ordinal)
            || !definition.WaitEventRouteTable.TryGetValue(stateName, out var routes))
        {
            return WaitRouteResolution.NotWaitRoute;
        }

        eventName = resumedEvent ?? (TryExtractWaitEventName(output, out var fromOutput) ? fromOutput : null);
        if (eventName is null || !routes.TryGetValue(eventName, out var route))
        {
            return WaitRouteResolution.Unresolved;
        }

        // Next 未設定（旧 wait.event + on.Completed など）は通常の FSM 評価に委ねる。
        if (string.IsNullOrWhiteSpace(route.Next))
        {
            return WaitRouteResolution.UseFsm;
        }

        transition = TransitionResult.ToNext(route.Next);
        return WaitRouteResolution.Resolved;
    }

    /// <summary>Wait 監査 output からイベント名を取り出す。</summary>
    private static bool TryExtractWaitEventName(object? output, out string eventName)
    {
        eventName = string.Empty;
        switch (output)
        {
            case IReadOnlyDictionary<string, string> stringMap
                when stringMap.TryGetValue("event", out var fromString)
                     && !string.IsNullOrWhiteSpace(fromString):
                eventName = fromString.Trim();
                return true;
            case IReadOnlyDictionary<string, object?> objectMap
                when objectMap.TryGetValue("event", out var raw)
                     && raw is string fromObject
                     && !string.IsNullOrWhiteSpace(fromObject):
                eventName = fromObject.Trim();
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var instance in _instances.Values)
        {
            instance.Dispose();
        }

        _instances.Clear();
        (_scheduler as IDisposable)?.Dispose();
    }

    /// <summary>コンパイル済み Wait 情報からグラフ用メタデータを構築する。</summary>
    private static WaitNodeGraphMetadata? BuildWaitNodeGraphMetadata(
        CompiledWorkflowDefinition def,
        string stateName)
    {
        List<string>? allowedEvents = null;
        string? waitKey = null;
        if (def.WaitEventRouteTable.TryGetValue(stateName, out var routes) && routes.Count > 0)
        {
            allowedEvents = routes.Keys.ToList();
            if (allowedEvents.Count == 1)
                waitKey = allowedEvents[0];
        }

        IReadOnlyList<WaitSubscriptionSnapshot>? subscriptions = null;
        if (def.WaitSubscriptions.TryGetValue(stateName, out var waitSubscriptions)
            && waitSubscriptions.Count > 0)
        {
            subscriptions = waitSubscriptions
                .Select(s => new WaitSubscriptionSnapshot
                {
                    Topic = s.Topic,
                    Key = s.Key,
                    ResumeEventName = s.ResumeEventName
                })
                .ToList();
        }

        if (allowedEvents is null && waitKey is null && subscriptions is null)
            return null;

        return new WaitNodeGraphMetadata(waitKey, allowedEvents, subscriptions);
    }

    private static string ResolveNodeType(CompiledWorkflowDefinition def, string stateName)
    {
        // Wait は InitialState でも Resume 対象のため Start より優先する。
        if (def.WaitEventRouteTable.ContainsKey(stateName))
            return "Wait";
        if (string.Equals(def.InitialState, stateName, StringComparison.OrdinalIgnoreCase))
            return "Start";
        if (def.JoinTable.ContainsKey(stateName))
            return "Join";
        if (def.ForkTable.ContainsKey(stateName))
            return "Fork";
        if (def.Transitions.TryGetValue(stateName, out var byFact)
            && byFact.Values.Any(target => target.End))
            return "End";
        return "Task";
    }
}
