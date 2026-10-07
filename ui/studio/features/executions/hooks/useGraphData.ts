"use client";

import { useMemo, useRef } from "react";
import { resolveGroupBounds } from "../lib/grouping";
import { layoutGraph, placeNodesAtSavedLayout } from "@/shared/lib/graphLayout";
import { mergeGraph, type MergedGraphEdge, type MergedGraphNode } from "../lib/mergeGraph";
import { pickPreferredRuntimeNode } from "../lib/pickPreferredRuntimeNode";
import type { ExecutionNodeDTO, ExecutionView } from "../types";
import type { GroupBounds } from "../lib/grouping";
import type { LayoutEdgeInput, LayoutNodeInput, PositionedNode } from "@/shared/lib/graphLayout";
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
 * レイアウトをやり直すかを決める構造キー。
 * ノード名、辺の from / to、WAITING のノード名、保存レイアウトの有無だけを含む。
 *
 * @param input マージ後の構造。
 * @returns 比較用の文字列。status と attempt は含まない。
 */
export function buildGraphStructureKey(input: {
  nodeNames: readonly string[];
  edges: readonly { from: string; to: string }[];
  waitingNodeNames: readonly string[];
  hasSavedLayout: boolean;
}): string {
  const names = [...input.nodeNames].sort((left, right) => left.localeCompare(right)).join("\n");
  const edges = input.edges
    .map((edge) => `${edge.from}->${edge.to}`)
    .sort((left, right) => left.localeCompare(right))
    .join("\n");
  const waiting = [...input.waitingNodeNames].sort((left, right) => left.localeCompare(right)).join("\n");
  return `${input.hasSavedLayout ? "saved" : "computed"}\n${names}\n${edges}\n${waiting}`;
}

/**
 * 前回座標を残し、ノードの状態だけを差し替える。
 *
 * @param previous 前回レイアウトしたノード。
 * @param nextNodes 今回のノード。
 * @returns 前回の座標と寸法を載せた今回のノード。
 */
export function reuseGraphPositions<T extends LayoutNodeInput>(
  previous: ReadonlyArray<PositionedNode<T>>,
  nextNodes: readonly T[]
): Array<PositionedNode<T>> {
  const byName = new Map(previous.map((node) => [node.name, node]));
  return nextNodes.map((node) => {
    const prior = byName.get(node.name);
    if (!prior) return { ...node, x: 0, y: 0, w: 0, h: 0 };
    return { ...node, x: prior.x, y: prior.y, w: prior.w, h: prior.h };
  });
}

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
  const previousLayoutRef = useRef<{
    key: string;
    nodes: Array<PositionedNode<MergedGraphNode>>;
    edges: LayoutEdgeInput[];
  } | null>(null);

  return useMemo(() => {
    if (!execution) return null;
    const merged = mergeGraph(execution, graphDefinition);
    const layoutMap = merged.meta?.layout;
    const hasSavedLayout = layoutMap != null && Object.keys(layoutMap).length > 0;
    const structureKey = buildGraphStructureKey({
      nodeNames: merged.nodes.map((node) => node.name),
      edges: merged.edges,
      waitingNodeNames: merged.nodes.filter((node) => node.status === "WAITING").map((node) => node.name),
      hasSavedLayout
    });
    const edgeInputs = merged.edges.map((edge: MergedGraphEdge) => ({ ...edge }));

    let placed: Array<PositionedNode<MergedGraphNode>>;
    let edges: LayoutEdgeInput[];
    if (hasSavedLayout && layoutMap) {
      placed = placeNodesAtSavedLayout(merged.nodes, layoutMap, merged.meta);
      edges = edgeInputs;
    } else if (previousLayoutRef.current?.key === structureKey) {
      placed = reuseGraphPositions(previousLayoutRef.current.nodes, merged.nodes);
      edges = previousLayoutRef.current.edges;
    } else {
      const positioned = layoutGraph(merged.nodes, edgeInputs, merged.meta);
      placed = positioned.nodes;
      edges = positioned.edges;
    }

    previousLayoutRef.current = { key: structureKey, nodes: placed, edges };
    const nodes = expandWaitingNodeLayout(placed);
    const groups = resolveGroupBounds(nodes, edges, merged.groups, merged.meta);
    return {
      graphId: execution.graphId,
      definitionBased: merged.isDefinitionBased,
      mergedNodes: merged.nodes,
      nodes,
      edges,
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
