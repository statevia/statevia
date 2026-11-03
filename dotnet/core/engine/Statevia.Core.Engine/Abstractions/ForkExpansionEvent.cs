namespace Statevia.Core.Engine.Abstractions;

/// <summary>Fork 物理展開ハンドラへ渡す 1 分岐分の計画。</summary>
/// <param name="BranchState">分岐先頭状態名。</param>
/// <param name="MappedInput">親 Context で評価済みの入力。</param>
public sealed record ForkBranchExpansionPlan(string BranchState, object? MappedInput);

/// <summary>
/// Hosted Runtime が論理 Fork の代わりに子 execution を起動するときの通知。
/// </summary>
/// <remarks>
/// <para>ハンドラ未登録時は Engine が従来どおり同一インスタンス上で分岐を Schedule する。</para>
/// <para>ハンドラ登録時は Engine は分岐を Schedule せず、本イベントのみ発火する。</para>
/// </remarks>
/// <param name="ExecutionId">親 execution ID（Engine 上の文字列 ID）。</param>
/// <param name="ForkState">Fork 遷移元の状態名（ForkTable キー）。</param>
/// <param name="SourceNodeId">Fork 遷移を起こしたグラフノード ID。</param>
/// <param name="Branches">分岐先頭と写像済み input。</param>
/// <param name="WorkflowInput">親のワークフロー開始 input（<c>$.input</c>）。子 execution の <c>$.input</c> になる。</param>
/// <param name="ParentContext">Fork 到達時点の親の <c>vars</c> / <c>states</c> の写しと論理実行 ID。各子が引き継ぐ。</param>
public sealed record ForkExpansionEvent(
    string ExecutionId,
    string ForkState,
    string SourceNodeId,
    IReadOnlyList<ForkBranchExpansionPlan> Branches,
    object? WorkflowInput,
    ForkParentContext ParentContext);

/// <summary>
/// Fork 到達時点の親の Execution Context のうち、各枝（子 execution）が引き継ぐ部分。
/// </summary>
/// <remarks>
/// <para>Fork 到達時に 1 回だけ写し取る。各枝は同じ写しから始まり、以後は独立する（兄弟の書き込みは見えない）。</para>
/// </remarks>
/// <param name="Vars">親の <c>vars</c> の写し（辞書ツリー、または同形の <see cref="System.Text.Json.JsonElement"/>）。</param>
/// <param name="States">親の <c>states</c> の写し（状態名 → <c>{ output }</c> の辞書、または同形の <see cref="System.Text.Json.JsonElement"/>）。</param>
/// <param name="LogicalExecutionId">
/// 論理実行 ID（親の <c>$.sys.execution.id</c>）。枝の <c>$.sys.execution.id</c> になる。ネストした Fork では根の ID のまま渡る。
/// </param>
public sealed record ForkParentContext(
    object? Vars,
    object? States,
    string LogicalExecutionId);

/// <summary>
/// 実行を Hosted Fork の枝（子 execution）として開始するときの指定。
/// </summary>
/// <remarks>
/// <para>枝の先頭の input は親の Context で 1 回だけ評価済みのため、子では再評価しない。
/// 子の Context で評価し直すと、親の Context を前提にしたパスが解決できず値を失う。</para>
/// <para>子の <c>$.input</c> は枝の input ではなく、親のワークフロー開始 input にそろえる。</para>
/// <para>子の <c>vars</c> / <c>states</c> は <paramref name="ParentContext"/> で初期化する。引き継いだ値は「枝が書いた値」に数えず、Join で親へ戻さない。</para>
/// </remarks>
/// <param name="WorkflowInput">親のワークフロー開始 input。子の <c>$.input</c> になる。</param>
/// <param name="ParentContext">Fork 到達時点の親の Context の写し。</param>
public sealed record ForkBranchStart(object? WorkflowInput, ForkParentContext ParentContext);
