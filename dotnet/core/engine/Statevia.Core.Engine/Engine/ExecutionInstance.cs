using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Join;
using System.Collections.Concurrent;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 1 つのワークフローインスタンスの実行状態を保持します。
/// FSM、JoinTracker、ExecutionGraph、状態出力、アクティブ状態などを管理します。
/// </summary>
/// <remarks>
/// <para>実行中 Action にはインスタンスの Action CTS を渡す。<see cref="MarkCancelled"/> と <see cref="Dispose"/> がそのトークンをキャンセルする。</para>
/// </remarks>
public sealed class ExecutionInstance : IDisposable
{
    /// <summary>ワークフローインスタンス ID。</summary>
    public required string ExecutionId { get; init; }
    /// <summary>コンパイル済み定義。</summary>
    public required CompiledWorkflowDefinition Definition { get; init; }
    /// <summary>事実駆動 FSM。</summary>
    public required IFsm Fsm { get; init; }
    /// <summary>Join トラッカー。</summary>
    public required IJoinTracker JoinTracker { get; init; }
    /// <summary>実行グラフ（観測用）。</summary>
    public required ExecutionGraph Graph { get; init; }

    /// <summary>実行時データの Execution Context（パス評価根）。</summary>
    public WorkflowExecutionContext Context { get; private set; } = WorkflowExecutionContext.Create(null);

    // Engine 単体の Fork で、Join がまだ成立していない枝の範囲（ID → 範囲）。
    private readonly Dictionary<string, InProcessBranchScope> _liveBranchScopes = new(StringComparer.Ordinal);

    // ノード ID → 属する枝の範囲（_liveBranchScopes に残る範囲のノードだけ）。
    private readonly Dictionary<string, InProcessBranchScope> _nodeBranchScopes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, object?> _stateOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _stateAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<Guid, byte> _appliedPublishClientEventIds = new();
    private readonly ConcurrentDictionary<Guid, byte> _appliedCancelClientEventIds = new();
    private readonly CancellationTokenSource _actionCts = new();

    /// <summary>end 到達で完了したか。</summary>
    public bool IsCompleted { get; private set; }
    /// <summary>協調的キャンセルで停止したか。</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>
    /// Unload により破棄済みか。park 用の CT キャンセルと明示 Cancel を区別する。
    /// </summary>
    internal bool IsUnloaded { get; private set; }

    /// <summary>実行中 Action に渡す協調 Cancel 用トークン。Unload 後は破棄済み。</summary>
    internal CancellationToken ActionCancellationToken => _actionCts.Token;
    /// <summary>失敗で停止したか。</summary>
    public bool IsFailed { get; private set; }

    /// <summary>開始 input と実行メタデータで Context を初期化する（実行開始時に 1 回）。</summary>
    /// <param name="input"><see cref="IExecutionEngine.Start"/> に渡された開始 input。</param>
    public void InitializeContext(object? input)
    {
        Context = WorkflowExecutionContext.Create(input, ExecutionId, Definition.Name);
    }

    /// <summary>Hosted Fork の枝として Context を初期化する（実行開始時に 1 回）。</summary>
    /// <param name="branch">枝の開始指定。親から引き継ぐ vars / states と論理実行 ID を含む。</param>
    public void InitializeForkBranchContext(ForkBranchStart branch)
    {
        Context = WorkflowExecutionContext.CreateForkBranch(branch, Definition.Name);
    }

    /// <summary>
    /// 状態完了時の出力を記録し、Context の <c>states</c> を更新する。
    /// <see cref="CompiledWorkflowDefinition.StateOutputs"/> があれば <c>vars</c> にも代入する。
    /// </summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="output"><see cref="IStateExecutor.ExecuteAsync"/> の戻り値。</param>
    public void SetOutput(string stateName, object? output) => SetOutput(stateName, output, scope: null);

    /// <summary>
    /// 状態完了時の出力を記録し、範囲の Context の <c>states</c> / <c>vars</c> を更新する。
    /// </summary>
    /// <remarks>状態の output の一覧（グラフと checkpoint 用）は範囲によらず実行全体で持つ。</remarks>
    /// <param name="stateName">状態名。</param>
    /// <param name="output">状態の output。</param>
    /// <param name="scope">状態が属する枝の範囲。null なら実行の Context。</param>
    internal void SetOutput(string stateName, object? output, InProcessBranchScope? scope)
    {
        var context = ContextOf(scope);
        lock (_lock)
        {
            _stateOutputs[stateName] = output;
            context.SetStateOutput(stateName, output);
            if (Definition.StateOutputs.TryGetValue(stateName, out var varsPath))
            {
                context.SetVar(varsPath, output);
            }
        }
    }

    /// <summary>範囲の Context を返す。</summary>
    /// <param name="scope">枝の範囲。null なら実行の Context。</param>
    /// <returns>評価と書き込みに使う Context。</returns>
    internal WorkflowExecutionContext ContextOf(InProcessBranchScope? scope) => scope?.Context ?? Context;

    /// <summary>
    /// Engine 単体の Fork 到達で、枝ごとに範囲を作る（外側の範囲の Context を枝ごとに写す）。
    /// </summary>
    /// <param name="forkNodeId">Fork に到達したノードの ID。</param>
    /// <param name="joinState">対になる Join の状態名。</param>
    /// <param name="parent">外側の枝の範囲（最外は null）。</param>
    /// <param name="branchHeads">枝の先頭の状態名。</param>
    /// <returns>枝の先頭と同じ順の範囲。</returns>
    internal IReadOnlyList<InProcessBranchScope> BeginInProcessFork(
        string forkNodeId,
        string joinState,
        InProcessBranchScope? parent,
        IReadOnlyList<string> branchHeads)
    {
        var outer = ContextOf(parent);
        var episode = new InProcessForkEpisode(forkNodeId, joinState, parent);
        var scopes = branchHeads
            .Select(head => new InProcessBranchScope($"{forkNodeId}:{head}", episode, head, outer.CreateBranchCopy()))
            .ToList();
        lock (_lock)
        {
            foreach (var scope in scopes)
            {
                _liveBranchScopes[scope.Id] = scope;
            }
        }

        return scopes;
    }

    /// <summary>ノードが属する枝の範囲を記録する。</summary>
    /// <param name="nodeId">ノード ID。</param>
    /// <param name="scope">枝の範囲。null なら何もしない（実行の Context）。</param>
    internal void RegisterNodeScope(string nodeId, InProcessBranchScope? scope)
    {
        if (scope is null)
            return;
        lock (_lock)
        {
            _nodeBranchScopes[nodeId] = scope;
        }
    }

    /// <summary>ノードが属する枝の範囲を返す。</summary>
    /// <param name="nodeId">ノード ID。</param>
    /// <returns>枝の範囲。範囲の外のノードは null。</returns>
    internal InProcessBranchScope? GetNodeScope(string nodeId)
    {
        lock (_lock)
        {
            return _nodeBranchScopes.GetValueOrDefault(nodeId);
        }
    }

    /// <summary>Join が成立したエピソードの枝の範囲を手放す（以後 checkpoint に含めない）。</summary>
    /// <param name="episode">成立したエピソード。</param>
    internal void CompleteInProcessFork(InProcessForkEpisode episode)
    {
        lock (_lock)
        {
            foreach (var id in _liveBranchScopes.Where(kv => kv.Value.Episode == episode).Select(kv => kv.Key).ToList())
            {
                _liveBranchScopes.Remove(id);
            }

            foreach (var nodeId in _nodeBranchScopes.Where(kv => kv.Value.Episode == episode).Select(kv => kv.Key).ToList())
            {
                _nodeBranchScopes.Remove(nodeId);
            }
        }
    }

    /// <summary>指定状態の出力を取得する。</summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="output">取得した出力。存在しないときは既定値。</param>
    /// <returns>出力が存在するとき true。</returns>
    public bool TryGetOutput(string stateName, out object? output) { lock (_lock) { return _stateOutputs.TryGetValue(stateName, out output); } }

    /// <summary>状態の試行回数をインクリメントし、次の試行番号（1 始まり）を返す。</summary>
    /// <param name="stateName">状態名。</param>
    /// <returns>次の試行番号。</returns>
    public int NextAttempt(string stateName)
    {
        lock (_lock)
        {
            var next = _stateAttempts.TryGetValue(stateName, out var current) ? current + 1 : 1;
            _stateAttempts[stateName] = next;
            return next;
        }
    }

    /// <summary>現在アクティブな状態集合へ追加する。</summary>
    /// <param name="stateName">状態名。</param>
    public void AddActiveState(string stateName) { lock (_lock) { _activeStates.Add(stateName); } }

    /// <summary>現在アクティブな状態集合から除去する。</summary>
    /// <param name="stateName">状態名。</param>
    public void RemoveActiveState(string stateName) { lock (_lock) { _activeStates.Remove(stateName); } }

    /// <summary>現在アクティブな状態名のスナップショットを返す。</summary>
    /// <returns>アクティブ状態名の一覧。</returns>
    public IReadOnlyList<string> GetActiveStates() { lock (_lock) { return _activeStates.ToList(); } }

    /// <summary>ワークフローを正常終了としてマークする。</summary>
    /// <param name="terminalOutput">終端 State の output（Context.output に設定）。</param>
    public void MarkCompleted(object? terminalOutput = null)
    {
        Context.SetWorkflowOutput(terminalOutput);
        IsCompleted = true;
    }

    /// <summary>協調的キャンセルにより停止したとマークし、実行中 Action の CT をキャンセルする。</summary>
    public void MarkCancelled()
    {
        IsCancelled = true;
        HaltActiveActions();
    }

    /// <summary>失敗により停止したとマークする。</summary>
    public void MarkFailed() => IsFailed = true;

    /// <summary>
    /// 実行中 Action の CT をキャンセルし、並行する状態を止める。<see cref="IsCancelled"/> は変えない。
    /// </summary>
    /// <remarks>
    /// 終端失敗では並行する状態を止めつつ、利用者による取り消しとは記録しない（Cancelled と Failed を区別する）。
    /// Unload 後に呼ばれたときは何もしない。
    /// </remarks>
    public void HaltActiveActions()
    {
        try
        {
            _actionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Unload 後の二重停止は無視する
        }
    }

    /// <summary>
    /// Unload 時に Action 用 <see cref="CancellationTokenSource"/> をキャンセルして破棄する。
    /// </summary>
    /// <remarks>二重呼び出しは CTS 破棄済みとして無視する。</remarks>
    /// <summary>
    /// Action 用 <see cref="CancellationTokenSource"/> をキャンセルして破棄する。
    /// </summary>
    /// <remarks>Unload / Engine Dispose から呼ぶ。二重呼び出しは CTS 破棄済みとして無視する。</remarks>
    public void Dispose()
    {
        IsUnloaded = true;
        try
        {
            _actionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _actionCts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <paramref name="clientEventId"/> を Publish 冪等集合へ未登録なら登録し true。既登録なら false。
    /// </summary>
    internal bool TryRegisterPublishClientEventId(Guid clientEventId) =>
        _appliedPublishClientEventIds.TryAdd(clientEventId, 0);

    /// <summary>Publish 冪等集合から <paramref name="clientEventId"/> を取り除く（発行失敗時の巻き戻し用）。</summary>
    internal void RemovePublishClientEventId(Guid clientEventId) =>
        _appliedPublishClientEventIds.TryRemove(clientEventId, out _);

    /// <summary>
    /// <paramref name="clientEventId"/> を Cancel 冪等集合へ未登録なら登録し true。既登録なら false。
    /// </summary>
    internal bool TryRegisterCancelClientEventId(Guid clientEventId) =>
        _appliedCancelClientEventIds.TryAdd(clientEventId, 0);

    /// <summary>チェックポイント用にランタイム状態をエクスポートする。</summary>
    internal ExecutionRuntimeCheckpoint ExportRuntimeCheckpoint()
    {
        lock (_lock)
        {
            var pendingWaits = Graph.GetNodesSnapshot()
                .Where(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                    && n.CompletedAt is null)
                .Select(n => new CheckpointPendingWait
                {
                    NodeId = n.NodeId,
                    NodeName = n.NodeName,
                    AllowedEvents = n.AllowedEvents?.ToList() ?? [],
                    Subscriptions = n.Subscriptions?
                        .Select(s => new CheckpointWaitSubscription
                        {
                            Topic = s.Topic,
                            Key = s.Key,
                            ResumeEventName = s.ResumeEventName
                        })
                        .ToList()
                })
                .ToList();

            var joinData = JoinTracker is JoinTracker concreteJoin
                ? concreteJoin.ExportCheckpoint()
                : new CheckpointJoinData
                {
                    JoinStateResults = new Dictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>>(),
                    JoinSourceNodeIds = new Dictionary<string, IReadOnlyDictionary<string, string>>(),
                    StartedJoins = []
                };

            return new ExecutionRuntimeCheckpoint
            {
                SchemaVersion = ExecutionRuntimeCheckpoint.CurrentSchemaVersion,
                ExecutionId = ExecutionId,
                DefinitionName = Definition.Name,
                IsCompleted = IsCompleted,
                IsCancelled = IsCancelled,
                IsFailed = IsFailed,
                ActiveStates = _activeStates.ToList(),
                StateAttempts = new Dictionary<string, int>(_stateAttempts, StringComparer.OrdinalIgnoreCase),
                StateOutputs = _stateOutputs.ToDictionary(
                    kv => kv.Key,
                    kv => CheckpointJson.ToElement(kv.Value),
                    StringComparer.OrdinalIgnoreCase),
                AppliedPublishClientEventIds = _appliedPublishClientEventIds.Keys.ToList(),
                AppliedCancelClientEventIds = _appliedCancelClientEventIds.Keys.ToList(),
                Context = Context.ExportCheckpoint(),
                Graph = Graph.ExportCheckpoint(),
                Join = joinData,
                PendingWaits = pendingWaits,
                BranchScopes = ExportBranchScopesUnlocked(),
                NodeBranchScopeIds = _nodeBranchScopes.ToDictionary(kv => kv.Key, kv => kv.Value.Id, StringComparer.Ordinal)
            };
        }
    }

    private List<CheckpointBranchScope> ExportBranchScopesUnlocked() =>
        _liveBranchScopes.Values
            .Select(scope => new CheckpointBranchScope
            {
                Id = scope.Id,
                ForkNodeId = scope.Episode.ForkNodeId,
                BranchHead = scope.BranchHead,
                JoinState = scope.Episode.JoinState,
                ParentScopeId = scope.Episode.Parent?.Id,
                Context = scope.Context.ExportCheckpoint(),
                ArrivalOrder = scope.Episode.GetArrivalOrder(scope)
            })
            .ToList();

    /// <summary>
    /// checkpoint の枝の範囲を復元する。外側の範囲から順に作り、エピソードごとに Join への到着順を戻す。
    /// </summary>
    /// <exception cref="InvalidOperationException">外側の範囲が一覧に無い、またはノードが一覧に無い範囲を指すとき。</exception>
    private void ImportBranchScopesUnlocked(ExecutionRuntimeCheckpoint checkpoint)
    {
        _liveBranchScopes.Clear();
        _nodeBranchScopes.Clear();
        var pending = (checkpoint.BranchScopes ?? []).ToList();
        var episodes = new Dictionary<string, InProcessForkEpisode>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(data => data.ParentScopeId is null || _liveBranchScopes.ContainsKey(data.ParentScopeId))
                .ToList();
            if (ready.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Checkpoint branch scope '{pending[0].Id}' refers to a missing parent scope '{pending[0].ParentScopeId}'.");
            }

            foreach (var data in ready)
            {
                if (!episodes.TryGetValue(data.ForkNodeId, out var episode))
                {
                    var parent = data.ParentScopeId is null ? null : _liveBranchScopes[data.ParentScopeId];
                    episode = new InProcessForkEpisode(data.ForkNodeId, data.JoinState, parent);
                    episodes[data.ForkNodeId] = episode;
                }

                var context = WorkflowExecutionContext.CreateFromCheckpoint(data.Context, ExecutionId, Definition.Name);
                _liveBranchScopes[data.Id] = new InProcessBranchScope(data.Id, episode, data.BranchHead, context);
                pending.Remove(data);
            }
        }

        foreach (var data in (checkpoint.BranchScopes ?? [])
                     .Where(data => data.ArrivalOrder is not null)
                     .OrderBy(data => data.ArrivalOrder))
        {
            var scope = _liveBranchScopes[data.Id];
            scope.Episode.TryArrive(scope);
        }

        foreach (var (nodeId, scopeId) in checkpoint.NodeBranchScopeIds ?? new Dictionary<string, string>())
        {
            if (!_liveBranchScopes.TryGetValue(scopeId, out var scope))
            {
                throw new InvalidOperationException(
                    $"Checkpoint node '{nodeId}' refers to a missing branch scope '{scopeId}'.");
            }

            _nodeBranchScopes[nodeId] = scope;
        }
    }

    /// <summary>チェックポイントから可変ランタイム状態を適用する（Definition / Fsm / Graph 器は既存）。</summary>
    /// <param name="checkpoint">チェックポイント。</param>
    internal void ApplyRuntimeCheckpoint(ExecutionRuntimeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        lock (_lock)
        {
            IsCompleted = checkpoint.IsCompleted;
            IsCancelled = checkpoint.IsCancelled;
            IsFailed = checkpoint.IsFailed;
            _activeStates.Clear();
            foreach (var state in checkpoint.ActiveStates)
            {
                _activeStates.Add(state);
            }

            _stateAttempts.Clear();
            foreach (var (name, attempt) in checkpoint.StateAttempts)
            {
                _stateAttempts[name] = attempt;
            }

            _stateOutputs.Clear();
            foreach (var (name, output) in checkpoint.StateOutputs)
            {
                _stateOutputs[name] = CheckpointJson.FromElement(output);
            }

            _appliedPublishClientEventIds.Clear();
            foreach (var id in checkpoint.AppliedPublishClientEventIds)
            {
                _appliedPublishClientEventIds[id] = 0;
            }

            _appliedCancelClientEventIds.Clear();
            foreach (var id in checkpoint.AppliedCancelClientEventIds)
            {
                _appliedCancelClientEventIds[id] = 0;
            }

            Context = WorkflowExecutionContext.CreateFromCheckpoint(
                checkpoint.Context,
                ExecutionId,
                Definition.Name);
            Graph.ImportFromCheckpoint(checkpoint.Graph);
            if (JoinTracker is JoinTracker concreteJoin)
            {
                concreteJoin.ImportFromCheckpoint(checkpoint.Join);
            }

            ImportBranchScopesUnlocked(checkpoint);
        }
    }
}
