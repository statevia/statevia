import type { ExecutionNodeDTO } from "../types";

/** 同一状態の訪問ナビで、今押せる移動。 */
export type NodeVisitNavigationState = {
  /** 訪問が 2 件以上で、選択がその中にあるときだけ true。 */
  visible: boolean;
  /** 選択中訪問の attempt。ナビに出すメタはこれだけ。 */
  attempt: number;
  /** 最新へ移せるか。すでに最新なら false。 */
  canJumpToLatest: boolean;
  /** 最新側へ 1 件移せるか。 */
  canStepNewer: boolean;
  /** 古い側へ 1 件移せるか。 */
  canStepOlder: boolean;
  /** 1 回目へ移せるか。すでに 1 回目なら false。 */
  canJumpToFirst: boolean;
  /** 最新訪問の nodeId。無ければ null。 */
  latestNodeId: string | null;
  /** 最新側の隣の nodeId。無ければ null。 */
  newerNodeId: string | null;
  /** 古い側の隣の nodeId。無ければ null。 */
  olderNodeId: string | null;
  /** 1 回目の nodeId。無ければ null。 */
  firstNodeId: string | null;
};

/**
 * 状態名を訪問の照合キーにする。前後空白と大文字小文字は区別しない。
 *
 * @param value 実行ノードまたは選択キーの名前。
 * @returns 空なら空文字。
 */
function normalizeNodeName(value: string | null | undefined): string {
  return typeof value === "string" ? value.trim().toLowerCase() : "";
}

/**
 * 同一 nodeName の訪問を attempt 昇順で返す。
 *
 * 同一 attempt は、渡された配列の後ろを新しい側にする。代表選択はここでは行わない。
 *
 * @param nodes 実行 View の nodes。
 * @param nodeNameKey 状態名。空なら空配列。
 * @returns 0 件以上。先頭が 1 回目側、末尾が最新。
 */
export function listNodeVisits(
  nodes: readonly ExecutionNodeDTO[],
  nodeNameKey: string
): ExecutionNodeDTO[] {
  const key = normalizeNodeName(nodeNameKey);
  if (key.length === 0) return [];

  const indexed = nodes
    .map((node, index) => ({ node, index }))
    .filter(({ node }) => normalizeNodeName(node.nodeName) === key);

  indexed.sort((left, right) => {
    if (left.node.attempt !== right.node.attempt) return left.node.attempt - right.node.attempt;
    return left.index - right.index;
  });

  return indexed.map(({ node }) => node);
}

/**
 * キャンバスや一覧で状態を選んだときの初期 nodeId。
 *
 * 実行ノードがあるときは、その状態の最新訪問を返す。無いときは選択キーをそのまま返す。
 *
 * @param nodes 実行 View の nodes。
 * @param selectionKey 定義上の状態名、または実行ノード ID。
 * @returns 詳細と Resume に使う ID。
 */
export function resolveInitialSelectionNodeId(
  nodes: readonly ExecutionNodeDTO[],
  selectionKey: string
): string {
  const exact = nodes.find((node) => node.nodeId === selectionKey);
  const nameKey = exact?.nodeName && exact.nodeName.trim().length > 0 ? exact.nodeName : selectionKey;
  const latest = listNodeVisits(nodes, nameKey).at(-1);
  return latest?.nodeId ?? selectionKey;
}

/**
 * スナップショット更新後の選択。残っている ID は維持し、消えたときだけ同じ状態の最新へ移す。
 *
 * 以前の状態名が無く、ID も nodes に無いときは、定義だけの選択として ID を維持する。
 * 以前の状態の訪問が残っていなければ選択を外す。
 *
 * @param nodes 更新後の nodes。
 * @param selectedNodeId 現在の選択。未選択は null。
 * @param previousNodeName 直前まで見ていた状態名。未確定は null。
 * @returns 次に保持する nodeId と状態名。
 */
export function resolveVisitAfterRefresh(
  nodes: readonly ExecutionNodeDTO[],
  selectedNodeId: string | null,
  previousNodeName: string | null
): { nodeId: string | null; nodeName: string | null } {
  if (!selectedNodeId) return { nodeId: null, nodeName: null };

  const exact = nodes.find((node) => node.nodeId === selectedNodeId);
  if (exact) {
    const nodeName = exact.nodeName && exact.nodeName.trim().length > 0 ? exact.nodeName : previousNodeName;
    return { nodeId: exact.nodeId, nodeName };
  }

  if (!previousNodeName) return { nodeId: selectedNodeId, nodeName: null };

  const latest = listNodeVisits(nodes, previousNodeName).at(-1);
  if (!latest) return { nodeId: null, nodeName: null };
  return { nodeId: latest.nodeId, nodeName: latest.nodeName ?? previousNodeName };
}

/**
 * Resume に送る実行ノード ID。選択中の訪問が同じ状態ならそれを使い、違えばその状態の最新を使う。
 *
 * 対象の訪問が残っていなければ null。消えた ID は返さない。
 *
 * @param nodes 実行 View の nodes。
 * @param requestedId キャンバスが渡した代表 ID または状態名。
 * @param selectedNodeId 詳細で選んでいる実行ノード ID。未選択は null。
 * @returns Resume API の nodeId。送れないときは null。
 */
export function resolveResumeTargetNodeId(
  nodes: readonly ExecutionNodeDTO[],
  requestedId: string,
  selectedNodeId: string | null
): string | null {
  const selected = selectedNodeId ? nodes.find((node) => node.nodeId === selectedNodeId) : undefined;
  const requested = nodes.find((node) => node.nodeId === requestedId);
  const nameKey = requested?.nodeName && requested.nodeName.trim().length > 0 ? requested.nodeName : requestedId;
  const visits = listNodeVisits(nodes, nameKey);

  if (selected && visits.some((visit) => visit.nodeId === selected.nodeId)) return selected.nodeId;

  const latest = visits.at(-1);
  if (latest) return latest.nodeId;
  return requested ? requested.nodeId : null;
}

/**
 * キャンバスの 1 ノードを、選択中の訪問に対して選択表示するか。
 *
 * ノードの複製はしない。status の色は呼び出し側の代表ノードのままにする。
 *
 * @param canvas 合成後の 1 ノード。
 * @param selectedNodeId 選択中 ID。未選択は null。
 * @param selectedNodeName 選択中訪問の状態名。未確定は null。
 * @returns そのキャンバスノードを選択表示するとき true。
 */
export function canvasNodeShowsSelection(
  canvas: { name: string; nodeName: string; nodeId: string },
  selectedNodeId: string | null,
  selectedNodeName: string | null
): boolean {
  if (!selectedNodeId) return false;
  if (selectedNodeId === canvas.name || selectedNodeId === canvas.nodeId) return true;

  const selectedName = normalizeNodeName(selectedNodeName);
  if (selectedName.length === 0) return false;
  return normalizeNodeName(canvas.nodeName) === selectedName || normalizeNodeName(canvas.name) === selectedName;
}

/**
 * 訪問配列と選択中 ID から、四ボタンの可否と移動先を返す。
 *
 * @param visits attempt 昇順の訪問。
 * @param selectedNodeId 選択中の実行ノード ID。
 * @returns 2 件未満、または選択が配列に無いときは visible が false。
 */
export function buildNodeVisitNavigation(
  visits: readonly ExecutionNodeDTO[],
  selectedNodeId: string | null
): NodeVisitNavigationState {
  const index = selectedNodeId ? visits.findIndex((visit) => visit.nodeId === selectedNodeId) : -1;
  const visible = visits.length > 1 && index >= 0;
  const atLatest = index === visits.length - 1;
  const atFirst = index === 0;

  return {
    visible,
    attempt: index >= 0 ? visits[index].attempt : 0,
    canJumpToLatest: visible && !atLatest,
    canStepNewer: visible && !atLatest,
    canStepOlder: visible && !atFirst,
    canJumpToFirst: visible && !atFirst,
    latestNodeId: visits.at(-1)?.nodeId ?? null,
    newerNodeId: index >= 0 && index < visits.length - 1 ? visits[index + 1].nodeId : null,
    olderNodeId: index > 0 ? visits[index - 1].nodeId : null,
    firstNodeId: visits[0]?.nodeId ?? null
  };
}
