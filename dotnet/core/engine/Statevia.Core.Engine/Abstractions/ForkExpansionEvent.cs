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
public sealed record ForkExpansionEvent(
    string ExecutionId,
    string ForkState,
    string SourceNodeId,
    IReadOnlyList<ForkBranchExpansionPlan> Branches,
    object? WorkflowInput);

/// <summary>
/// 実行を Hosted Fork の枝（子 execution）として開始するときの指定。
/// </summary>
/// <remarks>
/// <para>枝の先頭の input は親の Context で 1 回だけ評価済みのため、子では再評価しない。
/// 子の Context で評価し直すと、親の Context を前提にしたパスが解決できず値を失う。</para>
/// <para>子の <c>$.input</c> は枝の input ではなく、親のワークフロー開始 input にそろえる。</para>
/// </remarks>
/// <param name="WorkflowInput">親のワークフロー開始 input。子の <c>$.input</c> になる。</param>
public sealed record ForkBranchStart(object? WorkflowInput);
