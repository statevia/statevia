import type { ExecutionsFeatureUiText } from "./types";
import { executionsUiTextJa } from "./ja";

/**
 * executions feature の英語辞書切片（未翻訳は ja を継承）。
 */
export const executionsUiTextEn: ExecutionsFeatureUiText = {
  ...executionsUiTextJa,
  executionDashboard: {
    ...executionsUiTextJa.executionDashboard,
    operationsPermissionDenied: "Canceling, resuming, and sending events require the \"Write executions\" permission on one of your groups. Ask a tenant administrator.",
    header: {
      titleDefault: "Execution detail",
    },
    actions: {
      sectionTitle: "Execution actions",
      eventNameLabel: "Event name",
      eventNamePlaceholder: "event-name",
    },
    validation: {
      eventNameTooLong: "Event name must be within 64 characters.",
      eventNameInvalidFormat: "Event name must start with an ASCII letter and use only ASCII alphanumerics plus . - _.",
    },
    graph: {
      fullscreenEnter: "Fullscreen",
      fullscreenExit: "Exit fullscreen (Esc)",
      definitionMissingFallback: "The definition for this execution was not found, so transitions are drawn as temporary lines.",
    },
    errors: {
      executionNotFound: "The specified execution was not found. Please check the ID.",
    },
    toasts: {
      cancelAccepted: "Cancellation requested.",
      publishAccepted: "Event sent.",
      resumeAccepted: "Resume requested.",
    },
    replayDisabledReason: "Actions are disabled while replaying.",
    operationsAggregatedInRun: (cancelLabel: string, resumeLabel: string, sendEventLabel: string) =>
      `${cancelLabel} / ${resumeLabel} / ${sendEventLabel} are on the Operations screen.`,

  },
  executionTimeline: {
    ...executionsUiTextJa.executionTimeline,
    title: "Event timeline",
    backToCurrent: "Back to current",
    replayingPastStateMessage: 'Showing a past state. Click "Back to current" to return to the latest state.',
    empty: "No events",
    loadMore: "Load more",
    errorUnknown: "An unknown error occurred.",

  },
  executionComparison: {
    ...executionsUiTextJa.executionComparison,
    title: "Compare two executions",
    executionIdPlaceholder: "e.g. a1B2c3D4e5",
    executionABaselineLabel: (executionLabel: string) => `${executionLabel} A (baseline)`,
    executionBLabel: (executionLabel: string) => `${executionLabel} B`,
    kind: {
      onlyLeft: "A only",
      onlyRight: "B only",
      diff: "Diff",
    },
    state: {
      notLoaded: "Not loaded",
    },
    summary: {
      title: "Diff summary",
      failedOrCancelled: "Failed / Cancelled",
      others: "Others",
      noDiff: "No node differences",
      loadBothToShow: "Load A and B to show differences.",
    },

  },
  nodeDetail: {
    ...executionsUiTextJa.nodeDetail,
    prompts: {
      loadExecution: (executionLabel: string) => `Please load ${executionLabel}.`,
      selectNode: (nodeLabel: string) => `Please select ${nodeLabel}.`,
    },
    title: (nodeLabel: string) => `${nodeLabel} detail`,
    meta: {
      type: (nodeType: string) => `Type: ${nodeType}`,
      nodeName: (nodeName: string) => `Node name: ${nodeName}`,
      nodeId: (id: string) => `Node ID: ${id}`,
      attempt: (attempt: number) => `Pass: ${attempt}`,
      waitKey: (waitKey: string) => `Wait key: ${waitKey}`,
      allowedEvents: (events: string) => `Allowed events: ${events}`,
    },
    waiting: {
      title: "Wait details",
      reasonWaitByWaitKeyAndResumeWait: "Reason: waiting for a resume event on the wait key.",
      resumeEventName: (eventName: string) => `Resume event name: ${eventName}`,
      selectResumeEvent: "Event to resume with",
      selectInternalResumeEvent: "Event name",
      subscribeOnlyHint: "This resumes only this execution, using an event name.\nIt is not delivered to every execution waiting on the same topic / key.",
      subscribeResumeAction: "Resume this execution only",
    },
    cancel: {
      detailTitle: (cancelLabel: string) => `${cancelLabel} details`,
      reason: (cancelReason: string) => `Reason: ${cancelReason}`,
      convergedByExecutionCancel: "Stopped because the execution was canceled.",
    },
    failure: {
      title: "Failure",
      noMessage: "(No message)",
    },
    trace: {
      startedAt: (formattedInstant: string) => `Started: ${formattedInstant}`,
      completedAt: (formattedInstant: string) => `Completed: ${formattedInstant}`,
      duration: (durationText: string) => `Duration: ${durationText}`,
      durationUnavailable: "Duration: —",
      outputHeading: "Output",
      outputEmpty: "(None)",
      inputHeading: "Input",
      inputEmpty: "(None)",
      conditionRoutingHeading: "Condition result",
      conditionRoutingEmpty: "(None)",
    },
    visitNavigation: {
      jumpToLatest: "Go to the latest pass",
      stepNewer: "Go one pass newer",
      stepOlder: "Go one pass older",
      jumpToFirst: "Go to the first pass",
    },

  },
  graphLegend: {
    ...executionsUiTextJa.graphLegend,
    heading: {
      nodeStatus: "Node status",
      edgeType: "Transition type",
    },
    aria: {
      root: "Graph legend",
      nodeStatus: "Node status legend",
      edgeType: "Transition type legend",
    },
    edgeKind: {
      nextTraversed: "Taken",
      nextNotTraversed: "Not taken",
    },

  },
  executionHeader: {
    ...executionsUiTextJa.executionHeader,
    placeholderExecutionId: "e.g. a1B2c3D4e5",
    executionIdLabel: (executionLabel: string) => `${executionLabel} ID`,
    compareLabel: "Compare",
    realtimeSseLabel: "Realtime updates",
    cancelRequestedLabel: "Cancel requested",
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) =>
      `${cancelRequestedLabel}: ${cancelRequested ? "Yes" : "No"}`,

  },
  executionsPage: {
    ...executionsUiTextJa.executionsPage,
    loading: "Loading executions.",
    listSummary: (totalCount: number, page: number) => `Total ${totalCount} (page ${page})`,
    updatedAt: (formattedDateTime: string) => `Updated: ${formattedDateTime}`,
    empty: "No executions found.",
    error: "Failed to load executions. Please try again later.",
    pagination: {
      ...executionsUiTextJa.executionsPage.pagination,
      ariaLabel: "Executions pagination",
      currentPage: (page: number) => `Page ${page}`,
      prev: "Prev",
      next: "Next",
    },
    filter: {
      ...executionsUiTextJa.executionsPage.filter,
      contextActivePrefix: "Filtered by definition:",
      clearDefinition: "Clear definition filter",
      title: "Filters",
      all: "(All)",
      definitionInputHint: "Definition ID or UUID",
      definitionLabelWithHint: (definitionLabel: string) => `${definitionLabel} (ID or UUID)`,
      definitionPlaceholder: "e.g. a1B2c3D4e5",
      nameInputHint: "Execution ID (partial match) or UUID (exact match)",
      search: "Search",
      clear: "Clear",
      sortByLabel: "Sort by",
      sortOrderLabel: "Order",
      sortByUpdatedAt: "Updated at",
      sortByDisplayId: "ID",
      sortOrderDesc: "Descending",
      sortOrderAsc: "Ascending",
      invalidName: "Execution ID allows only ASCII alphanumerics plus . - _ within 100 characters.",
      invalidDefinitionId: "Definition ID allows only ASCII alphanumerics plus - _ within 80 characters.",
      pageInfo: (limit: number) => `Showing up to ${limit} items per page.`,
    },
    actions: {
      openDetail: "Details",
    },
    ingress: {
      open: "Event submission",
      title: "Event submission",
      topicLabel: "Topic",
      keyLabel: "Key",
      emptyKey: "No key",
      fanOutHint: "Every execution waiting on the same topic / key may resume.\nThis is not limited to one row in the list.",
      submit: "Send",
      cancel: "Close",
      candidatesUnavailable: "Candidates could not be loaded. You can still type a topic / key.",
      accepted: "Accepted. This succeeds even when no subscription matches.",
      invalidTopic: "Topic must start with a letter and use only ASCII letters, digits, and . _ - within 256 characters.",
      invalidKey: "A non-empty key must start with a letter and use only ASCII letters, digits, and . _ - within 256 characters.",
    },

  },
  executionDetailPage: {
    ...executionsUiTextJa.executionDetailPage,
    title: "Execution detail",
    missingExecutionId: "Execution ID is missing.",
    navRun: "Operations",
    navGraph: "Graph",

  },
  executionGraphPage: {
    ...executionsUiTextJa.executionGraphPage,
    title: "Execution graph",
    missingExecutionId: "Execution ID is missing.",
    navDetail: "Detail",
    navRun: "Operations",

  },
  executionRunPage: {
    ...executionsUiTextJa.executionRunPage,
    title: "Execution operations",
    missingExecutionId: "Execution ID is missing.",
    navDetail: "Detail",
    navGraph: "Graph",

  },
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) =>
      `${cancelLabel} has been requested, so actions such as ${resumeLabel} are unavailable.`,
    terminalNotice: (executionLabel: string) => `${executionLabel} is already finished.`,

  },
  nodeList: {
    ...executionsUiTextJa.nodeList,
    title: "Node list",
    summary: (stateCount: number, visitCount: number) =>
      stateCount === visitCount ? `${stateCount} items` : `${stateCount} nodes / ${visitCount} passes`,
    visitCount: (count: number) => `${count} passes`,
    visitLabel: (nodeName: string, attempt: number) => `${nodeName} (pass ${attempt})`,
    expandVisits: (nodeName: string) => `Expand passes of ${nodeName}`,
    collapseVisits: (nodeName: string) => `Collapse passes of ${nodeName}`,
    columns: {
      status: "Status",
      type: "Type",
      nodeName: "Node name",
      nodeId: "Node ID",
      duration: "Duration",
      expand: "Expand",
    },
    pagination: {
      ariaLabel: "Node list pages",
      currentPage: (page: number, pageCount: number) => `${page} / ${pageCount}`,
    },

  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => `Type: ${nodeType}`,
      attempt: (attempt: number) => `Pass: ${attempt}`,
      waitKey: (waitKey: string) => `Wait key: ${waitKey}`,
    },
    aria: {
      selectNode: (displayLabel: string) => `Select node: ${displayLabel}`,
    },

  },
  nodeCommands: {
    resumeDisabledReason: {
      runOnly: "Resume is available only on the Operations screen.",
      permissionDenied: "Resuming requires the \"Write executions\" permission on one of your groups.",
      executionNotLoaded: "The execution is not loaded.",
      nodeNotSelected: "Please select a node.",
      executionTerminal: "The execution has already finished.",
      cancelRequested: "Cancellation was requested, so actions such as resuming are unavailable.",
      waitingOnly: "Only waiting nodes can be resumed.",
      noResumeEvent: "Resume is unavailable because the node accepts no events.",
    },

  },
};
