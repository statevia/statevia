import type { LayoutNodeInput } from "@/shared/lib/graphLayout";
import type { DefinitionGraphDocument, DefinitionGraphNode, NodeType } from "./types";

/** React Flow ノードに載せる定義グラフの表示データ。 */
export type DefinitionGraphNodeData = {
  /** ノード種別。layout とハンドル位置の判定に使う。 */
  nodeType: string;
  /** ノード名。 */
  label: string;
  /** React Flow の計測・ハンドル位置と一致させる（layoutGraph の w/h と同一）。 */
  width: number;
  height: number;
};

/** キャンバス上の選択。未選択は null。 */
export type GraphSelection =
  | { kind: "node"; nodeName: string }
  | {
      kind: "edge";
      nodeName: string;
      edgeKind: "next" | "edge" | "error" | "waitEvent" | "waitSubscribe";
      edgeIndex?: number;
      eventName?: string;
      subscribeIndex?: number;
    }
  | null;

/** 追加できるノード種別と、追加できない理由。 */
export type AvailableNodeType = {
  /** 追加する種別。 */
  type: NodeType;
  /** 既に start / end があるときは true。 */
  disabled: boolean;
  /** 追加できない理由。できるときは undefined。 */
  reason?: string;
};

/** キャンバス辺のメタデータ。インスペクタの選択復元に使う。 */
export type GraphEdgeMeta = {
  id: string;
  source: string;
  target: string;
  edgeKind: "next" | "edge" | "branch" | "error" | "waitEvent" | "waitSubscribe";
  edgeIndex?: number;
  eventName?: string;
  subscribeIndex?: number;
  parallelIndex?: number;
  parallelCount?: number;
};

/** Wait インスペクタの編集モード。 */
export type WaitEditMode = "events" | "subscribe" | "legacy" | "conflict";

type ParallelEdgeCollector = {
  trackParallel: (edgeMeta: GraphEdgeMeta) => void;
  finalize: () => GraphEdgeMeta[];
};

/**
 * レイアウト入力へノード名と種別を写す。
 * @param document 定義グラフ。
 * @returns layoutGraph に渡すノード。
 */
export function toLayoutNodes(document: DefinitionGraphDocument): LayoutNodeInput[] {
  return document.nodes.map((node) => ({
    name: node.name,
    nodeType: node.type.toUpperCase()
  }));
}

function createParallelEdgeCollector(): ParallelEdgeCollector {
  const edges: GraphEdgeMeta[] = [];
  const parallelKeyToIndices = new Map<string, number[]>();

  const trackParallel = (edgeMeta: GraphEdgeMeta): void => {
    const edgeIndex = edges.length;
    edges.push(edgeMeta);
    const key = `${edgeMeta.source}=>${edgeMeta.target}`;
    const list = parallelKeyToIndices.get(key);
    if (list) {
      list.push(edgeIndex);
    } else {
      parallelKeyToIndices.set(key, [edgeIndex]);
    }
  };

  const finalize = (): GraphEdgeMeta[] => {
    for (const indices of parallelKeyToIndices.values()) {
      if (indices.length < 2) {
        continue;
      }
      for (let index = 0; index < indices.length; index += 1) {
        const edge = edges[indices[index]];
        edge.parallelIndex = index;
        edge.parallelCount = indices.length;
      }
    }
    return edges;
  };

  return { trackParallel, finalize };
}

function appendWaitEventGraphEdges(
  node: DefinitionGraphNode,
  trackParallel: (edgeMeta: GraphEdgeMeta) => void
): void {
  if (node.type !== "wait" || !node.events) {
    return;
  }
  for (const [eventName, target] of Object.entries(node.events)) {
    const trimmedTarget = target?.trim();
    const trimmedEvent = eventName.trim();
    if (!trimmedTarget || !trimmedEvent) {
      continue;
    }
    trackParallel({
      id: `waitEvent:${node.name}:${trimmedEvent}`,
      source: node.name,
      target: trimmedTarget,
      edgeKind: "waitEvent",
      eventName: trimmedEvent
    });
  }
}

/**
 * Subscribe 行からキャンバス辺を出す。id は配列 index（topic は重複し得る）。
 * @param node 起点ノード。
 * @param trackParallel 並列辺コレクタ。
 * @param untitledTopic 空 topic のラベル。
 */
function appendWaitSubscribeGraphEdges(
  node: DefinitionGraphNode,
  trackParallel: (edgeMeta: GraphEdgeMeta) => void,
  untitledTopic: string
): void {
  if (node.type !== "wait" || node.subscribe === undefined) {
    return;
  }
  node.subscribe.forEach((entry, index) => {
    const trimmedTarget = entry.next?.trim();
    if (!trimmedTarget) {
      return;
    }
    const topicLabel = entry.topic.trim().length > 0 ? entry.topic.trim() : untitledTopic;
    trackParallel({
      id: `waitSubscribe:${node.name}:${index}`,
      source: node.name,
      target: trimmedTarget,
      edgeKind: "waitSubscribe",
      subscribeIndex: index,
      eventName: topicLabel
    });
  });
}

function appendNodeGraphEdges(
  node: DefinitionGraphNode,
  trackParallel: (edgeMeta: GraphEdgeMeta) => void,
  untitledTopic: string
): void {
  if (node.type === "action" && node.error?.trim()) {
    trackParallel({
      id: `error:${node.name}`,
      source: node.name,
      target: node.error.trim(),
      edgeKind: "error"
    });
  }
  if (node.next?.trim()) {
    trackParallel({
      id: `next:${node.name}`,
      source: node.name,
      target: node.next.trim(),
      edgeKind: "next"
    });
  }
  for (const [index, edge] of (node.edges ?? []).entries()) {
    if (!edge.to?.trim()) {
      continue;
    }
    trackParallel({
      id: `edge:${node.name}:${index}`,
      source: node.name,
      target: edge.to.trim(),
      edgeKind: "edge",
      edgeIndex: index
    });
  }
  for (const [index, branch] of (node.branches ?? []).entries()) {
    if (!branch?.trim()) {
      continue;
    }
    trackParallel({
      id: `branch:${node.name}:${index}`,
      source: node.name,
      target: branch.trim(),
      edgeKind: "branch",
      edgeIndex: index
    });
  }
  appendWaitEventGraphEdges(node, trackParallel);
  appendWaitSubscribeGraphEdges(node, trackParallel, untitledTopic);
}

/**
 * 定義グラフからキャンバス辺を作る。同じ始点と終点の辺は平行にずらす。
 * @param document 定義グラフ。
 * @param untitledTopic 空 topic のラベル。
 * @returns 辺メタデータ。
 */
export function toGraphEdges(document: DefinitionGraphDocument, untitledTopic: string): GraphEdgeMeta[] {
  const collector = createParallelEdgeCollector();
  for (const node of document.nodes) {
    appendNodeGraphEdges(node, collector.trackParallel, untitledTopic);
  }
  return collector.finalize();
}

/**
 * 重なる辺の描画オフセット。error は下、条件辺は上へずらす。
 * @param edge 辺メタデータ。
 * @returns React Flow の path offset。
 */
export function edgeOffsetForRendering(edge: GraphEdgeMeta): number {
  let kindBaseOffset = 0;
  if (edge.edgeKind === "error") {
    kindBaseOffset = 36;
  } else if (edge.edgeKind === "edge") {
    kindBaseOffset = -24;
  }
  if (!edge.parallelCount || edge.parallelCount < 2 || edge.parallelIndex == null) {
    return kindBaseOffset;
  }
  const center = (edge.parallelCount - 1) / 2;
  const delta = edge.parallelIndex - center;
  return Math.round(kindBaseOffset + delta * 24);
}

/**
 * 追加ボタンの可否。start と end は文書内に 1 つまで。
 * @param document 定義グラフ。
 * @param labels 追加できない理由の文言。
 * @returns 種別ごとの追加可否。
 */
export function buildAvailableNodeTypes(
  document: DefinitionGraphDocument,
  labels: { addNodeDisabledReasonStart: string; addNodeDisabledReasonEnd: string }
): AvailableNodeType[] {
  const startCount = document.nodes.filter((node) => node.type === "start").length;
  const endCount = document.nodes.filter((node) => node.type === "end").length;
  return [
    {
      type: "start",
      disabled: startCount >= 1,
      reason: startCount >= 1 ? labels.addNodeDisabledReasonStart : undefined
    },
    { type: "action", disabled: false },
    { type: "wait", disabled: false },
    { type: "fork", disabled: false },
    { type: "join", disabled: false },
    {
      type: "end",
      disabled: endCount >= 1,
      reason: endCount >= 1 ? labels.addNodeDisabledReasonEnd : undefined
    }
  ];
}

/**
 * 種別ごとの次のノード名。既存名と大文字小文字を区別せずに避ける。
 * @param document 定義グラフ。
 * @param type 追加する種別。
 * @returns 未使用の名前。
 */
export function nextNodeName(document: DefinitionGraphDocument, type: NodeType): string {
  const used = new Set(document.nodes.map((node) => node.name.toLowerCase()));
  for (let index = 1; index < 9999; index += 1) {
    const candidate = `${type}_${index}`;
    if (!used.has(candidate.toLowerCase())) {
      return candidate;
    }
  }
  return `${type}_${crypto.randomUUID().slice(0, 8)}`;
}

/**
 * 種別に応じた初期ノードを作る。
 * @param type ノード種別。
 * @param name ノード名。
 * @returns 文書へ追加するノード。
 */
export function createNode(type: NodeType, name: string): DefinitionGraphNode {
  switch (type) {
    case "start":
      return { name, type: "start" };
    case "action":
      return { name, type: "action", action: "noop" };
    case "wait":
      return { name, type: "wait", events: { resume: "" } };
    case "fork":
      return { name, type: "fork", branches: [] };
    case "join":
      return { name, type: "join" };
    case "end":
      return { name, type: "end" };
  }
}

/**
 * 名前が一致するノードだけを置き換える。
 * @param document 定義グラフ。
 * @param nodeName 対象ノード名。
 * @param updater ノードの更新。
 * @returns 更新後の文書。
 */
export function updateNode(
  document: DefinitionGraphDocument,
  nodeName: string,
  updater: (node: DefinitionGraphNode) => DefinitionGraphNode
): DefinitionGraphDocument {
  return {
    ...document,
    nodes: document.nodes.map((node) => (node.name === nodeName ? updater(node) : node))
  };
}

/**
 * Wait ノードのインスペクタ編集モードを判定する。
 * @param node 対象ノード。
 * @returns events マップ編集、subscribe、旧形式、または併用衝突。
 */
export function resolveWaitEditMode(node: DefinitionGraphNode): WaitEditMode {
  if (node.type !== "wait") {
    return "events";
  }
  const hasEventsProperty = node.events !== undefined;
  const hasSubscribeProperty = node.subscribe !== undefined;
  const hasLegacyEvent = Boolean(node.event?.trim());
  const hasEdges = (node.edges?.length ?? 0) > 0;
  if (hasEventsProperty && hasSubscribeProperty) {
    return "conflict";
  }
  if (hasSubscribeProperty && (hasLegacyEvent || hasEdges)) {
    return "conflict";
  }
  if (hasSubscribeProperty) {
    return "subscribe";
  }
  if (hasEventsProperty && hasLegacyEvent) {
    return "conflict";
  }
  if (hasEventsProperty) {
    return "events";
  }
  if (hasLegacyEvent || node.next?.trim() || hasEdges) {
    return "legacy";
  }
  return "events";
}
