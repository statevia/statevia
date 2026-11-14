"use client";

import type { ExecutionNodeDTO, ExecutionView } from "../types";
import { formatTracePayload } from "../lib/formatExecutionTrace";
import { resolveWaitResumeEvents } from "../lib/waitResumeEvents";
import { toDisplayValue } from "@/shared/lib/displayValue";
import { getStatusStyle } from "@/shared/lib/statusStyle";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { NodeDetailStatusPanels, NodeDetailTraceSection } from "./nodeDetailSections";
import { Button } from "@/shared/ui/Button";

type NodeDetailProps = {
  execution: ExecutionView | null;
  node: ExecutionNodeDTO | null;
  loading: boolean;
  /** このノードを届け先に選んだ状態でイベント送信ダイアログを開く。 */
  onSendToNode: () => void;
  /** このノードへ送れない理由。送れるときは null。 */
  resumeDisabledReason: string | null;
  /** WAITING ノードの Resume に必要なイベント名（グラフ定義の Resume エッジから取得） */
  resumeEventName?: string | null;
  /** false のとき「このノードへ送る」を表示しない（executions.write が無いとき）。 */
  showSendAction?: boolean;
  className?: string;
  /**
   * 同一状態の訪問移動。未指定または visible が false のときは出さない。
   * ボタン文言の出し分け以外の状態は持たない。
   */
  nodeVisitNavigation?: {
    visible: boolean;
    attempt: number;
    canJumpToLatest: boolean;
    canStepNewer: boolean;
    canStepOlder: boolean;
    canJumpToFirst: boolean;
    onJumpToLatest: () => void;
    onStepNewer: () => void;
    onStepOlder: () => void;
    onJumpToFirst: () => void;
  };
};

/** ノード詳細パネル用の派生表示値。 */
type NodeDetailDerivedFields = {
  style: ReturnType<typeof getStatusStyle>;
  isWaiting: boolean;
  isCanceled: boolean;
  isFailed: boolean;
  nodeNameText: string;
  outputText: string;
  inputText: string;
  conditionRoutingText: string;
  showTracePanel: boolean;
  allowedEventsLabel: string;
};

/**
 * ノード詳細表示用の派生フィールドを計算する。
 */
function deriveNodeDetailFields(
  node: ExecutionNodeDTO,
  resumeEvents: string[]
): NodeDetailDerivedFields {
  return {
    style: getStatusStyle(node.status),
    isWaiting: node.status === "WAITING",
    isCanceled: node.status === "CANCELED",
    isFailed: node.status === "FAILED",
    nodeNameText: typeof node.nodeName === "string" ? node.nodeName.trim() : "",
    outputText: "output" in node && node.output !== undefined ? formatTracePayload(node.output) : "",
    inputText: "input" in node && node.input !== undefined ? formatTracePayload(node.input) : "",
    conditionRoutingText:
      "conditionRouting" in node && node.conditionRouting !== undefined
        ? formatTracePayload(node.conditionRouting)
        : "",
    showTracePanel:
      (node.startedAt != null && node.startedAt !== "") ||
      (node.completedAt != null && node.completedAt !== "") ||
      ("input" in node && node.input !== undefined) ||
      ("output" in node && node.output !== undefined) ||
      ("conditionRouting" in node && node.conditionRouting !== undefined),
    allowedEventsLabel: resumeEvents.length > 0 ? resumeEvents.join(", ") : "—"
  };
}

type NodeDetailSendActionProps = {
  loading: boolean;
  resumeDisabledReason: string | null;
  onSendToNode: () => void;
};

/**
 * 待機中のノードの「このノードへ送る」（要件 5.5）。届け先とイベントはイベント送信ダイアログで選ぶ。
 */
function NodeDetailSendAction({ loading, resumeDisabledReason, onSendToNode }: Readonly<NodeDetailSendActionProps>) {
  const uiText = useUiText();
  return (
    <div className="mt-3 space-y-2 border-t border-md-outline pt-3">
      <p className="text-xs text-md-on-surface-variant">{uiText.executionDashboard.sendEvent.nodeOptionHint(null)}</p>
      <Button variant="standard" disabled={!!resumeDisabledReason || loading} onClick={onSendToNode}>
        {uiText.executionDashboard.sendEvent.sendToNode}
      </Button>
      {resumeDisabledReason && <p className="text-xs text-md-on-surface-variant">{resumeDisabledReason}</p>}
    </div>
  );
}

/** 選択ノードの詳細パネル。 */
export function NodeDetail({
  execution,
  node,
  loading,
  onSendToNode,
  resumeDisabledReason,
  resumeEventName,
  showSendAction = true,
  className,
  nodeVisitNavigation
}: Readonly<NodeDetailProps>) {
  const uiText = useUiText();
  const baseClassName = "rounded-2xl border border-md-outline bg-md-surface p-4 shadow-xs";
  const asideClassName = className ? `${baseClassName} ${className}` : baseClassName;
  const resumeEvents = node ? resolveWaitResumeEvents(node) : [];

  if (!execution) {
    return (
      <aside className={asideClassName}>
        <p className="text-sm text-md-on-surface-variant">{uiText.nodeDetail.prompts.loadExecution(uiText.entities.execution)}</p>
      </aside>
    );
  }

  if (!node) {
    return (
      <aside className={asideClassName}>
        <p className="text-sm text-md-on-surface-variant">{uiText.nodeDetail.prompts.selectNode(uiText.entities.node)}</p>
      </aside>
    );
  }

  const fields = deriveNodeDetailFields(node, resumeEvents);

  return (
    <aside className={asideClassName}>
      <div className="flex h-5 items-center justify-between gap-2">
        <h2 className="text-sm font-semibold leading-5">{uiText.nodeDetail.title(uiText.entities.node)}</h2>
        {nodeVisitNavigation?.visible && (
          <div className="flex h-5 items-center gap-1">
            <button
              type="button"
              className="h-5 rounded-sm border border-md-outline bg-md-surface px-1.5 font-mono text-xs leading-5 text-md-on-surface hover:bg-md-surface-container disabled:cursor-not-allowed disabled:opacity-40"
              aria-label={uiText.nodeDetail.visitNavigation.jumpToLatest}
              disabled={!nodeVisitNavigation.canJumpToLatest}
              onClick={nodeVisitNavigation.onJumpToLatest}
            >
              {"<<"}
            </button>
            <button
              type="button"
              className="h-5 rounded-sm border border-md-outline bg-md-surface px-1.5 font-mono text-xs leading-5 text-md-on-surface hover:bg-md-surface-container disabled:cursor-not-allowed disabled:opacity-40"
              aria-label={uiText.nodeDetail.visitNavigation.stepNewer}
              disabled={!nodeVisitNavigation.canStepNewer}
              onClick={nodeVisitNavigation.onStepNewer}
            >
              {"<"}
            </button>
            <span className="px-1 text-xs leading-5 text-md-on-surface">{uiText.nodeDetail.meta.attempt(nodeVisitNavigation.attempt)}</span>
            <button
              type="button"
              className="h-5 rounded-sm border border-md-outline bg-md-surface px-1.5 font-mono text-xs leading-5 text-md-on-surface hover:bg-md-surface-container disabled:cursor-not-allowed disabled:opacity-40"
              aria-label={uiText.nodeDetail.visitNavigation.stepOlder}
              disabled={!nodeVisitNavigation.canStepOlder}
              onClick={nodeVisitNavigation.onStepOlder}
            >
              {">"}
            </button>
            <button
              type="button"
              className="h-5 rounded-sm border border-md-outline bg-md-surface px-1.5 font-mono text-xs leading-5 text-md-on-surface hover:bg-md-surface-container disabled:cursor-not-allowed disabled:opacity-40"
              aria-label={uiText.nodeDetail.visitNavigation.jumpToFirst}
              disabled={!nodeVisitNavigation.canJumpToFirst}
              onClick={nodeVisitNavigation.onJumpToFirst}
            >
              {">>"}
            </button>
          </div>
        )}
      </div>
      <div className={`mt-3 rounded-xl border p-3 ${fields.style.borderClass} ${fields.style.bgClass}`}>
        <div className="flex items-center justify-between">
          <div className="font-mono text-xs">{uiText.nodeDetail.meta.nodeId(node.nodeId)}</div>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${fields.style.badgeClass}`}>
            {toDisplayValue(node.status)}
          </span>
        </div>
        <div className="mt-2 space-y-1 text-xs text-md-on-surface">
          <div>{uiText.nodeDetail.meta.type(toDisplayValue(node.nodeType))}</div>
          {fields.nodeNameText !== "" && <div>{uiText.nodeDetail.meta.nodeName(fields.nodeNameText)}</div>}
          <div>{uiText.nodeDetail.meta.attempt(node.attempt)}</div>
          <div>{uiText.nodeDetail.meta.waitKey(node.waitKey ?? "—")}</div>
          <div>{uiText.nodeDetail.meta.allowedEvents(fields.allowedEventsLabel)}</div>

          {fields.showTracePanel && (
            <NodeDetailTraceSection
              node={node}
              inputText={fields.inputText}
              outputText={fields.outputText}
              conditionRoutingText={fields.conditionRoutingText}
            />
          )}

          <NodeDetailStatusPanels
            node={node}
            isWaiting={fields.isWaiting}
            isCanceled={fields.isCanceled}
            isFailed={fields.isFailed}
            resumeEventName={resumeEventName}
          />
        </div>
      </div>
      {showSendAction && fields.isWaiting && (
        <NodeDetailSendAction loading={loading} resumeDisabledReason={resumeDisabledReason} onSendToNode={onSendToNode} />
      )}
    </aside>
  );
}
