import type { GraphDefinition, GraphEdgeDef, GraphGroupDef, GraphDefinitionMeta } from "@/features/executions/graphs/types";
import type { ExecutionNodeDTO, NodeStatus, ExecutionView } from "../types";
import { pickPreferredRuntimeNode } from "./pickPreferredRuntimeNode";

/** 実行＋定義をマージしたグラフノード。 */
export type MergedGraphNode = {
  /** 定義グラフ上のノード名（GraphNodeDef.nodeName）。エッジ・レイアウト・グループのキー。 */
  name: string;
  /** ExecutionGraph のノード ID（差分ハイライト・ランタイム行と対応）。定義のみの IDLE 行では `name` と同一の合成値。 */
  nodeId: string;
  /** 定義グラフ・API の状態名（実行ノードに nodeName があればそれを優先） */
  nodeName: string;
  /** ノードの種別。 */
  nodeType: string;
  /** 表示ラベル。 */
  label: string;
  /** 属する分岐の名前。無ければ分岐の外。 */
  branch?: string;
  /** ノードのステータス。定義だけのノードは IDLE。 */
  status: NodeStatus;
  /** 試行回数。 */
  attempt: number;
  /** ランタイム行がマージされたときのワーカー ID（定義のみの IDLE 行では null）。 */
  workerId: string | null;
  /** Wait の待機キー。無ければ null。 */
  waitKey: string | null;
  /** Wait が受付可能なイベント名一覧（任意）。 */
  allowedEvents?: string[] | null;
  /** 実行のキャンセルで止まったノードか。 */
  canceledByExecution: boolean;
};

/** 実行＋定義をマージしたグラフ辺。 */
export type MergedGraphEdge = {
  /** 辺の ID。 */
  id: string;
  /** 始点のノード名。 */
  from: string;
  /** 終点のノード名。 */
  to: string;
  /** 辺の種類（通常・fork・join）。 */
  kind?: "normal" | "fork" | "join";
  /** 遷移の種類。省略時は Next。 */
  edgeType?: "Next" | "Resume" | "Cancel";
  /** Resume の辺に表示するイベント名。 */
  eventName?: string;
  /** Cancel の辺に表示する reason。 */
  cancelReason?: string;
  /** Cancel の辺に表示する cause。 */
  cancelCause?: string;
  /** 実行で通った辺か。 */
  traversed?: boolean;
};

/** マージ済みグラフ全体。 */
export type MergedGraph = {
  /** グラフ ID。 */
  graphId: string;
  /** マージしたノード。 */
  nodes: MergedGraphNode[];
  /** マージした辺。 */
  edges: MergedGraphEdge[];
  /** ノードのグループ。 */
  groups?: GraphGroupDef[];
  /** 配置のメタ情報。 */
  meta?: GraphDefinitionMeta;
  /** 定義のグラフを元に組み立てたか（false なら実行のノードだけ）。 */
  isDefinitionBased: boolean;
};

/** 定義のみ存在するノード用。`name` は定義上のノード名、`nodeName` はワークフロー状態名（通常は同一値）。 */
function asIdleNode(name: string, nodeName: string, nodeType: string): ExecutionNodeDTO {
  return {
    nodeId: name,
    nodeName,
    nodeType,
    status: "IDLE",
    attempt: 0,
    workerId: null,
    waitKey: null,
    allowedEvents: null,
    canceledByExecution: false
  };
}

function toEdge(edge: GraphEdgeDef, index: number): MergedGraphEdge {
  return {
    id: `e-${edge.from}-${edge.to}-${index}`,
    from: edge.from,
    to: edge.to,
    kind: edge.kind,
    edgeType: edge.edgeType,
    eventName: edge.eventName,
    cancelReason: edge.cancelReason,
    cancelCause: edge.cancelCause,
    traversed: false
  };
}

/** 実行グラフと定義グラフをマージする。 */
export function mergeGraph(execution: ExecutionView, definition: GraphDefinition | null): MergedGraph {
  const byRuntimeId = new Map<string, ExecutionNodeDTO>();
  const byNodeNameKey = new Map<string, ExecutionNodeDTO>();
  const nodeNameByRuntimeId = new Map<string, string>();
  for (const n of execution.nodes) {
    byRuntimeId.set(n.nodeId, n);
    const trimmed = typeof n.nodeName === "string" ? n.nodeName.trim() : "";
    if (trimmed.length === 0) continue;
    nodeNameByRuntimeId.set(n.nodeId, trimmed);
    // 循環で同名が複数あるときは WAITING / 最新 attempt を代表にする。
    const existing = byNodeNameKey.get(trimmed);
    if (!existing) {
      byNodeNameKey.set(trimmed, n);
      continue;
    }
    const preferred = pickPreferredRuntimeNode([existing, n], trimmed);
    if (preferred) byNodeNameKey.set(trimmed, preferred);
  }

  const traversedEdgeKeys = new Set(
    (execution.runtimeEdges ?? []).flatMap((edge) => {
      const directKey = `${edge.from}->${edge.to}`;
      const fromState = nodeNameByRuntimeId.get(edge.from);
      const toState = nodeNameByRuntimeId.get(edge.to);
      if (fromState === undefined || toState === undefined) return [directKey];
      return [directKey, `${fromState}->${toState}`];
    })
  );
  if (!definition) {
    return {
      graphId: execution.graphId,
      nodes: execution.nodes.map((n) => ({
        name: n.nodeId,
        nodeId: n.nodeId,
        nodeName: n.nodeName ?? "",
        nodeType: n.nodeType,
        label: n.nodeId,
        status: n.status,
        attempt: n.attempt,
        workerId: n.workerId,
        waitKey: n.waitKey,
        allowedEvents: n.allowedEvents ?? null,
        canceledByExecution: n.canceledByExecution
      })),
      edges: [],
      groups: [],
      meta: { direction: "TB" },
      isDefinitionBased: false
    };
  }

  const nodes = definition.nodes.map((defNode) => {
    const definitionNodeName = defNode.nodeName.trim();

    // 定義グラフのノード名（＝ワークフロー状態名）で実行ノードを突き合わせる。
    // 突き合わせ不可（旧データ等）の際のみ実行ノード ID 直接一致にフォールバックする。
    const runtimeNode =
      byNodeNameKey.get(definitionNodeName) ??
      byRuntimeId.get(defNode.nodeName) ??
      asIdleNode(defNode.nodeName, definitionNodeName, defNode.nodeType);

    const resolvedNodeName =
      typeof runtimeNode.nodeName === "string" && runtimeNode.nodeName.trim().length > 0
        ? runtimeNode.nodeName.trim()
        : definitionNodeName;

    return {
      name: defNode.nodeName,
      nodeId: runtimeNode.nodeId,
      nodeName: resolvedNodeName,
      nodeType: defNode.nodeType,
      label: defNode.label ?? defNode.nodeName,
      branch: defNode.branch,
      status: runtimeNode.status,
      attempt: runtimeNode.attempt,
      workerId: runtimeNode.workerId,
      waitKey: runtimeNode.waitKey,
      allowedEvents: runtimeNode.allowedEvents ?? null,
      canceledByExecution: runtimeNode.canceledByExecution
    };
  });

  return {
    graphId: definition.graphId,
    nodes,
    edges: definition.edges.map((defEdge, index) => toEdge(defEdge, index)).map((edge) => ({
      ...edge,
      traversed: traversedEdgeKeys.has(`${edge.from}->${edge.to}`)
    })),
    groups: definition.groups ?? [],
    meta: definition.meta,
    isDefinitionBased: true
  };
}
