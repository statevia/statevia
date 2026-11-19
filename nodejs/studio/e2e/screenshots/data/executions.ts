import type { ExecutionCounts } from "@/features/executions/api";
import type {
  ExecutionDTO,
  ExecutionEventsResponse,
  ExecutionGraphDTO,
  ExecutionGraphNodeDTO,
  ExecutionStatus,
  PagedExecutions
} from "@/features/executions/types";
import { ORDER_DEFINITION_ID, screenshotDefinitions } from "./definitions";

/**
 * 主役の実行（受注の並列処理。在庫移動のイベントを待っている）の表示用 ID。
 * @remarks 実行詳細のグラフ表示とイベント送信の画面で使う。定義一覧の最後の実行と同じ ID。
 */
export const WAITING_EXECUTION_ID = "Wd3pL7sKe9";

const workerId = "6d1f0c2a-4b7e-4c55-9a10-3e8b2f7d9c41";
const orderInput = { eligible: true, shared: { orderId: "ORD-1001" } };

/** `GET /v1/executions/{id}` の応答（ACTIVE で待機中）。`graphId` は定義の表示用 ID。 */
export const screenshotWaitingExecution: ExecutionDTO = {
  displayId: WAITING_EXECUTION_ID,
  resourceId: "00000000-0000-4000-8000-000000000801",
  graphId: ORDER_DEFINITION_ID,
  status: "ACTIVE",
  startedAt: "2026-11-12T01:12:00Z",
  updatedAt: "2026-11-12T01:12:02Z",
  cancelRequested: false,
  restartLost: false,
  waiting: true
};

/**
 * 完了したノードを作る。形はローカルの Service API の応答（2026-11-19 に取得）に合わせた。
 * @param nodeId ノード ID。
 * @param nodeName 状態名。
 * @param nodeType ノードの種類。
 * @param startedAt 開始日時。
 * @param resolution 条件分岐の解決（`linear` か `matched_case`）。
 * @returns 完了したノード。
 */
function completedNode(
  nodeId: string,
  nodeName: string,
  nodeType: string,
  startedAt: string,
  resolution: "linear" | "matched_case" = "linear"
): ExecutionGraphNodeDTO {
  return {
    nodeId,
    nodeName,
    nodeType,
    startedAt,
    completedAt: startedAt.replace("Z", "") + "5Z",
    fact: "Completed",
    input: orderInput,
    output: orderInput,
    attempt: 1,
    workerId,
    waitKey: null,
    allowedEvents: null,
    canceledByExecution: false,
    conditionRouting:
      resolution === "matched_case"
        ? {
            fact: "Completed",
            resolution,
            matchedCaseIndex: 0,
            caseEvaluations: [{ caseIndex: 0, declarationIndex: 0, order: 10, matched: true, reasonCode: null, reasonDetail: null }],
            evaluationErrors: []
          }
        : { fact: "Completed", resolution, matchedCaseIndex: null, caseEvaluations: [], evaluationErrors: [] }
  };
}

/** 待機中のノードの ID（イベント送信の届け先）。 */
export const WAITING_NODE_ID = "3f9a1c7e5b20";

/**
 * `GET /v1/executions/{id}/graph` の応答。
 * @remarks 受付・検証・分岐が終わり、東倉からの出荷は完了、在庫移動（`StockTransferred`）を待っている。
 */
export const screenshotWaitingExecutionGraph: ExecutionGraphDTO = {
  nodes: [
    completedNode("a1c40e9b7d01", "order.start", "Start", "2026-11-12T01:12:00.10Z"),
    completedNode("b2d51fa08e02", "order.preflight", "Task", "2026-11-12T01:12:00.20Z", "matched_case"),
    completedNode("c3e62ab19f03", "order.validate", "Task", "2026-11-12T01:12:00.30Z"),
    completedNode("d4f73bc2a004", "order.split", "Fork", "2026-11-12T01:12:00.40Z"),
    {
      nodeId: WAITING_NODE_ID,
      nodeName: "order.await.transfer",
      nodeType: "Wait",
      startedAt: "2026-11-12T01:12:01.40Z",
      completedAt: null,
      fact: null,
      input: orderInput,
      attempt: 1,
      workerId,
      waitKey: "StockTransferred",
      allowedEvents: ["StockTransferred"],
      canceledByExecution: false,
      conditionRouting: null
    },
    { ...completedNode("e5a84cd3b105", "order.ship.east", "Task", "2026-11-12T01:12:01.40Z"), completedAt: "2026-11-12T01:12:02Z" }
  ],
  edges: [
    { from: "a1c40e9b7d01", to: "b2d51fa08e02", type: 0 },
    { from: "b2d51fa08e02", to: "c3e62ab19f03", type: 0 },
    { from: "c3e62ab19f03", to: "d4f73bc2a004", type: 0 },
    { from: "d4f73bc2a004", to: WAITING_NODE_ID, type: 1 },
    { from: "d4f73bc2a004", to: "e5a84cd3b105", type: 1 }
  ]
};

const definitionEdges: readonly [string, string, string?][] = [
  ["order.start", "order.preflight"],
  ["order.preflight", "order.validate"],
  ["order.preflight", "order.reject.notify"],
  ["order.validate", "order.split"],
  ["order.validate", "order.reject.notify"],
  ["order.reject.notify", "order.end"],
  ["order.split", "order.ship.east"],
  ["order.split", "order.await.transfer"],
  ["order.ship.east", "order.merge"],
  ["order.await.transfer", "order.merge", "StockTransferred"],
  ["order.merge", "order.notify.customer"],
  ["order.notify.customer", "order.end"]
];

/**
 * `GET /v1/graphs/{graphId}` の応答（定義のグラフ）。
 * @remarks 実 API は同じ辺を重ねて返すが、画面は重なりを 1 本に描くので重なりを除いた。`label` は Wait の辺だけイベント名。
 */
export const screenshotOrderGraphDefinition = {
  graphId: ORDER_DEFINITION_ID,
  nodes: [
    ["order.start", "Start"],
    ["order.preflight", "Task"],
    ["order.validate", "Task"],
    ["order.reject.notify", "Task"],
    ["order.split", "Fork"],
    ["order.ship.east", "Task"],
    ["order.await.transfer", "Wait"],
    ["order.merge", "Join"],
    ["order.notify.customer", "Task"],
    ["order.end", "End"]
  ].map(([nodeName, nodeType]) => ({ nodeName, nodeType, label: nodeName })),
  edges: definitionEdges.map(([from, to, label]) => ({ from, to, label: label ?? "" }))
};

/** `GET /v1/executions/{id}/events` の応答（実行の開始だけ。実 API と同じく、ノードの進行はグラフで返る）。 */
export const screenshotWaitingExecutionEvents: ExecutionEventsResponse = {
  events: [{ seq: 1, type: "ExecutionStatusChanged", executionId: WAITING_EXECUTION_ID, to: "ACTIVE", at: "2026-11-12T01:12:00.05Z" }],
  hasMore: false
};

/** 一覧に並べる実行（表示用 ID・定義・状態・開始・更新・待機中か）。開始の新しい順。 */
const executionRows: readonly [string, number, ExecutionStatus, string, string, boolean][] = [
  [WAITING_EXECUTION_ID, 0, "ACTIVE", "2026-11-12T01:12:00Z", "2026-11-12T01:12:02Z", true],
  ["Ys1cH6mQz3", 3, "COMPLETED", "2026-11-12T01:00:00Z", "2026-11-12T01:00:04Z", false],
  ["Pz8rF2hJm4", 1, "COMPLETED", "2026-11-12T00:40:00Z", "2026-11-12T00:41:10Z", false],
  ["Ab4nT7yWq2", 0, "ACTIVE", "2026-11-12T00:31:00Z", "2026-11-12T00:31:03Z", true],
  ["Lx6gV2pRc8", 0, "COMPLETED", "2026-11-11T23:50:00Z", "2026-11-11T23:52:40Z", false],
  ["Ue3jK8bNf5", 1, "CANCELED", "2026-11-11T22:05:00Z", "2026-11-11T22:30:00Z", false],
  ["Tg5kB3nVd8", 2, "FAILED", "2026-11-11T15:00:00Z", "2026-11-11T15:02:30Z", false],
  ["Ro7sM1dHx4", 0, "COMPLETED", "2026-11-11T09:20:00Z", "2026-11-11T09:24:15Z", false],
  ["Gi2wE9cZk6", 3, "COMPLETED", "2026-11-11T01:00:00Z", "2026-11-11T01:00:05Z", false],
  ["Nv8hQ4tLa1", 1, "COMPLETED", "2026-11-10T06:10:00Z", "2026-11-10T06:11:20Z", false]
];

/**
 * 一覧の実行。`graphId` は実 API の一覧と同じく定義の resourceId。
 * @remarks 実行詳細の応答（`graphId` が表示用 ID）とは形が違うが、実 API に合わせた。
 */
export const screenshotExecutions: ExecutionDTO[] = executionRows.map(
  ([displayId, definitionIndex, status, startedAt, updatedAt, waiting], index) => ({
    displayId,
    resourceId: `00000000-0000-4000-8000-0000000008${String(index + 1).padStart(2, "0")}`,
    graphId: screenshotDefinitions[definitionIndex].resourceId,
    status,
    startedAt,
    updatedAt,
    cancelRequested: false,
    restartLost: false,
    waiting
  })
);

/**
 * `GET /v1/executions` の応答。
 * @param query 要求のクエリ（`limit` と `needsAttention` を見る）。
 * @returns 1 ページ目。要対応（待機中か FAILED）で絞るときはそれだけ返す。
 */
export function screenshotExecutionsPage(query: URLSearchParams): PagedExecutions {
  const needsAttention = query.get("needsAttention") === "true";
  const limit = Number(query.get("limit") ?? "20");
  const matched = screenshotExecutions.filter((execution) => !needsAttention || execution.waiting || execution.status === "FAILED");
  return { items: matched.slice(0, limit), totalCount: matched.length, offset: 0, limit, hasMore: matched.length > limit };
}

/**
 * `GET /v1/executions/counts` の応答。一覧の実行から数える。
 * @param query 要求のクエリ（`startedFrom` があれば、その日時以降に開始した実行だけ数える）。
 * @returns 状態ごとの件数。
 */
export function screenshotExecutionCounts(query: URLSearchParams): ExecutionCounts {
  const startedFrom = query.get("startedFrom");
  const from = startedFrom === null ? null : Date.parse(startedFrom);
  const counted = screenshotExecutions.filter((execution) => from === null || Date.parse(execution.startedAt) >= from);
  const countBy = (status: ExecutionStatus) => counted.filter((execution) => execution.status === status).length;
  return {
    total: counted.length,
    byStatus: { ACTIVE: countBy("ACTIVE"), COMPLETED: countBy("COMPLETED"), FAILED: countBy("FAILED"), CANCELED: countBy("CANCELED"), UNKNOWN: 0 },
    waiting: counted.filter((execution) => execution.waiting).length,
    needsAttention: counted.filter((execution) => execution.waiting || execution.status === "FAILED").length
  };
}
