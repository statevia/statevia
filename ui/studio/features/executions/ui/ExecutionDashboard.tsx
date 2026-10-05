"use client";

import { useMemo, useState, type ReactNode } from "react";
import { ActionLinkGroup } from "@/shared/ui/ActionLinkGroup";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { isWithinMaxLength, matchesPattern } from "@/shared/lib/validation/primitives";
import { EVENT_NAME_MAX_LENGTH, EVENT_NAME_PATTERN } from "@/shared/lib/validation/formRules";
import {
  useExecutionDashboard,
  type ExecutionDashboardModel,
  type UseExecutionDashboardOptions
} from "../hooks/useExecutionDashboard";
import { isPublishEventAvailable } from "../lib/waitResumeEvents";
import { ExecutionComparisonBar } from "./ExecutionComparisonBar";
import { ExecutionHeader } from "./ExecutionHeader";
import { ExecutionStatusBanner } from "./ExecutionStatusBanner";
import { ExecutionTimeline } from "./ExecutionTimeline";
import { NodeDetail } from "./NodeDetail";
import { NodeGraphView } from "./NodeGraphView";
import { NodeListView } from "./NodeListView";
import { ReplayBanner } from "./ReplayBanner";

/** 実行ダッシュボードの props。 */
export type ExecutionDashboardProps = UseExecutionDashboardOptions & {
  /** ヘッダ右側のナビ。未指定時は dashboard / executions / health。 */
  headerNav?: ReactNode;
};

type ExecutionDashboardViewProps = ExecutionDashboardModel & {
  /** ヘッダ右側のナビ。未指定時は既定リンク。 */
  headerNav?: ReactNode;
};

/**
 * 実行一覧・グラフ・タイムライン・ノード操作の共通ダッシュボード。
 * `/dashboard` や `/executions/[executionId]` から利用する。
 * @param props 初期実行 ID と、ヘッダ・比較・操作の有無。
 * @returns ダッシュボード。
 */
export function ExecutionDashboard({ headerNav, ...options }: Readonly<ExecutionDashboardProps>) {
  const dashboard = useExecutionDashboard(options);
  return <ExecutionDashboardView headerNav={headerNav} {...dashboard} />;
}

function ExecutionDashboardView({
  graphFullscreen,
  headerTitle,
  headerNav,
  toast,
  onCloseToast,
  executionId,
  executionIdEditable,
  onExecutionIdChange,
  onLoadExecution,
  onCancelExecution,
  loading,
  canCancel,
  onPublishEvent,
  execution,
  viewMode,
  onViewModeChange,
  showViewToggle,
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
  onResumeNode,
  onResumeSelectedNode,
  getResumeDisabledReasonForNode,
  savedGraphViewport,
  onGraphViewportChange,
  selectedNode,
  selectedResumeDisabledReason,
  resumeEventName,
  nodeVisitNavigation
}: Readonly<ExecutionDashboardViewProps>) {
  const uiText = useUiText();
  const [eventName, setEventName] = useState("");
  const trimmedEventName = eventName.trim();
  const eventNameValidationMessage = useMemo(() => {
    if (!trimmedEventName) return null;
    if (!isWithinMaxLength(trimmedEventName, EVENT_NAME_MAX_LENGTH)) {
      return uiText.executionDashboard.validation.eventNameTooLong;
    }
    if (!matchesPattern(trimmedEventName, EVENT_NAME_PATTERN)) {
      return uiText.executionDashboard.validation.eventNameInvalidFormat;
    }
    return null;
  }, [trimmedEventName, uiText.executionDashboard.validation.eventNameInvalidFormat, uiText.executionDashboard.validation.eventNameTooLong]);
  const defaultHeaderNav = (
    <ActionLinkGroup
      links={[
        { label: uiText.navigation.dashboard, href: "/dashboard", priority: "primary" },
        { label: uiText.lists.executions, href: "/executions" },
        { label: uiText.navigation.health, href: "/health" }
      ]}
    />
  );

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
          <header className="flex flex-col items-start gap-3 rounded-2xl border border-md-outline bg-md-surface px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
            <h1 className="text-xl font-bold text-md-on-surface">{headerTitle}</h1>
            {headerNav ?? defaultHeaderNav}
          </header>

          <ExecutionHeader
            executionId={executionId}
            executionIdEditable={executionIdEditable}
            onExecutionIdChange={onExecutionIdChange}
            onLoad={onLoadExecution}
            onCancel={onCancelExecution}
            loading={loading}
            canCancel={canCancel}
            execution={execution}
            viewMode={viewMode}
            onViewModeChange={onViewModeChange}
            showViewToggle={showViewToggle}
            compareMode={compareMode}
            onCompareModeChange={comparisonEnabled ? onCompareModeChange : undefined}
            streamEnabled={streamEnabled}
            onStreamEnabledChange={onStreamEnabledChange}
            showCancelAction={operationsEnabled}
          />

          <Toast toast={toast} onClose={onCloseToast} />

          {operationsEnabled && showExecutionPanels && isPublishEventAvailable(execution) && (
            <section className="rounded-2xl border border-md-outline bg-md-surface p-4 shadow-sm">
              <h2 className="text-sm font-semibold text-md-on-surface">{uiText.executionDashboard.actions.sectionTitle}</h2>
              <div className="mt-3 flex flex-wrap items-end gap-2">
                <label className="block min-w-[14rem] flex-1 text-xs text-md-on-surface-variant">
                  <span>{uiText.executionDashboard.actions.eventNameLabel}</span>
                  <input
                    className="mt-1 w-full rounded-xl border border-md-outline-variant bg-md-surface-container px-3 py-2 text-sm text-md-on-surface outline-none focus:border-md-primary"
                    value={eventName}
                    onChange={(event) => setEventName(event.target.value)}
                    placeholder={uiText.executionDashboard.actions.eventNamePlaceholder}
                    autoComplete="off"
                  />
                </label>
                <button
                  type="button"
                  className="rounded-xl border border-md-outline-variant bg-md-surface-container px-3 py-2 text-sm text-md-on-surface hover:bg-md-surface-container-high disabled:opacity-50"
                  disabled={loading || !trimmedEventName || !execution || terminal || !!eventNameValidationMessage}
                  onClick={() => {
                    onPublishEvent(trimmedEventName);
                    setEventName("");
                  }}
                >
                  {uiText.actions.sendEvent}
                </button>
              </div>
              {eventNameValidationMessage && (
                <p className="mt-2 text-xs text-rose-600">{eventNameValidationMessage}</p>
              )}
              <p className="mt-2 text-xs text-md-on-surface-variant">
                {uiText.executionDashboard.operationsAggregatedInRun(
                  uiText.actions.cancel,
                  uiText.actions.resume,
                  uiText.actions.sendEvent
                )}
              </p>
            </section>
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
                    <button
                      type="button"
                      className="rounded-xl border border-md-outline-variant bg-md-surface-container px-3 py-1.5 text-xs font-semibold text-md-on-surface hover:bg-md-surface-container-high"
                      onClick={onToggleGraphFullscreen}
                    >
                      {graphFullscreen
                        ? uiText.executionDashboard.graph.fullscreenExit
                        : uiText.executionDashboard.graph.fullscreenEnter}
                    </button>
                  </div>
                  {graphData && !graphData.definitionBased && !graphFullscreen && (
                    <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                      {uiText.executionDashboard.graph.definitionMissingFallback(graphData.graphId)}
                    </div>
                  )}
                  {graphData && (
                    <NodeGraphView
                      nodes={graphData.nodes}
                      edges={graphData.edges}
                      groups={graphData.groups}
                      selectedNodeId={selectedNodeId}
                      selectedNodeName={selectedNode?.nodeName ?? null}
                      onSelectNode={onSelectNode}
                      onResumeNode={onResumeNode}
                      getResumeDisabledReason={getResumeDisabledReasonForNode}
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
              onResume={onResumeSelectedNode}
              resumeDisabledReason={selectedResumeDisabledReason}
              resumeEventName={resumeEventName}
              nodeVisitNavigation={nodeVisitNavigation}
              showResumeAction={operationsEnabled}
              className={graphFullscreen ? "h-full min-h-0 overflow-auto" : undefined}
            />
          </main>
        </div>
      )}
    </div>
  );
}
