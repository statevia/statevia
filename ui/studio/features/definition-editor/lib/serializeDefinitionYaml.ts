import { stringify } from "yaml";
import type {
  DefinitionGraphDocument,
  DefinitionGraphEdge,
  DefinitionGraphNode,
  DefinitionGraphWaitSubscribeEntry
} from "./types";

function isNonEmptyRecord(value: object): boolean {
  return Object.keys(value).length > 0;
}

/**
 * wait.events を YAML 出力用にコピーする（空なら undefined）。
 *
 * @param events Wait ノードのイベントマップ（未設定可）
 * @returns 非空のコピー。空または未設定なら undefined
 */
function copyWaitEvents(
  events: DefinitionGraphNode["events"]
): Record<string, string> | undefined {
  if (events === undefined || !isNonEmptyRecord(events)) {
    return undefined;
  }
  return { ...events };
}

/**
 * wait.subscribe を YAML 出力用にコピーする（空配列は出さない）。
 * 空の `key` はキーごと省略する。
 *
 * @param subscribe Wait ノードの購読配列（未設定可）
 * @returns 1 件以上あるときの配列。空または未設定なら undefined
 */
function copyWaitSubscribe(
  subscribe: DefinitionGraphNode["subscribe"]
): Array<Record<string, string>> | undefined {
  if (subscribe === undefined || subscribe.length === 0) {
    return undefined;
  }
  return subscribe.map((entry) => serializeWaitSubscribeEntry(entry));
}

/**
 * Subscribe 1 行を YAML オブジェクトにする。
 *
 * @param entry ドキュメント上の購読行
 * @returns topic / next。非空 key のみ付与
 */
function serializeWaitSubscribeEntry(
  entry: DefinitionGraphWaitSubscribeEntry
): Record<string, string> {
  const row: Record<string, string> = {
    topic: entry.topic,
    next: entry.next
  };
  if (entry.key !== undefined && entry.key.trim().length > 0) {
    row.key = entry.key;
  }
  return row;
}

/**
 * ノードの任意フィールドを YAML オブジェクトへ載せる。
 *
 * @param node グラフドキュメント上のノード
 * @param target 出力中のプレーンオブジェクト
 */
function assignOptionalNodeFields(
  node: DefinitionGraphNode,
  target: Record<string, unknown>
): void {
  if (node.action?.trim()) {
    target.action = node.action.trim();
  }
  if (node.type === "action" && node.error?.trim()) {
    target.error = node.error.trim();
  }
  if (node.event?.trim()) {
    target.event = node.event.trim();
  }
  const waitEvents = copyWaitEvents(node.events);
  if (waitEvents !== undefined) {
    target.events = waitEvents;
  }
  const waitSubscribe = copyWaitSubscribe(node.subscribe);
  if (waitSubscribe !== undefined) {
    target.subscribe = waitSubscribe;
  }
  if (node.type === "fork") {
    target.branches = [...(node.branches ?? [])];
  }
  if (node.type === "join" && node.mode === "all") {
    target.mode = "all";
  }
  if (node.input !== undefined) {
    const emit = typeof node.input === "string" || isNonEmptyRecord(node.input);
    if (emit) {
      target.input = node.input;
    }
  }
}

/**
 * 1 ノードを nodes YAML オブジェクトへ写像する。
 *
 * @param node グラフドキュメント上のノード
 * @returns stringify 用のプレーンオブジェクト
 */
function serializeGraphNode(node: DefinitionGraphNode): Record<string, unknown> {
  const base: Record<string, unknown> = {
    name: node.name,
    type: node.type
  };
  assignOptionalNodeFields(node, base);

  const edges = serializeOutgoingEdges(node.edges);
  const collapsedTarget = edges[0]?.to;
  if (collapsedTarget !== undefined && canCollapseToNext(node, edges)) {
    base.next = collapsedTarget;
    return base;
  }

  if (node.next?.trim()) {
    base.next = node.next.trim();
  }
  if (edges.length > 0) {
    base.edges = edges;
  }
  return base;
}

/**
 * 空 `to` を除いた出力用 edges を作る。
 *
 * @param edges ドキュメント上の辺（未設定可）
 * @returns YAML に載せる辺
 */
function serializeOutgoingEdges(
  edges: DefinitionGraphNode["edges"]
): DefinitionGraphEdge[] {
  return (edges ?? [])
    .map((edge) => ({
      to: edge.to,
      ...(edge.when
        ? {
            when: {
              path: edge.when.path,
              op: edge.when.op,
              value: edge.when.value
            }
          }
        : {}),
      ...(typeof edge.order === "number" ? { order: edge.order } : {}),
      ...(edge.default === true ? { default: true } : {})
    }))
    .filter((edge) => edge.to.trim().length > 0);
}

/**
 * 単一無条件 edge を `next` へ畳めるか。
 *
 * @param node 元ノード（明示 `next` があれば畳まない）
 * @param edges 正規化後の辺
 * @returns 畳めるとき true
 */
function canCollapseToNext(
  node: DefinitionGraphNode,
  edges: DefinitionGraphEdge[]
): boolean {
  const first = edges[0];
  return (
    !node.next?.trim() &&
    edges.length === 1 &&
    first !== undefined &&
    !first.when &&
    first.default !== true &&
    first.order == null
  );
}

/**
 * DefinitionGraphDocument を既存 nodes スキーマの YAML へ変換する。
 *
 * @param document グラフドキュメント
 * @returns nodes 形式の YAML 文字列
 */
export function serializeDefinitionYaml(document: DefinitionGraphDocument): string {
  const nodes = document.nodes.map((node) => serializeGraphNode(node));

  const workflowOut: Record<string, unknown> = {
    name: document.workflow.name
  };
  if (document.workflow.id?.trim()) {
    workflowOut.id = document.workflow.id.trim();
  }
  if (document.workflow.description?.trim()) {
    workflowOut.description = document.workflow.description.trim();
  }

  const root: Record<string, unknown> = {
    version: document.version,
    workflow: workflowOut,
    nodes
  };
  return stringify(root);
}
