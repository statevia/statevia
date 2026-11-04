namespace Statevia.Core.Engine.Engine;

/// <summary>
/// Engine 単体（Fork 展開ハンドラ未登録）で、1 回の Fork 到達から対になる Join の成立までの並列エピソード。
/// </summary>
/// <remarks>
/// <para>枝が Join に入った順（＝枝の完了順）を保持する。Join の成立時、この順に各枝の書き込みを外側の Context へマージする。</para>
/// <para>同じ枝の 2 回目の到着（checkpoint から復元して同じ遷移をやり直した場合）は記録しない。</para>
/// </remarks>
/// <param name="forkNodeId">Fork に到達したノードの ID（エピソードの相関キー。Hosted の forkNodeId と同じ）。</param>
/// <param name="joinState">対になる Join の状態名（<c>join.all</c> が Fork の枝の集合と一致するもの）。</param>
/// <param name="parent">外側の枝の範囲。最外の Fork では null（実行の Context）。</param>
internal sealed class InProcessForkEpisode(string forkNodeId, string joinState, InProcessBranchScope? parent)
{
    private readonly object _lock = new();
    private readonly List<InProcessBranchScope> _arrivals = [];

    /// <summary>Fork に到達したノードの ID。</summary>
    public string ForkNodeId { get; } = forkNodeId;

    /// <summary>対になる Join の状態名。</summary>
    public string JoinState { get; } = joinState;

    /// <summary>外側の枝の範囲（最外は null）。</summary>
    public InProcessBranchScope? Parent { get; } = parent;

    /// <summary>
    /// 枝の Join への到着を記録する。
    /// </summary>
    /// <param name="scope">到着した枝。</param>
    /// <returns>初めての到着なら true。既に到着済みなら false。</returns>
    public bool TryArrive(InProcessBranchScope scope)
    {
        lock (_lock)
        {
            if (_arrivals.Contains(scope))
                return false;
            _arrivals.Add(scope);
            return true;
        }
    }

    /// <summary>Join に到着した枝を到着順に返す。</summary>
    /// <returns>到着順の枝の範囲。</returns>
    public IReadOnlyList<InProcessBranchScope> GetArrivals()
    {
        lock (_lock)
        {
            return _arrivals.ToList();
        }
    }

    /// <summary>枝の到着順（0 始まり）。未到着なら null。</summary>
    /// <param name="scope">枝の範囲。</param>
    /// <returns>到着順。</returns>
    public int? GetArrivalOrder(InProcessBranchScope scope)
    {
        lock (_lock)
        {
            var index = _arrivals.IndexOf(scope);
            return index < 0 ? null : index;
        }
    }
}

/// <summary>
/// Engine 単体の Fork の 1 つの枝の範囲。枝の中の評価と書き込みは <see cref="Context"/> に閉じる。
/// </summary>
/// <remarks>
/// <para><see cref="Context"/> は Fork 到達時点の外側の Context の写しから始まり、兄弟の枝とは共有しない（fork-join.md の兄弟参照不可）。</para>
/// <para>同じ枝の後続の状態は同じ範囲を受け継ぐ。枝の中の Fork は、この範囲を外側に持つ内側のエピソードを作る。</para>
/// </remarks>
/// <param name="id">checkpoint の中で範囲を識別する ID（<c>forkNodeId:branchHead</c>）。</param>
/// <param name="episode">属する並列エピソード。</param>
/// <param name="branchHead">枝の先頭の状態名（<c>join.all</c> のキー）。</param>
/// <param name="context">この枝の Context。</param>
internal sealed class InProcessBranchScope(
    string id,
    InProcessForkEpisode episode,
    string branchHead,
    WorkflowExecutionContext context)
{
    /// <summary>checkpoint の中で範囲を識別する ID。</summary>
    public string Id { get; } = id;

    /// <summary>属する並列エピソード。</summary>
    public InProcessForkEpisode Episode { get; } = episode;

    /// <summary>枝の先頭の状態名。</summary>
    public string BranchHead { get; } = branchHead;

    /// <summary>この枝の Context。</summary>
    public WorkflowExecutionContext Context { get; } = context;
}
