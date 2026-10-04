"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import ReactFlow, {
  Background,
  Controls,
  Handle,
  MarkerType,
  MiniMap,
  Position,
  useNodeId,
  useUpdateNodeInternals
} from "reactflow";
import type { NodeProps, NodeTypes } from "reactflow";
import "reactflow/dist/style.css";
import { getNodeAppearance } from "@/shared/lib/nodeAppearance";
import { getStatusStyle } from "@/shared/lib/statusStyle";
import { renameNodeNameInDocument } from "../lib/renameNodeNameInDocument";
import {
  convertLegacyWaitToEvents,
  removeWaitEvent,
  setLegacyWaitEvent,
  setWaitEventTarget,
  setWaitEvents
} from "../lib/setWaitEvents";
import {
  removeWaitSubscribeRow,
  setWaitSubscribe,
  setWaitSubscribeTarget,
  switchWaitMode
} from "../lib/setWaitSubscribe";
import type { DefinitionGraphDocument, DefinitionGraphNode } from "../lib/types";
import {
  resolveWaitEditMode,
  updateNode,
  type DefinitionGraphNodeData,
  type GraphSelection
} from "../lib/definitionGraphCanvas";
import { useDefinitionGraphEditor } from "../hooks/useDefinitionGraphEditor";
import { useGraphActionSchemas } from "../hooks/useGraphActionSchemas";
import { ActionInputCodeEditor } from "@/shared/ui/ActionInputCodeEditor";
import { ActionIdCombobox } from "./ActionIdCombobox";
import { SchemaDrivenActionInputForm } from "./SchemaDrivenActionInputForm";
import { WaitEventsEditor } from "./WaitEventsEditor";
import { WaitSubscribeEditor } from "./WaitSubscribeEditor";
import { GraphNodeShell } from "@/shared/ui/GraphNodeShell";
import { getCachedActionSchemaDetail } from "../actionSchema/actionSchemaSessionCache";
import { isIndexedActionId } from "../actionSchema/isIndexedActionId";
import type {
  ActionInputValidationDetail,
  ActionSchemaDetailResponse,
  ActionSchemaIndexItem
} from "../actionSchema/types";

function formatActionInputForEditor(input: DefinitionGraphNode["input"]): string {
  if (input === undefined) {
    return "";
  }
  if (typeof input === "string") {
    return input;
  }
  try {
    return JSON.stringify(input, null, 2);
  } catch {
    return "";
  }
}

/**
 * パス文字列はそのまま、JSON オブジェクトは `{ ... }` として入力する。
 */
function parseActionInputEditorText(text: string): string | Record<string, unknown> | undefined {
  const t = text.trim();
  if (!t) {
    return undefined;
  }
  if (t.startsWith("{") || t.startsWith("[")) {
    const parsed: unknown = JSON.parse(t);
    if (parsed !== null && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    throw new SyntaxError("Input JSON must be a single object for action input mapping.");
  }
  return t;
}

function inputToFormRecord(input: DefinitionGraphNode["input"]): Record<string, unknown> {
  if (input !== null && typeof input === "object" && !Array.isArray(input)) {
    return { ...input };
  }
  return {};
}

const handleClassName =
  "z-20 h-4 w-4 border-md-outline-variant bg-md-surface-container";

function DefinitionGraphNodeComponent({ data }: NodeProps<DefinitionGraphNodeData>) {
  const appearance = getNodeAppearance(data.nodeType);
  const chrome = getStatusStyle("IDLE");
  const isGateway = appearance.shapeKind === "gatewayFork" || appearance.shapeKind === "gatewayJoin";
  const flowNodeId = useNodeId();
  const updateInternals = useUpdateNodeInternals();

  const t = data.nodeType.trim().toUpperCase();
  const isStart = t === "START";
  const isEnd = t === "END";
  const isAction = t === "ACTION";
  /** スタート: 出し口（下）のみ。エンド: 受け口（上）のみ。それ以外: 上下とも。 */
  const showTargetHandle = !isStart;
  const showSourceHandle = !isEnd;

  useEffect(() => {
    if (flowNodeId != null && flowNodeId !== "") {
      updateInternals(flowNodeId);
    }
  }, [flowNodeId, updateInternals, data.nodeType, data.label, data.width, data.height, showTargetHandle, showSourceHandle, isAction]);

  return (
    <div
      className={`relative box-border flex min-h-0 flex-col ${isGateway ? "bg-transparent" : ""}`}
      style={{
        width: data.width,
        height: data.height,
        minWidth: data.width,
        minHeight: data.height
      }}
    >
      {showTargetHandle && (
        <Handle
          id="in"
          type="target"
          position={Position.Top}
          className={handleClassName}
        />
      )}
      <div className="relative z-0 flex min-h-0 flex-1 flex-col overflow-hidden">
        <GraphNodeShell
          shapeKind={appearance.shapeKind}
          borderClass={chrome.borderClass}
          bgClass={chrome.bgClass}
          className="h-full min-h-0"
        >
          <div className="flex flex-col gap-0.5">
            <span className="text-[10px] font-semibold">{appearance.label}</span>
            <span className="break-all font-mono text-[9px] leading-tight">{data.label}</span>
          </div>
        </GraphNodeShell>
      </div>
      {showSourceHandle && (
        <Handle
          id="out"
          type="source"
          position={Position.Bottom}
          className={handleClassName}
        />
      )}
      {isAction && (
        <Handle
          id="out-error"
          type="source"
          position={Position.Right}
          className={handleClassName}
        />
      )}
    </div>
  );
}

const DEFINITION_GRAPH_NODE_TYPES: NodeTypes = {
  definitionGraphNode: DefinitionGraphNodeComponent
};

const DEFINITION_GRAPH_EDGE_DEFAULTS = {
  type: "smoothstep" as const,
  className: "definition-graph-edge",
  style: { strokeWidth: 2.75, stroke: "var(--md-sys-color-outline)" },
  markerEnd: {
    type: MarkerType.ArrowClosed,
    width: 15,
    height: 15,
    color: "var(--md-sys-color-outline)"
  },
  labelShowBg: true,
  labelStyle: { fontSize: 10, fontWeight: 600, fill: "var(--md-sys-color-on-surface)" },
  labelBgStyle: { minWidth: 52, fill: "var(--md-sys-color-surface)" },
  labelBgPadding: [4, 4] as [number, number],
  labelBgBorderRadius: 2
};

type DefinitionGraphEditorProps = {
  document: DefinitionGraphDocument | null;
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  validationMessages: string[];
  /** Compiler 422 の action input 詳細（インライン表示用）。 */
  actionValidationDetails?: ActionInputValidationDetail[];
  labels: {
    title: string;
    empty: string;
    addNode: string;
    addNodeDialogTitle: string;
    addNodeDisabledReasonStart: string;
    addNodeDisabledReasonEnd: string;
    nodeInspectorTitle: string;
    edgeInspectorTitle: string;
    deleteNode: string;
    deleteEdge: string;
    apply: string;
    closeDialog: string;
    selfReferenceRejected: string;
    whenOpPlaceholder: string;
    whenPathPlaceholder: string;
    whenPathHint: string;
    whenValuePlaceholder: string;
    whenValueDisabledForExists: string;
    whenValueHintIn: string;
    whenValueHintBetween: string;
    fullscreenEnter: string;
    fullscreenExit: string;
    actionInputLabel: string;
    actionErrorLabel: string;
    actionInputPlaceholder: string;
    actionInputHint: string;
    actionInputInvalidJson: string;
    actionIdCandidatesLoading: string;
    actionIdNoResults: string;
    waitEventsSectionTitle: string;
    waitEventNameLabel: string;
    waitEventTargetLabel: string;
    waitEventsAdd: string;
    waitEventsRemove: string;
    waitLegacyEventLabel: string;
    waitConvertToEvents: string;
    waitEventsConflictHint: string;
    waitSubscribeSectionTitle: string;
    waitSubscribeTopicLabel: string;
    waitSubscribeKeyLabel: string;
    waitSubscribeNextLabel: string;
    waitSubscribeAdd: string;
    waitSubscribeRemove: string;
    waitSwitchToSubscribe: string;
    waitSwitchToEvents: string;
    waitResolveToSubscribe: string;
    waitSubscribeConflictHint: string;
    waitSubscribeUntitledTopic: string;
  };
};

const WHEN_OP_OPTIONS = [
  { value: "EQ", label: "EQ (=)" },
  { value: "NE", label: "NE (!=)" },
  { value: "GT", label: "GT (>)" },
  { value: "GTE", label: "GTE (>=)" },
  { value: "LT", label: "LT (<)" },
  { value: "LTE", label: "LTE (<=)" },
  { value: "EXISTS", label: "EXISTS" },
  { value: "IN", label: "IN" },
  { value: "BETWEEN", label: "BETWEEN" }
] as const;

/** 十進・指数表記の ASCII 数値リテラル風（0x 等は含まない）。when の YAML 往復・パースで共通利用 */
const DECIMAL_NUMERIC_STRING_PATTERN = /^[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:e[-+]?\d+)?$/i;

function formatWhenValue(value: unknown): string {
  if (typeof value === "string") {
    const trimmed = value.trim();
    const asLower = trimmed.toLowerCase();
    const needsQuotesToStayString =
      asLower === "true" || asLower === "false" || DECIMAL_NUMERIC_STRING_PATTERN.test(trimmed);
    if (needsQuotesToStayString) {
      return `"${value}"`;
    }
    return value;
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return `${value}`;
  }
  if (value == null) {
    return "";
  }
  try {
    return JSON.stringify(value);
  } catch {
    return "";
  }
}

function parseWhenValueInput(input: string, op?: string): unknown {
  const trimmed = input.trim();
  const upperOp = op?.toUpperCase();

  if ((upperOp === "IN" || upperOp === "BETWEEN") && trimmed.startsWith("[") && trimmed.endsWith("]")) {
    try {
      const parsed: unknown = JSON.parse(trimmed);
      if (Array.isArray(parsed)) {
        return parsed;
      }
    } catch {
      // JSON 配列として解釈できない場合は既存ルールへフォールバックする。
    }
  }

  if (trimmed.length >= 2 && trimmed.startsWith("\"") && trimmed.endsWith("\"")) {
    return trimmed.slice(1, -1);
  }

  const normalized = trimmed.toLowerCase();
  if (normalized === "true") {
    return true;
  }
  if (normalized === "false") {
    return false;
  }

  if (DECIMAL_NUMERIC_STRING_PATTERN.test(trimmed)) {
    return Number(trimmed);
  }

  return input;
}

/** DefinitionGraphEditor。 */
export function DefinitionGraphEditor({
  document,
  onDocumentChange,
  validationMessages,
  actionValidationDetails = [],
  labels
}: Readonly<DefinitionGraphEditorProps>) {
  const canvas = useDefinitionGraphEditor({ document, onDocumentChange, labels });

  if (!document) {
    return (
      <section className="rounded-lg border border-md-outline bg-md-surface p-4">
        <p className="text-sm text-md-on-surface-variant">{labels.empty}</p>
      </section>
    );
  }

  const wrapperClassName = canvas.isFullscreen ? "fixed inset-0 z-50 bg-md-surface-container-high p-4" : "";
  const panelClassName = canvas.isFullscreen
    ? "mx-auto h-full w-full max-w-[1600px] space-y-3 rounded-lg border border-md-outline bg-md-surface p-4"
    : "space-y-3 rounded-lg border border-md-outline bg-md-surface p-4";
  const gridClassName = canvas.isFullscreen
    ? "grid h-[calc(100%-4rem)] gap-3 lg:grid-cols-[minmax(0,1fr)_340px]"
    : "grid gap-3 lg:grid-cols-[minmax(0,1fr)_340px]";
  const graphHeightClassName = canvas.isFullscreen ? "h-full min-h-[520px]" : "h-[420px] lg:h-[520px]";

  return (
    <div className={wrapperClassName}>
      <section className={panelClassName}>
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-sm font-semibold text-md-on-surface">{labels.title}</h3>
        <button
          type="button"
          className="ml-auto rounded border border-md-outline-variant bg-md-surface-container px-2 py-1 text-xs"
          onClick={canvas.toggleFullscreen}
        >
          {canvas.isFullscreen ? labels.fullscreenExit : labels.fullscreenEnter}
        </button>
      </div>

      <div className={gridClassName}>
        <div
          className={`${graphHeightClassName} min-h-0 min-w-0 rounded border border-md-outline-variant`}
        >
          <ReactFlow
            nodes={canvas.nodes}
            edges={canvas.edges}
            nodeTypes={DEFINITION_GRAPH_NODE_TYPES}
            onNodesChange={canvas.onNodesChange}
            onNodeDragStop={canvas.onNodeDragStop}
            onConnect={canvas.onConnect}
            defaultEdgeOptions={DEFINITION_GRAPH_EDGE_DEFAULTS}
            elevateEdgesOnSelect
            edgesFocusable
            onNodeClick={(_, node) => canvas.selectNode(String(node.id))}
            onEdgeClick={(_, edge) => canvas.selectEdge(String(edge.id))}
            onPaneClick={canvas.clearSelection}
            fitView
          >
            <MiniMap zoomable pannable />
            <Controls />
            <Background />
          </ReactFlow>
        </div>
        <div className="flex h-full min-h-0 flex-col gap-2 rounded border border-md-outline-variant bg-md-surface-container p-2">
          <section className="shrink-0 space-y-2 rounded border border-md-outline bg-md-surface p-2">
            <p className="text-sm font-medium">{labels.addNodeDialogTitle}</p>
            <div className="grid grid-cols-2 gap-2">
              {canvas.availableNodeTypes.map((entry) => (
                <button
                  key={entry.type}
                  type="button"
                  disabled={entry.disabled}
                  className="rounded border border-md-outline px-2 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={() => canvas.addNode(entry.type)}
                  title={entry.reason}
                >
                  {entry.type}
                </button>
              ))}
            </div>
            {canvas.availableNodeTypes.some((entry) => entry.disabled && entry.reason) && (
              <ul className="list-disc pl-4 text-xs text-md-on-surface-variant">
                {canvas.availableNodeTypes
                  .filter((entry) => entry.disabled && entry.reason)
                  .map((entry) => (
                    <li key={`${entry.type}-${entry.reason}`}>{entry.reason}</li>
                  ))}
              </ul>
            )}
          </section>
          <div className="min-h-0 flex-1 overflow-y-auto">
            <GraphInspector
              document={document}
              selection={canvas.selection}
              labels={labels}
              actionValidationDetails={actionValidationDetails}
              onDocumentChange={onDocumentChange}
              onClearSelection={canvas.clearSelection}
              onInspectingNodeNameChange={canvas.selectNode}
            />
          </div>
        </div>
      </div>

      {canvas.graphMessage && <p className="text-xs text-rose-600">{canvas.graphMessage}</p>}
      {validationMessages.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-xs text-rose-600">
          {validationMessages.slice(0, 6).map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}

      </section>
    </div>
  );
}

type GraphInspectorProps = {
  document: DefinitionGraphDocument;
  selection: GraphSelection;
  labels: DefinitionGraphEditorProps["labels"];
  actionValidationDetails: ActionInputValidationDetail[];
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  onClearSelection: () => void;
  /** name 入力でノード識別子が変わったとき選択状態を追従させる（未追従だとインスペクターが消える） */
  onInspectingNodeNameChange?: (nextName: string) => void;
};

type GraphNodeInspectorProps = {
  document: DefinitionGraphDocument;
  node: DefinitionGraphNode;
  labels: DefinitionGraphEditorProps["labels"];
  actionValidationDetails: ActionInputValidationDetail[];
  loadActionSchema: (actionId: string) => Promise<ActionSchemaDetailResponse | undefined>;
  getCachedActionSchema: (actionId: string) => ActionSchemaDetailResponse | undefined;
  actionCandidates: ReadonlyArray<ActionSchemaIndexItem>;
  actionCandidatesLoading: boolean;
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  onClearSelection: () => void;
  onInspectingNodeNameChange?: (nextName: string) => void;
};

function GraphNodeInspector({
  document,
  node,
  labels,
  actionValidationDetails,
  loadActionSchema,
  getCachedActionSchema,
  actionCandidates,
  actionCandidatesLoading,
  onDocumentChange,
  onClearSelection,
  onInspectingNodeNameChange
}: Readonly<GraphNodeInspectorProps>) {
  const actionInputSig = node.type === "action" ? JSON.stringify(node.input ?? null) : "";
  const [actionInputDraft, setActionInputDraft] = useState("");
  const [actionInputError, setActionInputError] = useState<string | null>(null);
  const [actionOrEventDraft, setActionOrEventDraft] = useState(
    () => (node.type === "action" ? node.action : node.event) ?? ""
  );
  const waitEditMode = resolveWaitEditMode(node);
  const schemaLookupActionId = node.type === "action" ? actionOrEventDraft.trim() : "";
  const isIndexedSchemaAction = useMemo(
    () => isIndexedActionId(schemaLookupActionId, actionCandidates),
    [actionCandidates, schemaLookupActionId]
  );
  const [schemaDetail, setSchemaDetail] = useState<ActionSchemaDetailResponse | undefined>(() =>
    schemaLookupActionId ? getCachedActionSchemaDetail(schemaLookupActionId) : undefined
  );
  const [schemaLoadFailed, setSchemaLoadFailed] = useState(false);
  const nodeValidationDetails = useMemo(
    () =>
      actionValidationDetails.filter(
        (detail) => detail.state === node.name || detail.state === undefined
      ),
    [actionValidationDetails, node.name]
  );

  useEffect(() => {
    if (node.type === "action") {
      setActionInputDraft(formatActionInputForEditor(node.input));
      setActionInputError(null);
      setActionOrEventDraft(node.action ?? "");
    } else if (node.type === "wait" && waitEditMode === "legacy") {
      setActionOrEventDraft(node.event ?? "");
    }
  }, [node.name, node.type, node.input, node.action, node.event, actionInputSig, waitEditMode]);

  const commitActionOrEventDraft = useCallback(() => {
    if (node.type === "action") {
      if (actionOrEventDraft === (node.action ?? "")) {
        return;
      }
      onDocumentChange(
        updateNode(document, node.name, (targetNode) =>
          targetNode.type === "action"
            ? { ...targetNode, action: actionOrEventDraft, input: undefined }
            : targetNode
        )
      );
      setActionInputDraft("");
      setActionInputError(null);
      return;
    }
    if (node.type === "wait" && waitEditMode === "legacy") {
      if (actionOrEventDraft === (node.event ?? "")) {
        return;
      }
      onDocumentChange(setLegacyWaitEvent(document, node.name, actionOrEventDraft));
    }
  }, [
    actionOrEventDraft,
    document,
    node.action,
    node.event,
    node.name,
    node.type,
    onDocumentChange,
    waitEditMode
  ]);

  useEffect(() => {
    if (node.type !== "action" || !schemaLookupActionId) {
      setSchemaDetail(undefined);
      setSchemaLoadFailed(false);
      return;
    }

    if (actionCandidatesLoading) {
      const cachedWhileLoading = getCachedActionSchema(schemaLookupActionId);
      if (cachedWhileLoading) {
        setSchemaDetail(cachedWhileLoading);
        setSchemaLoadFailed(false);
      }
      return;
    }

    if (!isIndexedSchemaAction) {
      setSchemaDetail(undefined);
      setSchemaLoadFailed(false);
      return;
    }

    const cached = getCachedActionSchema(schemaLookupActionId);
    if (cached) {
      setSchemaDetail(cached);
      setSchemaLoadFailed(false);
      return;
    }

    let cancelled = false;
    void loadActionSchema(schemaLookupActionId).then((detail) => {
      if (cancelled) {
        return;
      }
      setSchemaDetail(detail);
      setSchemaLoadFailed(!detail);
    });
    return () => {
      cancelled = true;
    };
  }, [
    actionCandidatesLoading,
    getCachedActionSchema,
    isIndexedSchemaAction,
    loadActionSchema,
    node.type,
    schemaLookupActionId
  ]);

  const useSchemaForm = node.type === "action" && schemaDetail && !schemaLoadFailed;
  const formValue = inputToFormRecord(node.input);

  return (
    <section className="space-y-2 rounded border border-md-outline-variant bg-md-surface-container p-3">
      <p className="text-sm font-medium">{labels.nodeInspectorTitle}</p>
      <label className="block text-xs">
        <span className="block">name</span>
        <input
          className="mt-1 w-full rounded border border-md-outline px-2 py-1"
          value={node.name}
          onChange={(changeEvent) => {
            const nextName = changeEvent.target.value;
            onDocumentChange(renameNodeNameInDocument(document, node.name, nextName));
            onInspectingNodeNameChange?.(nextName);
          }}
        />
      </label>
      {(node.type === "action" || (node.type === "wait" && waitEditMode === "legacy")) && (
        <label className="block text-xs">
          <span className="block">
            {node.type === "action" ? "action" : labels.waitLegacyEventLabel}
          </span>
          {node.type === "action" ? (
            <ActionIdCombobox
              value={actionOrEventDraft}
              candidates={actionCandidates}
              loading={actionCandidatesLoading}
              labels={{
                loading: labels.actionIdCandidatesLoading,
                noResults: labels.actionIdNoResults
              }}
              onChange={setActionOrEventDraft}
              onCommit={commitActionOrEventDraft}
            />
          ) : (
            <input
              className="mt-1 w-full rounded border border-md-outline px-2 py-1"
              value={actionOrEventDraft}
              onChange={(changeEvent) => {
                setActionOrEventDraft(changeEvent.target.value);
              }}
              onBlur={() => {
                commitActionOrEventDraft();
              }}
              onKeyDown={(keydownEvent) => {
                if (keydownEvent.key === "Enter") {
                  keydownEvent.currentTarget.blur();
                }
              }}
            />
          )}
        </label>
      )}
      {node.type === "wait" && waitEditMode === "legacy" && (
        <button
          type="button"
          className="rounded border border-md-outline-variant px-2 py-1 text-xs"
          onClick={() => {
            onDocumentChange(convertLegacyWaitToEvents(document, node.name));
          }}
        >
          {labels.waitConvertToEvents}
        </button>
      )}
      {node.type === "wait" && waitEditMode === "conflict" && (
        <div className="space-y-2">
          <p className="text-xs text-rose-700">
            {node.subscribe !== undefined && node.events !== undefined
              ? labels.waitSubscribeConflictHint
              : labels.waitEventsConflictHint}
          </p>
          {node.events !== undefined && (
            <button
              type="button"
              className="rounded border border-md-outline-variant px-2 py-1 text-xs"
              onClick={() => {
                onDocumentChange(setWaitEvents(document, node.name, node.events ?? {}));
              }}
            >
              {labels.waitConvertToEvents}
            </button>
          )}
          {node.subscribe !== undefined && (
            <button
              type="button"
              className="rounded border border-md-outline-variant px-2 py-1 text-xs"
              onClick={() => {
                onDocumentChange(
                  setWaitSubscribe(
                    document,
                    node.name,
                    node.subscribe && node.subscribe.length > 0
                      ? node.subscribe
                      : [{ topic: "", next: "" }]
                  )
                );
              }}
            >
              {labels.waitResolveToSubscribe}
            </button>
          )}
        </div>
      )}
      {node.type === "wait" && waitEditMode === "events" && (
        <div className="space-y-2">
          <WaitEventsEditor
            events={node.events ?? {}}
            labels={{
              waitEventsSectionTitle: labels.waitEventsSectionTitle,
              waitEventNameLabel: labels.waitEventNameLabel,
              waitEventTargetLabel: labels.waitEventTargetLabel,
              waitEventsAdd: labels.waitEventsAdd,
              waitEventsRemove: labels.waitEventsRemove
            }}
            onEventsChange={(events) => {
              onDocumentChange(setWaitEvents(document, node.name, events));
            }}
          />
          <button
            type="button"
            className="rounded border border-md-outline-variant px-2 py-1 text-xs"
            onClick={() => {
              onDocumentChange(switchWaitMode(document, node.name, "subscribe"));
            }}
          >
            {labels.waitSwitchToSubscribe}
          </button>
        </div>
      )}
      {node.type === "wait" && waitEditMode === "subscribe" && (
        <div className="space-y-2">
          <WaitSubscribeEditor
            entries={node.subscribe ?? []}
            labels={{
              waitSubscribeSectionTitle: labels.waitSubscribeSectionTitle,
              waitSubscribeTopicLabel: labels.waitSubscribeTopicLabel,
              waitSubscribeKeyLabel: labels.waitSubscribeKeyLabel,
              waitSubscribeNextLabel: labels.waitSubscribeNextLabel,
              waitSubscribeAdd: labels.waitSubscribeAdd,
              waitSubscribeRemove: labels.waitSubscribeRemove
            }}
            onEntriesChange={(entries) => {
              onDocumentChange(setWaitSubscribe(document, node.name, entries));
            }}
          />
          <button
            type="button"
            className="rounded border border-md-outline-variant px-2 py-1 text-xs"
            onClick={() => {
              onDocumentChange(switchWaitMode(document, node.name, "events"));
            }}
          >
            {labels.waitSwitchToEvents}
          </button>
        </div>
      )}
      {node.type === "action" && (
        <label className="block text-xs">
          <span className="block">{labels.actionErrorLabel}</span>
          <input
            className="mt-1 w-full rounded border border-md-outline px-2 py-1"
            value={node.error ?? ""}
            onChange={(changeEvent) => {
              const nextValue = changeEvent.target.value.trim();
              onDocumentChange(
                updateNode(document, node.name, (targetNode) =>
                  targetNode.type === "action"
                    ? { ...targetNode, error: nextValue.length > 0 ? nextValue : undefined }
                    : targetNode
                )
              );
            }}
          />
        </label>
      )}
      {node.type === "action" && (
        <div className="block text-xs">
          <span className="block">{labels.actionInputLabel}</span>
          {useSchemaForm ? (
            <SchemaDrivenActionInputForm
              actionId={schemaLookupActionId}
              schemaDetail={schemaDetail}
              value={formValue}
              validationDetails={nodeValidationDetails}
              onChange={(nextValue) => {
                setActionInputError(null);
                onDocumentChange(
                  updateNode(document, node.name, (targetNode) =>
                    targetNode.type === "action"
                      ? {
                          ...targetNode,
                          input: Object.keys(nextValue).length > 0 ? nextValue : undefined
                        }
                      : targetNode
                  )
                );
              }}
            />
          ) : (
            <>
              <ActionInputCodeEditor
                key={node.name}
                value={actionInputDraft}
                placeholder={labels.actionInputPlaceholder}
                onChange={(next) => {
                  setActionInputDraft(next);
                  setActionInputError(null);
                }}
                onBlur={(latestText) => {
                  try {
                    const parsed = parseActionInputEditorText(latestText);
                    setActionInputError(null);
                    onDocumentChange(
                      updateNode(document, node.name, (targetNode) =>
                        targetNode.type === "action" ? { ...targetNode, input: parsed } : targetNode
                      )
                    );
                    setActionInputDraft(formatActionInputForEditor(parsed));
                  } catch {
                    setActionInputError(labels.actionInputInvalidJson);
                  }
                }}
              />
              <span className="mt-0.5 block text-[10px] text-md-on-surface-variant">
                {labels.actionInputHint}
              </span>
            </>
          )}
          {actionInputError ? <p className="text-[10px] text-rose-600">{actionInputError}</p> : null}
        </div>
      )}
      {node.type === "fork" && (
        <label className="block text-xs">
          <span className="block">branches (comma separated)</span>
          <input
            className="mt-1 w-full rounded border border-md-outline px-2 py-1"
            value={(node.branches ?? []).join(", ")}
            onChange={(changeEvent) => {
              const branches = changeEvent.target.value
                .split(",")
                .map((entry) => entry.trim())
                .filter((entry) => entry.length > 0);
              onDocumentChange(updateNode(document, node.name, (targetNode) => ({ ...targetNode, branches })));
            }}
          />
        </label>
      )}
      <div className="flex justify-end">
        <button
          type="button"
          className="rounded border border-rose-400 px-2 py-1 text-xs text-rose-700"
          onClick={() => {
            onDocumentChange({
              ...document,
              nodes: document.nodes.filter((entry) => entry.name !== node.name)
            });
            onClearSelection();
          }}
        >
          {labels.deleteNode}
        </button>
      </div>
    </section>
  );
}

function GraphInspector({
  document,
  selection,
  labels,
  actionValidationDetails,
  onDocumentChange,
  onClearSelection,
  onInspectingNodeNameChange
}: Readonly<GraphInspectorProps>) {
  const edgeSourceNode =
    selection?.kind === "edge"
      ? document.nodes.find((entry) => entry.name === selection.nodeName)
      : undefined;
  const schemas = useGraphActionSchemas({ document, edgeSourceNode });

  if (!selection) {
    return null;
  }

  if (selection.kind === "node") {
    const node = document.nodes.find((entry) => entry.name === selection.nodeName);
    if (!node) {
      return null;
    }
    return (
      <GraphNodeInspector
        document={document}
        node={node}
        labels={labels}
        actionValidationDetails={actionValidationDetails}
        loadActionSchema={schemas.loadActionSchema}
        getCachedActionSchema={schemas.getCachedActionSchema}
        actionCandidates={schemas.actionCandidates}
        actionCandidatesLoading={schemas.actionCandidatesLoading}
        onDocumentChange={onDocumentChange}
        onClearSelection={onClearSelection}
        onInspectingNodeNameChange={onInspectingNodeNameChange}
      />
    );
  }

  const sourceNode = document.nodes.find((node) => node.name === selection.nodeName);
  if (!sourceNode) {
    return null;
  }

  return (
    <GraphEdgeInspector
      document={document}
      sourceNode={sourceNode}
      selection={selection}
      labels={labels}
      whenPathHints={schemas.whenPathHints}
      onDocumentChange={onDocumentChange}
      onClearSelection={onClearSelection}
    />
  );
}

type GraphEdgeSelection = Extract<NonNullable<GraphSelection>, { kind: "edge" }>;

type GraphEdgeInspectorProps = {
  document: DefinitionGraphDocument;
  sourceNode: DefinitionGraphNode;
  selection: GraphEdgeSelection;
  labels: DefinitionGraphEditorProps["labels"];
  whenPathHints: string[];
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  onClearSelection: () => void;
};

/**
 * 選択中の辺メタデータからインスペクタ表示用の遷移先を解決する。
 *
 * @param sourceNode 辺の起点ノード
 * @param selection 辺選択
 * @returns 遷移先オブジェクト。解決不能なら null
 */
function resolveSelectedEdgeTarget(
  sourceNode: DefinitionGraphNode,
  selection: GraphEdgeSelection
): NonNullable<DefinitionGraphNode["edges"]>[number] | { to: string } | null {
  switch (selection.edgeKind) {
    case "next":
      return { to: sourceNode.next ?? "" };
    case "error":
      return { to: sourceNode.error ?? "" };
    case "waitEvent": {
      const eventName = selection.eventName;
      if (!eventName || !sourceNode.events) {
        return null;
      }
      return { to: sourceNode.events[eventName] ?? "" };
    }
    case "waitSubscribe": {
      const subscribeIndex = selection.subscribeIndex;
      if (subscribeIndex === undefined || sourceNode.subscribe === undefined) {
        return null;
      }
      return { to: sourceNode.subscribe[subscribeIndex]?.next ?? "" };
    }
    default:
      return (sourceNode.edges ?? [])[selection.edgeIndex ?? -1] ?? null;
  }
}

/**
 * 選択辺の遷移先をドキュメントへ反映する。
 *
 * @param document 定義グラフ
 * @param sourceNode 起点ノード
 * @param selection 辺選択
 * @param nextTarget 新しい遷移先
 * @returns 更新後ドキュメント
 */
function applySelectedEdgeTarget(
  document: DefinitionGraphDocument,
  sourceNode: DefinitionGraphNode,
  selection: GraphEdgeSelection,
  nextTarget: string
): DefinitionGraphDocument {
  switch (selection.edgeKind) {
    case "next":
      return updateNode(document, sourceNode.name, (node) => ({ ...node, next: nextTarget }));
    case "error":
      return updateNode(document, sourceNode.name, (node) =>
        node.type === "action" ? { ...node, error: nextTarget.trim() || undefined } : node
      );
    case "waitEvent":
      if (!selection.eventName) {
        return document;
      }
      return setWaitEventTarget(document, sourceNode.name, selection.eventName, nextTarget);
    case "waitSubscribe":
      if (selection.subscribeIndex === undefined) {
        return document;
      }
      return setWaitSubscribeTarget(document, sourceNode.name, selection.subscribeIndex, nextTarget);
    default:
      return updateNode(document, sourceNode.name, (node) => ({
        ...node,
        edges: (node.edges ?? []).map((edge, index) =>
          index === selection.edgeIndex ? { ...edge, to: nextTarget } : edge
        )
      }));
  }
}

/**
 * 選択辺をドキュメントから削除する。
 *
 * @param document 定義グラフ
 * @param sourceNode 起点ノード
 * @param selection 辺選択
 * @returns 更新後ドキュメント
 */
function removeSelectedEdge(
  document: DefinitionGraphDocument,
  sourceNode: DefinitionGraphNode,
  selection: GraphEdgeSelection
): DefinitionGraphDocument {
  switch (selection.edgeKind) {
    case "next":
      return updateNode(document, sourceNode.name, (node) => ({ ...node, next: undefined }));
    case "error":
      return updateNode(document, sourceNode.name, (node) =>
        node.type === "action" ? { ...node, error: undefined } : node
      );
    case "waitEvent":
      if (!selection.eventName) {
        return document;
      }
      return removeWaitEvent(document, sourceNode.name, selection.eventName);
    case "waitSubscribe":
      if (selection.subscribeIndex === undefined) {
        return document;
      }
      return removeWaitSubscribeRow(document, sourceNode.name, selection.subscribeIndex);
    default:
      return updateNode(document, sourceNode.name, (node) => ({
        ...node,
        edges: (node.edges ?? []).filter((_, index) => index !== selection.edgeIndex)
      }));
  }
}

function GraphEdgeInspector({
  document,
  sourceNode,
  selection,
  labels,
  whenPathHints,
  onDocumentChange,
  onClearSelection
}: Readonly<GraphEdgeInspectorProps>) {
  const targetEdge = resolveSelectedEdgeTarget(sourceNode, selection);
  if (!targetEdge) {
    return null;
  }

  const conditionalEdge: NonNullable<DefinitionGraphNode["edges"]>[number] | undefined =
    selection.edgeKind === "edge" ? targetEdge : undefined;
  const selectedWhenOp = (conditionalEdge?.when?.op ?? "").toUpperCase();
  const isDefaultEdge = conditionalEdge?.default === true;
  const isWhenFieldsDisabled = isDefaultEdge;
  const isWhenValueDisabled = selectedWhenOp === "EXISTS";
  let whenValueHint: string | null = null;
  if (selectedWhenOp === "IN") {
    whenValueHint = labels.whenValueHintIn;
  } else if (selectedWhenOp === "BETWEEN") {
    whenValueHint = labels.whenValueHintBetween;
  }

  return (
    <section className="space-y-2 rounded border border-md-outline-variant bg-md-surface-container p-3">
      <p className="text-sm font-medium">{labels.edgeInspectorTitle}</p>
      <label className="block text-xs">
        <span className="block">to</span>
        <input
          className="mt-1 w-full rounded border border-md-outline px-2 py-1"
          value={targetEdge.to}
          onChange={(changeEvent) => {
            onDocumentChange(
              applySelectedEdgeTarget(document, sourceNode, selection, changeEvent.target.value)
            );
          }}
        />
      </label>
      {selection.edgeKind === "edge" && (
        <div className="space-y-2">
          <label className="inline-flex items-center gap-2 text-xs">
            <input
              type="checkbox"
              checked={conditionalEdge?.default === true}
              onChange={(changeEvent) => {
                const isDefault = changeEvent.target.checked;
                onDocumentChange(
                  updateNode(document, sourceNode.name, (node) => ({
                    ...node,
                    edges: (node.edges ?? []).map((edge, index) =>
                      index === selection.edgeIndex
                        ? {
                            ...edge,
                            default: isDefault ? true : undefined,
                            ...(isDefault ? { when: undefined } : {})
                          }
                        : edge
                    )
                  }))
                );
              }}
            />
            <span>default</span>
          </label>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <label className="block text-xs">
              <span className="block">when.path</span>
              <input
                className="mt-1 w-full rounded border border-md-outline px-2 py-1"
                value={conditionalEdge?.when?.path ?? ""}
                placeholder={labels.whenPathPlaceholder}
                disabled={isWhenFieldsDisabled}
                list={whenPathHints.length > 0 ? `when-path-hints-${sourceNode.name}` : undefined}
                onChange={(changeEvent) => {
                  const path = changeEvent.target.value;
                  onDocumentChange(
                    updateNode(document, sourceNode.name, (node) => ({
                      ...node,
                      edges: (node.edges ?? []).map((edge, index) =>
                        index === selection.edgeIndex
                          ? { ...edge, when: { path, op: edge.when?.op ?? "eq", value: edge.when?.value ?? "" } }
                          : edge
                      )
                    }))
                  );
                }}
              />
              {whenPathHints.length > 0 ? (
                <datalist id={`when-path-hints-${sourceNode.name}`}>
                  {whenPathHints.map((hint) => (
                    <option key={hint} value={hint} />
                  ))}
                </datalist>
              ) : null}
              <span className="mt-0.5 block text-[10px] text-md-on-surface-variant">
                {labels.whenPathHint}
              </span>
            </label>
            <label className="block text-xs">
              <span className="block">when.op</span>
              <select
                className="mt-1 w-full rounded border border-md-outline px-2 py-1"
                value={selectedWhenOp}
                disabled={isWhenFieldsDisabled}
                onChange={(changeEvent) => {
                  const op = changeEvent.target.value.toUpperCase();
                  onDocumentChange(
                    updateNode(document, sourceNode.name, (node) => ({
                      ...node,
                      edges: (node.edges ?? []).map((edge, index) =>
                        index === selection.edgeIndex
                          ? {
                              ...edge,
                              when: {
                                path: edge.when?.path ?? "$.input.x",
                                op,
                                value: op === "EXISTS" ? undefined : (edge.when?.value ?? "")
                              }
                            }
                          : edge
                      )
                    }))
                  );
                }}
              >
                <option value="" disabled>
                  {labels.whenOpPlaceholder}
                </option>
                {WHEN_OP_OPTIONS.map((op) => (
                  <option key={op.value} value={op.value}>
                    {op.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="block text-xs">
              <span className="block">when.value</span>
              <input
                className="mt-1 w-full rounded border border-md-outline px-2 py-1"
                value={formatWhenValue(conditionalEdge?.when?.value)}
                placeholder={labels.whenValuePlaceholder}
                disabled={isWhenFieldsDisabled || isWhenValueDisabled}
                onChange={(changeEvent) => {
                  const value = parseWhenValueInput(changeEvent.target.value, selectedWhenOp);
                  onDocumentChange(
                    updateNode(document, sourceNode.name, (node) => ({
                      ...node,
                      edges: (node.edges ?? []).map((edge, index) =>
                        index === selection.edgeIndex
                          ? { ...edge, when: { path: edge.when?.path ?? "$.x", op: edge.when?.op ?? "eq", value } }
                          : edge
                      )
                    }))
                  );
                }}
              />
              {!isWhenFieldsDisabled && isWhenValueDisabled && (
                <span className="mt-1 block text-[11px] text-md-on-surface-variant">
                  {labels.whenValueDisabledForExists}
                </span>
              )}
              {!isWhenFieldsDisabled && !isWhenValueDisabled && whenValueHint && (
                <span className="mt-1 block text-[11px] text-md-on-surface-variant">
                  {whenValueHint}
                </span>
              )}
            </label>
          </div>
        </div>
      )}
      <div className="flex justify-end">
        <button
          type="button"
          className="rounded border border-rose-400 px-2 py-1 text-xs text-rose-700"
          onClick={() => {
            onDocumentChange(removeSelectedEdge(document, sourceNode, selection));
            onClearSelection();
          }}
        >
          {labels.deleteEdge}
        </button>
      </div>
    </section>
  );
}
