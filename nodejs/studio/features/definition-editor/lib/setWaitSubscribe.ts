import { setWaitEvents } from "./setWaitEvents";
import type {
  DefinitionGraphDocument,
  DefinitionGraphNode,
  DefinitionGraphWaitSubscribeEntry
} from "./types";

/** Wait の編集モード切替先。 */
export type WaitModeSwitch = "events" | "subscribe";

/**
 * Wait ノードの `subscribe` 配列を設定し、Signal / 旧形式フィールドをクリアする。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param entries 購読行（順序維持）
 * @returns 更新後ドキュメント（対象が Wait でない場合は変更なし）
 */
export function setWaitSubscribe(
  document: DefinitionGraphDocument,
  nodeName: string,
  entries: readonly DefinitionGraphWaitSubscribeEntry[]
): DefinitionGraphDocument {
  return mapWaitNode(document, nodeName, () => ({
    subscribe: entries.map((entry) => copySubscribeEntry(entry)),
    events: undefined,
    event: undefined,
    next: undefined,
    edges: undefined
  }));
}

/**
 * 空の購読行を 1 件追加する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @returns 更新後ドキュメント
 */
export function addWaitSubscribeRow(
  document: DefinitionGraphDocument,
  nodeName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait") {
    return document;
  }
  const current = node.subscribe === undefined ? [] : [...node.subscribe];
  current.push({ topic: "", next: "" });
  return setWaitSubscribe(document, nodeName, current);
}

/**
 * 指定 index の購読行を削除する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param index 削除する配列 index
 * @returns 更新後ドキュメント
 */
export function removeWaitSubscribeRow(
  document: DefinitionGraphDocument,
  nodeName: string,
  index: number
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait" || node.subscribe === undefined) {
    return document;
  }
  if (index < 0 || index >= node.subscribe.length) {
    return document;
  }
  return setWaitSubscribe(
    document,
    nodeName,
    node.subscribe.filter((_, entryIndex) => entryIndex !== index)
  );
}

/**
 * 指定 index の遷移先だけを差し替える。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param index 対象配列 index
 * @param targetNodeName 新しい遷移先
 * @returns 更新後ドキュメント
 */
export function setWaitSubscribeTarget(
  document: DefinitionGraphDocument,
  nodeName: string,
  index: number,
  targetNodeName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait" || node.subscribe === undefined) {
    return document;
  }
  if (index < 0 || index >= node.subscribe.length) {
    return document;
  }
  return setWaitSubscribe(
    document,
    nodeName,
    node.subscribe.map((entry, entryIndex) =>
      entryIndex === index ? { ...entry, next: targetNodeName } : entry
    )
  );
}

/**
 * Wait ノード上の `subscribe` にキャンバス接続先を反映する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param targetNodeName 接続先ノード名
 * @returns 更新後ドキュメント
 * @remarks 空 `next` の先頭行を埋め、無ければ行を末尾追加する。
 */
export function connectWaitSubscribeTarget(
  document: DefinitionGraphDocument,
  nodeName: string,
  targetNodeName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait") {
    return document;
  }

  const current =
    node.subscribe === undefined ? [] : node.subscribe.map((entry) => copySubscribeEntry(entry));
  const emptyIndex = current.findIndex((entry) => entry.next.trim().length === 0);
  if (emptyIndex < 0) {
    current.push({ topic: "", next: targetNodeName });
  } else {
    current[emptyIndex] = { ...current[emptyIndex], next: targetNodeName };
  }
  return setWaitSubscribe(document, nodeName, current);
}

/**
 * Signal と Subscribe を破壊的に切り替える。中身の機械変換はしない。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param mode 切替先
 * @returns 更新後ドキュメント
 */
export function switchWaitMode(
  document: DefinitionGraphDocument,
  nodeName: string,
  mode: WaitModeSwitch
): DefinitionGraphDocument {
  switch (mode) {
    case "subscribe":
      return setWaitSubscribe(document, nodeName, [{ topic: "", next: "" }]);
    case "events":
      return setWaitEvents(document, nodeName, { resume: "" });
  }
}

function copySubscribeEntry(
  entry: DefinitionGraphWaitSubscribeEntry
): DefinitionGraphWaitSubscribeEntry {
  const topic = entry.topic;
  const next = entry.next;
  const key = entry.key?.trim();
  if (key === undefined || key.length === 0) {
    return { topic, next };
  }
  return { topic, key, next };
}

function mapWaitNode(
  document: DefinitionGraphDocument,
  nodeName: string,
  updater: (node: DefinitionGraphNode) => Partial<DefinitionGraphNode>
): DefinitionGraphDocument {
  let changed = false;
  const nodes = document.nodes.map((node) => {
    if (node.name !== nodeName || node.type !== "wait") {
      return node;
    }
    changed = true;
    return { ...node, ...updater(node), name: node.name, type: "wait" as const };
  });
  return changed ? { ...document, nodes } : document;
}
