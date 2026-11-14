"use client";

import type { ExecutionNodeDTO } from "../types";
import { formatExecutionDuration, formatExecutionInstant } from "@/shared/lib/dateTime";
import { useLocale, useUiText } from "@/shared/i18n/uiTextContext";

type TracePayloadDisclosureProps = {
  heading: string;
  payloadText: string;
  emptyLabel: string;
};

function TracePayloadDisclosure({ heading, payloadText, emptyLabel }: Readonly<TracePayloadDisclosureProps>) {
  const display = payloadText === "" ? emptyLabel : payloadText;
  return (
    <details className="mt-1 rounded-lg border border-md-outline-variant bg-(--md-sys-color-surface-container-high)/60">
      <summary className="cursor-pointer select-none list-none px-2 py-1.5 text-xs font-medium text-md-on-surface outline-hidden marker:content-none [&::-webkit-details-marker]:hidden">
        {heading}
      </summary>
      <pre className="mx-2 mb-2 max-h-40 overflow-auto whitespace-pre-wrap wrap-break-word rounded-lg bg-md-surface-container-high p-2 text-[10px] leading-snug text-md-on-surface">
        {display}
      </pre>
    </details>
  );
}

type NodeDetailTraceSectionProps = {
  node: ExecutionNodeDTO;
  inputText: string;
  outputText: string;
  conditionRoutingText: string;
};

function NodeDetailTraceDuration({ node }: Readonly<Pick<NodeDetailTraceSectionProps, "node">>) {
  const uiText = useUiText();
  const durationText = formatExecutionDuration(node.startedAt, node.completedAt);
  if (durationText != null) {
    return <div>{uiText.nodeDetail.trace.duration(durationText)}</div>;
  }
  if (
    node.startedAt != null &&
    node.startedAt !== "" &&
    node.completedAt != null &&
    node.completedAt !== ""
  ) {
    return (
      <div className="text-md-on-surface-variant">
        {uiText.nodeDetail.trace.durationUnavailable}
      </div>
    );
  }
  return null;
}

/** ノード実行トレース（時刻・入出力）を表示する。 */
export function NodeDetailTraceSection({
  node,
  inputText,
  outputText,
  conditionRoutingText
}: Readonly<NodeDetailTraceSectionProps>) {
  const uiText = useUiText();
  const locale = useLocale();
  return (
    <div className="mt-2 space-y-1 border-t border-md-outline-variant pt-2">
      {node.startedAt != null && node.startedAt !== "" && (
        <div>{uiText.nodeDetail.trace.startedAt(formatExecutionInstant(node.startedAt, locale))}</div>
      )}
      {node.completedAt != null && node.completedAt !== "" && (
        <div>{uiText.nodeDetail.trace.completedAt(formatExecutionInstant(node.completedAt, locale))}</div>
      )}
      <NodeDetailTraceDuration node={node} />
      {"input" in node && node.input !== undefined && (
        <TracePayloadDisclosure
          heading={uiText.nodeDetail.trace.inputHeading}
          payloadText={inputText}
          emptyLabel={uiText.nodeDetail.trace.inputEmpty}
        />
      )}
      {"output" in node && node.output !== undefined && (
        <TracePayloadDisclosure
          heading={uiText.nodeDetail.trace.outputHeading}
          payloadText={outputText}
          emptyLabel={uiText.nodeDetail.trace.outputEmpty}
        />
      )}
      {"conditionRouting" in node && node.conditionRouting !== undefined && (
        <TracePayloadDisclosure
          heading={uiText.nodeDetail.trace.conditionRoutingHeading}
          payloadText={conditionRoutingText}
          emptyLabel={uiText.nodeDetail.trace.conditionRoutingEmpty}
        />
      )}
    </div>
  );
}

type NodeDetailStatusPanelsProps = {
  node: ExecutionNodeDTO;
  isWaiting: boolean;
  isCanceled: boolean;
  isFailed: boolean;
  resumeEventName?: string | null;
};

/** WAITING / CANCELED / FAILED の状態別詳細を表示する。 */
export function NodeDetailStatusPanels({
  node,
  isWaiting,
  isCanceled,
  isFailed,
  resumeEventName
}: Readonly<NodeDetailStatusPanelsProps>) {
  const uiText = useUiText();
  return (
    <>
      {isWaiting && (
        <div className="mt-2 rounded-lg border p-2 border-md-warning/40 bg-md-warning-container text-md-on-warning-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-warning-container)_30%,transparent)]">
          <div className="font-medium">{uiText.nodeDetail.waiting.title}</div>
          <div className="mt-1">
            <div>{uiText.nodeDetail.waiting.reasonWaitByWaitKeyAndResumeWait}</div>
            {resumeEventName != null && resumeEventName !== "" && (
              <div className="mt-0.5 font-medium">{uiText.nodeDetail.waiting.resumeEventName(resumeEventName)}</div>
            )}
          </div>
        </div>
      )}
      {isCanceled && (
        <div className="mt-2 rounded-lg border p-2 border-md-error/40 bg-md-error-container text-md-on-error-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-error-container)_30%,transparent)]">
          <div className="font-medium">{uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel)}</div>
          <div className="mt-1 space-y-0.5">
            {node.cancelReason != null && node.cancelReason !== "" && <div>{uiText.nodeDetail.cancel.reason(node.cancelReason)}</div>}
            {node.canceledByExecution && (
              <div className="rounded-sm bg-md-surface/60 px-2 py-1">{uiText.nodeDetail.cancel.convergedByExecutionCancel}</div>
            )}
          </div>
        </div>
      )}
      {isFailed && (
        <div className="mt-2 rounded-lg border p-2 border-md-error/40 bg-md-error-container text-md-on-error-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-error-container)_30%,transparent)]">
          <div className="font-medium">{uiText.nodeDetail.failure.title}</div>
          <div className="mt-1">
            {node.error?.message != null && node.error.message !== "" ? (
              <div className="wrap-break-word">{node.error.message}</div>
            ) : (
              <div className="opacity-80">{uiText.nodeDetail.failure.noMessage}</div>
            )}
          </div>
        </div>
      )}
    </>
  );
}
