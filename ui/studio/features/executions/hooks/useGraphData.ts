"use client";

import { useMemo } from "react";
import { resolveGroupBounds } from "../lib/grouping";
import { layoutGraph } from "@/shared/lib/graphLayout";
import { mergeGraph, type MergedGraphEdge, type MergedGraphNode } from "../lib/mergeGraph";
import { pickPreferredRuntimeNode } from "../lib/pickPreferredRuntimeNode";
import type { ExecutionNodeDTO, ExecutionView } from "../types";
import type { GroupBounds } from "../lib/grouping";
import type { LayoutEdgeInput, PositionedNode } from "@/shared/lib/graphLayout";
import type { GraphDefinition } from "@/features/executions/graphs/types";

/** GraphData の型定義。 */
export type GraphData = {
  graphId: string;
  definitionBased: boolean;
  mergedNodes: MergedGraphNode[];
  nodes: Array<PositionedNode<MergedGraphNode>>;
  edges: LayoutEdgeInput[];
  groups: GroupBounds[];
};

/**
 * WAITING ノードに再開操作を描くときのレイアウト高さ。
 * Wait の既定 150px では説明・選択・ボタンが枠を超え、出辺がカード途中に残る。
 * カード描画は中身の高さに任せ、ここは次ノードをその下へずらすための確保分。
 */
export const WAITING_RESUME_LAYOUT_HEIGHT = 320;

/**
 * WAITING ノードを {@link WAITING_RESUME_LAYOUT_HEIGHT} まで広げ、それより下のノードを同じだけ下げる。
 * 同じ高さの並列ノードは互いを押さない。
 *
 * @param nodes レイアウト済みノード。
 * @returns 間隔を空けたノード。WAITING が無ければ要素はそのまま。
 */
export function expandWaitingNodeLayout<T extends { status: string; y: number; h: number }>(nodes: readonly T[]): T[] {
  const extras = nodes
    .filter((node) => node.status === "WAITING" && node.h < WAITING_RESUME_LAYOUT_HEIGHT)
    .map((node) => ({ top: node.y, extra: WAITING_RESUME_LAYOUT_HEIGHT - node.h }));
  if (extras.length === 0) return [...nodes];

  return nodes.map((node) => {
    const extraAbove = extras.reduce((sum, item) => (item.top < node.y ? sum + item.extra : sum), 0);
    const ownExtra =
      node.status === "WAITING" && node.h < WAITING_RESUME_LAYOUT_HEIGHT
        ? WAITING_RESUME_LAYOUT_HEIGHT - node.h
        : 0;
    if (extraAbove === 0 && ownExtra === 0) return node;
    return { ...node, y: node.y + extraAbove, h: node.h + ownExtra };
  });
}

/** 実行ビューと定義グラフを合成したグラフデータを組み立てる。 */
export function useGraphData(
  execution: ExecutionView | null,
  graphDefinition: GraphDefinition | null
): GraphData | null {
  return useMemo(() => {
    if (!execution) return null;
    const merged = mergeGraph(execution, graphDefinition);
    const positioned = layoutGraph(
      merged.nodes,
      merged.edges.map((edge: MergedGraphEdge) => ({ ...edge })),
      merged.meta
    );
    const layoutMap = merged.meta?.layout;
    const placed =
      layoutMap && Object.keys(layoutMap).length > 0
        ? positioned.nodes.map((n) => {
            const p = layoutMap[n.name];
            return p ? { ...n, x: p.x, y: p.y } : n;
          })
        : positioned.nodes;
    const nodes = expandWaitingNodeLayout(placed);
    const groups = resolveGroupBounds(nodes, positioned.edges, merged.groups, merged.meta);
    return {
      graphId: execution.graphId,
      definitionBased: merged.isDefinitionBased,
      mergedNodes: merged.nodes,
      nodes,
      edges: positioned.edges,
      groups
    };
  }, [execution, graphDefinition]);
}

/**
 * ノード詳細・Resume 用に `ExecutionNodeDTO` を解決する。
 * リストはランタイム `nodeId`（UUID）、グラフは定義の `name`（状態キー）で選択するため、
 * `nodeName` およびマージ結果の `nodeName` でランタイム行へ寄せる。
 * 循環で同名 Wait が複数あるときは {@link pickPreferredRuntimeNode} で WAITING 行を優先する。
 */
export function getNodeWithFallback(
  execution: ExecutionView | null,
  graphData: GraphData | null,
  nodeId: string | null
): ExecutionNodeDTO | null {
  if (!execution || !nodeId) return null;
  const key = nodeId.trim();

  const byRuntimeId = execution.nodes.find((n) => n.nodeId === key);
  if (byRuntimeId) return byRuntimeId;

  const byNodeNameKey = pickPreferredRuntimeNode(execution.nodes, key);
  if (byNodeNameKey) return byNodeNameKey;

  const mergedNode = graphData?.mergedNodes.find((n) => n.name === key);
  if (!mergedNode) return null;

  const mergedState = mergedNode.nodeName.trim();
  if (mergedState.length > 0) {
    const byMergedNodeName = pickPreferredRuntimeNode(execution.nodes, mergedState);
    if (byMergedNodeName) return byMergedNodeName;
  }

  // 定義のみの IDLE 行など、実行 nodes に無いときはマージ結果から DTO を合成する。
  const fallbackFromMerged: ExecutionNodeDTO = {
    nodeId: mergedNode.nodeId,
    nodeName: mergedNode.nodeName,
    nodeType: mergedNode.nodeType,
    status: mergedNode.status,
    attempt: mergedNode.attempt,
    workerId: mergedNode.workerId,
    waitKey: mergedNode.waitKey,
    allowedEvents: mergedNode.allowedEvents ?? null,
    canceledByExecution: mergedNode.canceledByExecution
  };
  return fallbackFromMerged;
}
