import type {
  ExecutionDTO,
  ExecutionGraphDTO,
  ExecutionGraphNodeDTO,
  ExecutionView,
  PagedExecutions
} from "@/features/executions/types";
import type { ExecutionCounts } from "@/features/executions/api";
import type { ProjectSummary } from "@/shared/project/types";

/** モック E2E の実行 ID。 */
export const MOCK_EXECUTION_ID = "ex-1";

/** モック E2E の定義の表示用 ID（`graphId`）。 */
export const MOCK_GRAPH_ID = "hello";

/** `GET /v1/executions/ex-1` の応答（ACTIVE）。 */
export const mockExecutionDto: ExecutionDTO = {
  displayId: MOCK_EXECUTION_ID,
  resourceId: "00000000-0000-0000-0000-000000000001",
  graphId: MOCK_GRAPH_ID,
  status: "ACTIVE",
  startedAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:01Z",
  cancelRequested: false,
  restartLost: false,
  waiting: false
};

const startNode: ExecutionGraphNodeDTO = {
  nodeId: "start",
  nodeName: "Start",
  nodeType: "Start",
  startedAt: "2026-01-01T00:00:00Z",
  completedAt: "2026-01-01T00:00:00.5Z",
  fact: "Completed",
  attempt: 1
};

const taskNode: ExecutionGraphNodeDTO = {
  nodeId: "task-a",
  nodeName: "TaskA",
  nodeType: "Task",
  startedAt: "2026-01-01T00:00:01Z",
  completedAt: null,
  fact: null,
  attempt: 1,
  workerId: "w1"
};

/** `GET /v1/executions/ex-1/graph` の応答（Start が完了し、TaskA が実行中）。 */
export const mockExecutionGraph: ExecutionGraphDTO = {
  nodes: [startNode, taskNode],
  edges: [{ from: "start", to: "task-a" }]
};

/**
 * 待機中の Wait ノードを作る。
 * @param nodeId ノード ID。
 * @param nodeName 状態名。
 * @param eventName 待っているイベント名。
 * @returns 未完了の Wait ノード（画面では WAITING）。
 */
export function waitingNode(nodeId: string, nodeName: string, eventName: string): ExecutionGraphNodeDTO {
  return {
    nodeId,
    nodeName,
    nodeType: "Wait",
    startedAt: "2026-01-01T00:00:01Z",
    completedAt: null,
    fact: null,
    attempt: 1,
    waitKey: eventName,
    allowedEvents: [eventName]
  };
}

/** `GET /v1/executions/ex-1/graph` の応答（Start が完了し、WaitApproval が Approved を待つ）。 */
export const mockSingleWaitGraph: ExecutionGraphDTO = {
  nodes: [startNode, waitingNode("wait-approval", "WaitApproval", "Approved")],
  edges: [{ from: "start", to: "wait-approval" }]
};

/** タイムラインのリプレイの既定（seq が当てはまらないとき）に返す最新の状態。 */
export const mockExecution: ExecutionView = {
  ...mockExecutionDto,
  nodes: [
    { nodeId: "start", nodeName: "Start", nodeType: "Start", status: "SUCCEEDED", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-a", nodeName: "TaskA", nodeType: "Task", status: "RUNNING", attempt: 1, workerId: "w1", waitKey: null, canceledByExecution: false },
  ],
};

/** 実行一覧で、既定の実行（ACTIVE）と並べる完了済みの実行。 */
export const mockCompletedExecutionDto: ExecutionDTO = {
  ...mockExecutionDto,
  displayId: "ex-2",
  resourceId: "00000000-0000-0000-0000-000000000002",
  status: "COMPLETED",
  startedAt: "2025-12-31T00:00:00Z",
  updatedAt: "2025-12-31T00:00:05Z"
};

/** `GET /v1/projects` の応答（所属 Project が 1 件）。実行一覧はこの Project を選んで一覧を取得する。 */
export const mockProjects: ProjectSummary[] = [
  {
    projectId: "00000000-0000-0000-0000-0000000000a1",
    slug: "default",
    displayName: "Default",
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role: "executor"
  }
];

/** `GET /v1/executions` の応答（ACTIVE の ex-1 と COMPLETED の ex-2）。 */
export const mockExecutionsPage: PagedExecutions = {
  items: [mockExecutionDto, mockCompletedExecutionDto],
  totalCount: 2,
  offset: 0,
  limit: 20,
  hasMore: false
};

/** `GET /v1/executions/counts` の応答（`mockExecutionsPage` と合う件数）。 */
export const mockExecutionCounts: ExecutionCounts = {
  total: 2,
  byStatus: { ACTIVE: 1, COMPLETED: 1, FAILED: 0, CANCELED: 0, UNKNOWN: 0 },
  waiting: 0,
  needsAttention: 0
};
