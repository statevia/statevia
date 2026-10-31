import type { ExecutionNodeDTO } from "../types";
import { listNodeVisits } from "./nodeVisits";
import { pickPreferredRuntimeNode } from "./pickPreferredRuntimeNode";
import { getNodeSortWeight } from "@/shared/lib/statusStyle";

/**
 * ノード一覧の 1 ページに出す状態の数。
 * 循環の訪問は状態ごとに畳むので、この件数は定義上の状態数に対するページサイズである。
 * グラフ取得は全ノードのままなので、ページングは描画行数だけを減らす。
 */
export const NODE_LIST_PAGE_SIZE = 20;

/** 一覧で 1 行にまとめる、同じ状態名の訪問。 */
export type NodeListGroup = {
  /** ページと展開状態のキー。状態名、または名前の無いノードの ID。 */
  key: string;
  /** 表示する状態名。名前が無いときは空。 */
  nodeName: string;
  /** 畳んだ行に出す代表。WAITING を優先する既存の代表選択。 */
  representative: ExecutionNodeDTO;
  /** attempt 昇順の訪問。1 件のときは代表と同じノード。 */
  visits: ExecutionNodeDTO[];
};

/**
 * 実行ノードを状態名ごとにまとめる。
 *
 * 並びは代表のステータス順で、同順は状態名順。名前の無いノードはまとめない。
 *
 * @param nodes 実行ビューの全ノード。
 * @returns 一覧の畳み行。
 */
export function buildNodeListGroups(nodes: readonly ExecutionNodeDTO[]): NodeListGroup[] {
  const named = new Map<string, ExecutionNodeDTO[]>();
  const unnamed: ExecutionNodeDTO[] = [];

  for (const node of nodes) {
    const nodeName = typeof node.nodeName === "string" ? node.nodeName.trim() : "";
    if (nodeName.length === 0) {
      unnamed.push(node);
      continue;
    }
    const key = nodeName.toLowerCase();
    const bucket = named.get(key);
    if (bucket) bucket.push(node);
    else named.set(key, [node]);
  }

  const groups = [...named.entries()].flatMap(([key, bucket]) => {
    const sampleName = bucket[0]?.nodeName ?? key;
    const visits = listNodeVisits(bucket, sampleName);
    const representative = pickPreferredRuntimeNode(bucket, sampleName) ?? visits[0];
    if (!representative) return [];
    return [{
      key,
      nodeName: representative.nodeName?.trim() || sampleName,
      representative,
      visits
    }];
  });

  const singleGroups = unnamed.map((node) => ({
    key: `id:${node.nodeId}`,
    nodeName: "",
    representative: node,
    visits: [node]
  }));

  return [...groups, ...singleGroups].sort((left, right) => {
    const statusOrder = getNodeSortWeight(left.representative.status) - getNodeSortWeight(right.representative.status);
    if (statusOrder !== 0) return statusOrder;
    return left.nodeName.localeCompare(right.nodeName);
  });
}

/**
 * 状態グループをページに切る。範囲外のページは端へ寄せる。
 *
 * @param groups 状態ごとの行。
 * @param page 1 始まりのページ。
 * @param pageSize 1 ページの状態数。
 * @returns そのページの行と、補正後のページ位置。
 */
export function pageNodeListGroups(
  groups: readonly NodeListGroup[],
  page: number,
  pageSize: number
): { groups: NodeListGroup[]; pageIndex: number; pageCount: number } {
  const size = pageSize > 0 ? pageSize : NODE_LIST_PAGE_SIZE;
  const pageCount = Math.max(1, Math.ceil(groups.length / size));
  const pageIndex = Math.min(Math.max(page, 1), pageCount);
  const start = (pageIndex - 1) * size;
  return {
    groups: groups.slice(start, start + size),
    pageIndex,
    pageCount
  };
}

/**
 * 指定ノードが含まれる一覧ページを返す。
 *
 * @param groups 状態ごとの行。
 * @param nodeId 選択中の実行ノード ID。
 * @param pageSize 1 ページの状態数。
 * @returns 1 始まりのページ。見つからなければ 1。
 */
export function nodeListPageForNode(
  groups: readonly NodeListGroup[],
  nodeId: string,
  pageSize: number
): number {
  const index = groups.findIndex((group) => group.visits.some((visit) => visit.nodeId === nodeId));
  if (index < 0) return 1;
  const size = pageSize > 0 ? pageSize : NODE_LIST_PAGE_SIZE;
  return Math.floor(index / size) + 1;
}
