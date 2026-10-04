"use client";

import { useCallback, useEffect, useMemo, useState, type MouseEvent } from "react";
import {
  MarkerType,
  Position,
  useNodesState,
  type Connection,
  type Edge,
  type Node,
  type OnConnect
} from "reactflow";
import { layoutGraph, type LayoutEdgeInput } from "@/shared/lib/graphLayout";
import {
  buildAvailableNodeTypes,
  createNode,
  edgeOffsetForRendering,
  nextNodeName,
  resolveWaitEditMode,
  toGraphEdges,
  toLayoutNodes,
  updateNode,
  type AvailableNodeType,
  type DefinitionGraphNodeData,
  type GraphEdgeMeta,
  type GraphSelection
} from "../lib/definitionGraphCanvas";
import { connectWaitEventTarget } from "../lib/setWaitEvents";
import { connectWaitSubscribeTarget } from "../lib/setWaitSubscribe";
import type { DefinitionGraphDocument, NodeType } from "../lib/types";

/** キャンバス操作が参照する文言。 */
export type DefinitionGraphCanvasLabels = {
  /** 空 topic の辺ラベル。 */
  waitSubscribeUntitledTopic: string;
  /** 自己参照を拒否したときのメッセージ。 */
  selfReferenceRejected: string;
  /** subscribe と他形式が衝突しているときのメッセージ。 */
  waitSubscribeConflictHint: string;
  /** start を追加できない理由。 */
  addNodeDisabledReasonStart: string;
  /** end を追加できない理由。 */
  addNodeDisabledReasonEnd: string;
};

/** 定義グラフキャンバスの振る舞い。 */
export type UseDefinitionGraphEditorOptions = {
  /** 編集中の文書。無いときは空キャンバス。 */
  document: DefinitionGraphDocument | null;
  /**
   * 文書の更新。
   * @param nextDocument 更新後の文書。
   */
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  /** キャンバス操作の文言。 */
  labels: DefinitionGraphCanvasLabels;
};

/** 定義グラフキャンバスの描画用状態。ReactNode は含まない。 */
export type DefinitionGraphEditorModel = {
  /** 選択中のノードまたは辺。 */
  selection: GraphSelection;
  /** 接続拒否などのメッセージ。無ければ null。 */
  graphMessage: string | null;
  /** 全画面か。 */
  isFullscreen: boolean;
  /** 全画面を切り替える。 */
  toggleFullscreen: () => void;
  /** React Flow のノード。 */
  nodes: Node<DefinitionGraphNodeData>[];
  /** React Flow の辺。 */
  edges: Edge[];
  /** ノード位置の変更を React Flow から受ける。 */
  onNodesChange: ReturnType<typeof useNodesState<DefinitionGraphNodeData>>[2];
  /**
   * ドラッグ終了でレイアウトを文書へ書く。
   * @param event マウスイベント。
   * @param node 離したノード。
   */
  onNodeDragStop: (event: MouseEvent, node: Node<DefinitionGraphNodeData>) => void;
  /** ハンドル接続。 */
  onConnect: OnConnect;
  /**
   * ノードを選択する。
   * @param nodeName ノード名。
   */
  selectNode: (nodeName: string) => void;
  /**
   * 辺を選択する。branch は選択しない。
   * @param edgeId React Flow の辺 ID。
   */
  selectEdge: (edgeId: string) => void;
  /** 選択を消す。 */
  clearSelection: () => void;
  /** 追加ボタンの可否。 */
  availableNodeTypes: AvailableNodeType[];
  /**
   * ノードを追加して選択する。追加できない種別では何もしない。
   * @param type ノード種別。
   */
  addNode: (type: NodeType) => void;
};

/**
 * 定義グラフの選択、ドラッグ位置、全画面、ノード接続を持つ。
 *
 * 画面は戻り値を React Flow と既存のインスペクタへ渡す。ノード部品とインスペクタの入力欄は画面側に残す。
 *
 * @param options 編集中の文書と更新関数。
 * @returns キャンバスの状態とコマンド。
 */
export function useDefinitionGraphEditor({
  document,
  onDocumentChange,
  labels
}: UseDefinitionGraphEditorOptions): DefinitionGraphEditorModel {
  const [selection, setSelection] = useState<GraphSelection>(null);
  const [graphMessage, setGraphMessage] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [nodes, setNodes, onNodesChange] = useNodesState<DefinitionGraphNodeData>([]);

  const graphLayout = useMemo(() => {
    if (!document) {
      return {
        edges: [] as Edge[],
        edgeMap: new Map<string, GraphEdgeMeta>(),
        layoutByName: new Map<string, { x: number; y: number; w: number; h: number }>()
      };
    }
    const sourceEdges = toGraphEdges(document, labels.waitSubscribeUntitledTopic);
    const layout = layoutGraph(
      toLayoutNodes(document),
      sourceEdges.map<LayoutEdgeInput>((edge) => ({
        id: edge.id,
        from: edge.source,
        to: edge.target
      })),
      {
        // h > 72 にすると compact レイアウト（ranksep 小）を回避でき、上下間隔を広げられる。
        defaultNodeSize: { w: 240, h: 80 }
      }
    );
    const layoutByName = new Map(layout.nodes.map((node) => [node.name, node]));
    const edgeMap = new Map(sourceEdges.map((edge) => [edge.id, edge]));
    const edges: Edge[] = sourceEdges.map((edge) => {
      let label = "edge";
      if (edge.edgeKind === "next") {
        label = "next";
      } else if (edge.edgeKind === "error") {
        label = "error";
      } else if (edge.edgeKind === "branch") {
        label = "branch";
      } else if (edge.edgeKind === "waitEvent") {
        label = edge.eventName ?? "event";
      } else if (edge.edgeKind === "waitSubscribe") {
        label = edge.eventName ?? labels.waitSubscribeUntitledTopic;
      }
      return {
        id: edge.id,
        source: edge.source,
        target: edge.target,
        sourceHandle: edge.edgeKind === "error" ? "out-error" : "out",
        targetHandle: "in",
        label,
        animated: edge.edgeKind === "edge" || edge.edgeKind === "error",
        style:
          edge.edgeKind === "error"
            ? {
                stroke: "var(--md-sys-color-edge-error-accent)",
                strokeDasharray: "7 5"
              }
            : undefined,
        markerEnd:
          edge.edgeKind === "error"
            ? {
                type: MarkerType.ArrowClosed,
                width: 15,
                height: 15,
                color: "var(--md-sys-color-edge-error-accent)"
              }
            : undefined,
        labelStyle: {
          fontSize: 10,
          fontWeight: edge.edgeKind === "error" ? 700 : 600,
          fill:
            edge.edgeKind === "error"
              ? "var(--md-sys-color-edge-error-accent)"
              : "var(--md-sys-color-on-surface-variant)"
        },
        pathOptions: {
          offset: edgeOffsetForRendering(edge),
          borderRadius: 10
        }
      };
    });
    return { edges, edgeMap, layoutByName };
  }, [document, labels.waitSubscribeUntitledTopic]);

  useEffect(() => {
    if (!document) {
      setNodes([]);
      return;
    }
    const { layoutByName } = graphLayout;
    setNodes((prev) => {
      const prevPos = new Map(prev.map((node) => [node.id, node.position]));
      return document.nodes.map((node) => {
        const positioned = layoutByName.get(node.name);
        const pos =
          document.meta?.layout?.[node.name] ?? prevPos.get(node.name) ?? { x: positioned?.x ?? 0, y: positioned?.y ?? 0 };
        const width = positioned?.w ?? 220;
        const height = positioned?.h ?? 120;
        const flowNode: Node<DefinitionGraphNodeData> = {
          id: node.name,
          type: "definitionGraphNode",
          position: pos,
          style: { width, height },
          width,
          height,
          sourcePosition: Position.Bottom,
          targetPosition: Position.Top,
          data: {
            nodeType: node.type.toUpperCase(),
            label: node.name,
            width,
            height
          },
          draggable: true,
          connectable: true
        };
        return flowNode;
      });
    });
  }, [document, graphLayout, setNodes]);

  const persistNodePosition = useCallback(
    (nodeName: string, position: { x: number; y: number }) => {
      if (!document) {
        return;
      }
      onDocumentChange({
        ...document,
        meta: {
          ...document.meta,
          layout: {
            ...document.meta?.layout,
            [nodeName]: { x: position.x, y: position.y }
          }
        }
      });
    },
    [document, onDocumentChange]
  );

  const onNodeDragStop = useCallback(
    (_event: MouseEvent, node: Node<DefinitionGraphNodeData>) => {
      persistNodePosition(String(node.id), node.position);
    },
    [persistNodePosition]
  );

  const availableNodeTypes = useMemo(
    () => (document ? buildAvailableNodeTypes(document, labels) : []),
    [document, labels]
  );

  const onConnect: OnConnect = (connection: Connection) => {
    if (!document || !connection.source || !connection.target) {
      return;
    }
    const targetNodeName = connection.target;
    if (connection.source === connection.target) {
      setGraphMessage(labels.selfReferenceRejected);
      return;
    }
    const sourceNode = document.nodes.find((node) => node.name === connection.source);
    if (!sourceNode) {
      return;
    }
    if (connection.sourceHandle === "out-error") {
      if (sourceNode.type !== "action") {
        return;
      }
      onDocumentChange(
        updateNode(document, sourceNode.name, (node) =>
          node.type === "action" ? { ...node, error: targetNodeName } : node
        )
      );
      setGraphMessage(null);
      return;
    }
    const waitMode = sourceNode.type === "wait" ? resolveWaitEditMode(sourceNode) : null;
    if (waitMode === "conflict") {
      setGraphMessage(labels.waitSubscribeConflictHint);
      return;
    }
    const nextDocument = updateNode(document, sourceNode.name, (node) => {
      if (node.type === "wait" && (node.events !== undefined || node.subscribe !== undefined)) {
        return node;
      }
      if (node.type === "fork") {
        const branches = new Set(node.branches ?? []);
        branches.add(targetNodeName);
        return { ...node, branches: Array.from(branches) };
      }
      if (!node.next && (!node.edges || node.edges.length === 0)) {
        return { ...node, next: targetNodeName };
      }
      if (node.next && (!node.edges || node.edges.length === 0)) {
        if (node.next === targetNodeName) {
          return node;
        }
        return {
          ...node,
          next: undefined,
          edges: [{ to: node.next }, { to: targetNodeName }]
        };
      }
      const existing = node.edges ?? [];
      if (existing.some((edge) => edge.to === targetNodeName)) {
        return node;
      }
      return {
        ...node,
        edges: [...existing, { to: targetNodeName }]
      };
    });
    if (waitMode === "subscribe") {
      onDocumentChange(connectWaitSubscribeTarget(document, sourceNode.name, targetNodeName));
    } else if (sourceNode.type === "wait" && sourceNode.events !== undefined) {
      onDocumentChange(connectWaitEventTarget(document, sourceNode.name, targetNodeName));
    } else {
      onDocumentChange(nextDocument);
    }
    setGraphMessage(null);
  };

  useEffect(() => {
    if (!isFullscreen) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsFullscreen(false);
      }
    };
    globalThis.addEventListener("keydown", onKeyDown);
    return () => globalThis.removeEventListener("keydown", onKeyDown);
  }, [isFullscreen]);

  const toggleFullscreen = useCallback(() => {
    setIsFullscreen((current) => !current);
  }, []);

  const selectNode = useCallback((nodeName: string) => {
    setSelection({ kind: "node", nodeName });
  }, []);

  const selectEdge = useCallback(
    (edgeId: string) => {
      const meta = graphLayout.edgeMap.get(edgeId);
      if (!meta || meta.edgeKind === "branch") {
        return;
      }
      setSelection({
        kind: "edge",
        nodeName: meta.source,
        edgeKind: meta.edgeKind,
        edgeIndex: meta.edgeIndex,
        eventName: meta.eventName,
        subscribeIndex: meta.subscribeIndex
      });
    },
    [graphLayout.edgeMap]
  );

  const clearSelection = useCallback(() => {
    setSelection(null);
  }, []);

  const addNode = useCallback(
    (type: NodeType) => {
      if (!document) {
        return;
      }
      const entry = availableNodeTypes.find((candidate) => candidate.type === type);
      if (!entry || entry.disabled) {
        return;
      }
      const name = nextNodeName(document, type);
      onDocumentChange({
        ...document,
        nodes: [...document.nodes, createNode(type, name)]
      });
      setSelection({ kind: "node", nodeName: name });
    },
    [availableNodeTypes, document, onDocumentChange]
  );

  return {
    selection,
    graphMessage,
    isFullscreen,
    toggleFullscreen,
    nodes,
    edges: graphLayout.edges,
    onNodesChange,
    onNodeDragStop,
    onConnect,
    selectNode,
    selectEdge,
    clearSelection,
    availableNodeTypes,
    addNode
  };
}
