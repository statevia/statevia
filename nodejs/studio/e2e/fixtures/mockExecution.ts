import type {
  ExecutionDTO,
  ExecutionGraphDTO,
  ExecutionGraphNodeDTO,
  ExecutionView
} from "@/features/executions/types";

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
