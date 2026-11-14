"use client";

import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import {
  useExecutionDashboard,
  type ExecutionDashboardModel,
  type UseExecutionDashboardOptions
} from "../hooks/useExecutionDashboard";
import { ExecutionComparisonBar } from "./ExecutionComparisonBar";
import { ExecutionHeader } from "./ExecutionHeader";
import { ExecutionStatusBanner } from "./ExecutionStatusBanner";
import { ExecutionTimeline } from "./ExecutionTimeline";
import { ExecutionWaitNotice } from "./ExecutionWaitNotice";
import { NodeDetail } from "./NodeDetail";
import { NodeGraphView } from "./NodeGraphView";
import { NodeListView } from "./NodeListView";
import { ReplayBanner } from "./ReplayBanner";
import { SendEventDialog } from "./SendEventDialog";
import { ViewToggle } from "./ViewToggle";
import { Button } from "@/shared/ui/Button";
import { Notice } from "@/shared/ui/Notice";

/** 実行ダッシュボードの props。 */
export type ExecutionDashboardProps = UseExecutionDashboardOptions;

type ExecutionDashboardViewProps = ExecutionDashboardModel;

/**
 * 1 つの実行の詳細（見出しの操作・待機のお知らせ・タイムライン・ノードの一覧とグラフ・イベント送信）。
 * `/executions/[executionId]` から利用する。
 * @param props 初期実行 ID と、比較の有無。
 * @returns 実行の詳細。
 */
export function ExecutionDashboard(options: Readonly<ExecutionDashboardProps>) {
  const dashboard = useExecutionDashboard(options);
  return <ExecutionDashboardView {...dashboard} />;
}

function ExecutionDashboardView({
  graphFullscreen,
  toast,
  onCloseToast,
  executionId,
  onCancelExecution,
  loading,
  canCancel,
  waits,
  canSendEvent,
  sendEventDialog,
  onOpenSendEvent,
  onCloseSendEvent,
  onSendEvent,
  sendingEvent,
  execution,
  viewMode,
  onViewModeChange,
  compareMode,
  onCompareModeChange,
  comparisonEnabled,
  operationsEnabled,
  streamEnabled,
  onStreamEnabledChange,
  showExecutionPanels,
  executionB,
  executionIdB,
  onExecutionIdBChange,
  onLoadExecutionB,
  loadingB,
  executionDiff,
  onSelectNode,
  onSelectListedNode,
  terminal,
  isReplaying,
  onBackToCurrent,
  timelineEvents,
  timelineLoading,
  timelineError,
  replayAtSeq,
  onSelectSeq,
  timelineHasMore,
  timelineLoadingMore,
  onTimelineLoadMore,
  displayExecution,
  selectedNodeId,
  graphData,
  onToggleGraphFullscreen,
  getResumeDisabledReasonForNode,
  savedGraphViewport,
  onGraphViewportChange,
  selectedNode,
  selectedResumeDisabledReason,
  resumeEventName,
  nodeVisitNavigation
}: Readonly<ExecutionDashboardViewProps>) {
  const uiText = useUiText();
  const showSendEventAction = canSendEvent && waits.length > 0;

  const graphWrapperClassName = graphFullscreen ? "fixed inset-0 z-50 bg-md-surface-container-high p-4" : "";
  const graphMainClassName = graphFullscreen
    ? "mx-auto grid h-full max-w-[1600px] gap-4 lg:grid-cols-[minmax(0,1.8fr)_380px]"
    : "grid gap-4 lg:grid-cols-[1.6fr_1fr]";
  const graphSectionClassName = graphFullscreen ? "min-h-0" : "";
  const graphContainerClassName = `space-y-2 ${graphFullscreen ? "flex h-full min-h-0 flex-col" : ""}`;

  return (
    <div className={graphFullscreen ? "" : "space-y-4"}>
      {!graphFullscreen && (
        <>
          <Breadcrumbs
            items={[
              { label: uiText.navigation.groups.workflow },
              { label: uiText.lists.executions, href: "/executions" },
              { label: executionId }
            ]}
          />

          <ExecutionHeader
            executionId={executionId}
            execution={execution}
            waiting={waits.length > 0}
            loading={loading}
            canCancel={canCancel}
            showCancelAction={operationsEnabled}
            onCancel={onCancelExecution}
            showCompareAction={comparisonEnabled && showExecutionPanels}
            compareMode={compareMode}
            onCompareModeChange={onCompareModeChange}
            showSendEventAction={showSendEventAction}
            onOpenSendEvent={() => onOpenSendEvent(null)}
          />

          <Toast toast={toast} onClose={onCloseToast} />

          {showExecutionPanels && !terminal && !execution?.cancelRequested && (
            <ExecutionWaitNotice waits={waits} onSendTo={canSendEvent ? onOpenSendEvent : undefined} />
          )}

          {comparisonEnabled && compareMode && showExecutionPanels && (
            <ExecutionComparisonBar
              executionLeft={execution}
              executionRight={executionB}
              executionIdRight={executionIdB}
              onExecutionIdRightChange={onExecutionIdBChange}
              onLoadRight={onLoadExecutionB}
              loadingRight={loadingB}
              diff={executionDiff}
              onSelectDiffNode={onSelectNode}
            />
          )}

          <ExecutionStatusBanner cancelRequested={!!execution?.cancelRequested} terminal={terminal} />

          {!loading && !showExecutionPanels && (
            <PageState state="error" message={uiText.executionDashboard.errors.executionNotFound} />
          )}

          {showExecutionPanels && isReplaying && (
            <ReplayBanner onBackToCurrent={onBackToCurrent} />
          )}

          {showExecutionPanels && (
            <ExecutionTimeline
              events={timelineEvents}
              loading={timelineLoading}
              error={timelineError}
              selectedSeq={replayAtSeq}
              onSelectSeq={onSelectSeq}
              onBackToCurrent={onBackToCurrent}
              isReplaying={isReplaying}
              hasMore={timelineHasMore}
              loadingMore={timelineLoadingMore}
              onLoadMore={onTimelineLoadMore}
            />
          )}
        </>
      )}

      {showExecutionPanels && !graphFullscreen && (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <ViewToggle value={viewMode} onChange={onViewModeChange} />
          <label className="flex cursor-pointer items-center gap-2 text-sm text-md-on-surface">
            <input
              type="checkbox"
              checked={streamEnabled}
              onChange={(event) => onStreamEnabledChange(event.target.checked)}
              className="h-4 w-4 accent-[var(--md-sys-color-primary)]"
            />
            <span>{uiText.executionHeader.realtimeSseLabel}</span>
          </label>
        </div>
      )}

      {showExecutionPanels && (
        <div className={graphWrapperClassName}>
          <main className={graphMainClassName}>
            <section className={graphSectionClassName}>
              {viewMode === "list" ? (
                <NodeListView
                  nodes={displayExecution?.nodes ?? []}
                  selectedNodeId={selectedNodeId}
                  onSelectNode={onSelectListedNode}
                />
              ) : (
                <div className={graphContainerClassName}>
                  <div className="flex justify-end">
                    <Button variant="outline" size="sm" onClick={onToggleGraphFullscreen}>
                      {graphFullscreen
                        ? uiText.executionDashboard.graph.fullscreenExit
                        : uiText.executionDashboard.graph.fullscreenEnter}
                    </Button>
                  </div>
                  {graphData && !graphData.definitionBased && !graphFullscreen && (
                    <Notice tone="warning" title={uiText.executionDashboard.graph.definitionMissingFallback} />
                  )}
                  {graphData && (
                    <NodeGraphView
                      nodes={graphData.nodes}
                      edges={graphData.edges}
                      groups={graphData.groups}
                      selectedNodeId={selectedNodeId}
                      selectedNodeName={selectedNode?.nodeName ?? null}
                      onSelectNode={onSelectNode}
                      onSendToNode={onOpenSendEvent}
                      getResumeDisabledReason={getResumeDisabledReasonForNode}
                      showSendAction={canSendEvent}
                      defaultViewport={savedGraphViewport}
                      onViewportChange={onGraphViewportChange}
                      heightClassName={graphFullscreen ? "h-full min-h-[360px]" : undefined}
                      nodeDiffHighlight={compareMode ? executionDiff?.nodeHighlights : undefined}
                    />
                  )}
                </div>
              )}
            </section>

            <NodeDetail
              execution={displayExecution ?? execution}
              node={selectedNode}
              loading={loading}
              onSendToNode={() => {
                if (selectedNode) onOpenSendEvent(selectedNode.nodeId);
              }}
              resumeDisabledReason={selectedResumeDisabledReason}
              resumeEventName={resumeEventName}
              nodeVisitNavigation={nodeVisitNavigation}
              showSendAction={canSendEvent}
              className={graphFullscreen ? "h-full min-h-0 overflow-auto" : undefined}
            />
          </main>
        </div>
      )}

      {sendEventDialog && execution && (
        <SendEventDialog
          executionDisplayId={execution.displayId}
          waits={waits}
          initialNodeId={sendEventDialog.initialNodeId}
          sending={sendingEvent}
          onClose={onCloseSendEvent}
          onSend={onSendEvent}
        />
      )}
    </div>
  );
}
