"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { apiGet } from "@/shared/api";
import { loadActionSchemaIndex } from "../actionSchema/actionSchemaIndexSessionCache";
import {
  getCachedActionSchemaDetail,
  setCachedActionSchemaDetail
} from "../actionSchema/actionSchemaSessionCache";
import { buildIndexedActionIdSet } from "../actionSchema/isIndexedActionId";
import { collectUpstreamOutputPathHints } from "../actionSchema/outputSchemaHints";
import type {
  ActionSchemaDetailResponse,
  ActionSchemaIndexItem,
  JsonSchemaObject
} from "../actionSchema/types";
import { buildDocumentAdjacency } from "../lib/definitionGraphAdjacency";
import type { DefinitionGraphDocument, DefinitionGraphNode } from "../lib/types";

/** action schema 取得の入力。 */
export type UseGraphActionSchemasOptions = {
  /** 編集中の定義グラフ。 */
  document: DefinitionGraphDocument;
  /** 選択中の辺の起点。ノード選択や未選択では undefined。 */
  edgeSourceNode: DefinitionGraphNode | undefined;
};

/** グラフ編集が使う action schema。ReactNode は含まない。 */
export type GraphActionSchemasModel = {
  /** schema index の候補。 */
  actionCandidates: ReadonlyArray<ActionSchemaIndexItem>;
  /** index の取得中か。 */
  actionCandidatesLoading: boolean;
  /**
   * action schema を取得してキャッシュする。index に無い ID は取得しない。
   * @param actionId action ID。
   * @returns 詳細。失敗または対象外は undefined。
   */
  loadActionSchema: (actionId: string) => Promise<ActionSchemaDetailResponse | undefined>;
  /**
   * セッションキャッシュにある schema 詳細を返す。
   * @param actionId action ID。
   * @returns キャッシュ済みの詳細。無ければ undefined。
   */
  getCachedActionSchema: (actionId: string) => ActionSchemaDetailResponse | undefined;
  /** 選択中の辺の起点から見た上流 output の path 候補。 */
  whenPathHints: string[];
};

/**
 * 定義グラフ向けに action schema の index と詳細を取得する。
 *
 * 辺を選んだときは上流 action の output schema も先に読む。画面は候補と path ヒントを既存のインスペクタへ渡す。
 *
 * @param options 編集中の文書と、選択中の辺の起点。
 * @returns 候補、取得関数、when path のヒント。
 */
export function useGraphActionSchemas({
  document,
  edgeSourceNode
}: UseGraphActionSchemasOptions): GraphActionSchemasModel {
  const [outputSchemaByActionId, setOutputSchemaByActionId] = useState(
    () => new Map<string, JsonSchemaObject | undefined>()
  );
  const [actionCandidates, setActionCandidates] = useState<ReadonlyArray<ActionSchemaIndexItem>>([]);
  const [actionCandidatesLoading, setActionCandidatesLoading] = useState(true);
  const indexedActionIds = useMemo(() => buildIndexedActionIdSet(actionCandidates), [actionCandidates]);
  const inflightSchemaLoadsRef = useRef(new Map<string, Promise<ActionSchemaDetailResponse | undefined>>());

  useEffect(() => {
    let cancelled = false;
    void loadActionSchemaIndex(() => apiGet("/actions/schema/index")).then((items) => {
      if (!cancelled) {
        setActionCandidates(items);
        setActionCandidatesLoading(false);
      }
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const loadActionSchema = useCallback(async (actionId: string) => {
    const trimmed = actionId.trim();
    if (!trimmed || !indexedActionIds.has(trimmed)) {
      return undefined;
    }
    const cached = getCachedActionSchemaDetail(trimmed);
    if (cached) {
      return cached;
    }
    const inflight = inflightSchemaLoadsRef.current.get(trimmed);
    if (inflight !== undefined) {
      return inflight;
    }
    const request = (async () => {
      try {
        const detail = await apiGet<ActionSchemaDetailResponse>(
          `/actions/schema/${encodeURIComponent(trimmed)}`
        );
        if (!detail) {
          return undefined;
        }
        setCachedActionSchemaDetail(trimmed, detail);
        setOutputSchemaByActionId((previous) => {
          const next = new Map(previous);
          next.set(trimmed, detail.schema.outputSchema);
          return next;
        });
        return detail;
      } catch {
        return undefined;
      } finally {
        inflightSchemaLoadsRef.current.delete(trimmed);
      }
    })();
    inflightSchemaLoadsRef.current.set(trimmed, request);
    return request;
  }, [indexedActionIds]);

  const getCachedActionSchema = useCallback(
    (actionId: string) => getCachedActionSchemaDetail(actionId),
    []
  );

  const whenPathHints = useMemo(() => {
    if (!edgeSourceNode) {
      return [];
    }
    return collectUpstreamOutputPathHints(
      document.nodes.map((entry) => ({ name: entry.name, type: entry.type, action: entry.action })),
      buildDocumentAdjacency(document),
      edgeSourceNode.name,
      outputSchemaByActionId
    );
  }, [document, edgeSourceNode, outputSchemaByActionId]);

  useEffect(() => {
    if (!edgeSourceNode) {
      return;
    }
    const adjacency = buildDocumentAdjacency(document);
    const graphNodes = document.nodes.map((entry) => ({ name: entry.name, type: entry.type, action: entry.action }));
    const upstreamActionIds = new Set<string>();
    const visited = new Set<string>();
    const queue = adjacency.filter((edge) => edge.targetId === edgeSourceNode.name).map((edge) => edge.sourceId);
    while (queue.length > 0) {
      const currentName = queue.shift();
      if (!currentName || visited.has(currentName)) {
        continue;
      }
      visited.add(currentName);
      const node = graphNodes.find((entry) => entry.name === currentName);
      if (node?.type === "action" && node.action?.trim()) {
        upstreamActionIds.add(node.action.trim());
      }
      for (const edge of adjacency) {
        if (edge.targetId === currentName) {
          queue.push(edge.sourceId);
        }
      }
    }
    for (const actionId of upstreamActionIds) {
      void loadActionSchema(actionId);
    }
  }, [document, edgeSourceNode, loadActionSchema]);

  return {
    actionCandidates,
    actionCandidatesLoading,
    loadActionSchema,
    getCachedActionSchema,
    whenPathHints
  };
}
