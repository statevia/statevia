import {
  apiGet,
  apiPost,
  type PaginationQuery,
  type SortQuery,
} from "@/shared/api";
import type {
  CommandAccepted,
  ExecutionDTO,
  ExecutionEventsResponse,
  ExecutionGraphDTO,
  PagedExecutions,
} from "./types";

export type { PaginationQuery, SortOrder, SortQuery } from "@/shared/api";

/** 実行一覧 API / 画面 URL のクエリパラメータ。 */
export type ExecutionsListQuery = {
  /** ページングの指定。 */
  pagination: PaginationQuery;
  /** 並び順の指定。 */
  sort: SortQuery;
  /** ステータスで絞り込む。未指定なら絞り込まない。 */
  status?: string;
  /** true なら要対応（待機中または `FAILED`）の実行だけ。未指定なら絞り込まない。 */
  needsAttention?: boolean;
  /** 定義名で絞り込む。未指定なら絞り込まない。 */
  name?: string;
  /** 定義で絞り込む。未指定なら絞り込まない。 */
  definitionId?: string;
  /** 指定時はその Project の実行だけ。未指定は所属の和。 */
  projectId?: string;
};

/**
 * 実行一覧用の相対パス `...?...` を組み立てる。空のフィルタは含めない。
 * API（`/api/core/...`）と App Router の `/executions` 双方で同じ形を使う。
 * @param params 一覧クエリ
 * @returns `/executions?...`
 */
export function buildExecutionsListPath(params: ExecutionsListQuery): string {
  const query = new URLSearchParams();
  query.set("limit", String(params.pagination.limit));
  query.set("offset", String(params.pagination.offset));
  if (params.status?.trim()) query.set("status", params.status.trim());
  if (params.needsAttention) query.set("needsAttention", "true");
  if (params.name?.trim()) query.set("name", params.name.trim());
  if (params.definitionId?.trim()) query.set("definitionId", params.definitionId.trim());
  if (params.projectId) query.set("projectId", params.projectId);
  if (params.sort.sortBy?.trim()) query.set("sortBy", params.sort.sortBy.trim());
  if (params.sort.sortOrder) query.set("sortOrder", params.sort.sortOrder);
  return `/executions?${query.toString()}`;
}

/**
 * 実行一覧を取得する。
 * @param query 一覧クエリ
 */
export async function listExecutions(query: ExecutionsListQuery): Promise<PagedExecutions> {
  return apiGet<PagedExecutions>(buildExecutionsListPath(query));
}

/** `GET /v1/executions/counts` の応答（実行の件数）。 */
export type ExecutionCounts = {
  /** 全件数。`byStatus` の和と等しい。 */
  total: number;
  /** 状態ごとの件数（契約の 5 値）。 */
  byStatus: Record<"ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED" | "UNKNOWN", number>;
  /** 待機中の件数。 */
  waiting: number;
  /** 要対応（待機中または `FAILED`）の件数。 */
  needsAttention: number;
};

/** 実行の件数の絞り込み。一覧と同じ意味で、状態・待機・要対応は受けない（それごとに数えて返すため）。 */
export type ExecutionCountsQuery = {
  /** 定義で絞り込む。 */
  definitionId?: string;
  /** 実行 ID の検索語で絞り込む。 */
  name?: string;
  /** その Project の実行だけ数える。 */
  projectId?: string;
  /** 開始日時の下限（含む。ISO 8601、タイムゾーン必須）。 */
  startedFrom?: string;
};

/**
 * 実行の件数を取得する（状態ごと・待機中・要対応）。
 * @remarks 一覧とは別の呼び出しのため、間に書き込みがあれば一覧の件数と一致しないことがある。
 * @param query 絞り込み。空の値は送らない。
 * @returns 件数。
 */
export async function getExecutionCounts(query: ExecutionCountsQuery): Promise<ExecutionCounts> {
  const params = new URLSearchParams();
  if (query.definitionId?.trim()) params.set("definitionId", query.definitionId.trim());
  if (query.name?.trim()) params.set("name", query.name.trim());
  if (query.projectId) params.set("projectId", query.projectId);
  if (query.startedFrom) params.set("startedFrom", query.startedFrom);
  const search = params.toString();
  return apiGet<ExecutionCounts>(search ? `/executions/counts?${search}` : "/executions/counts");
}

/**
 * 実行 1 件を取得する。
 * @param displayId 実行 displayId
 */
export async function getExecution(displayId: string): Promise<ExecutionDTO> {
  return apiGet<ExecutionDTO>(`/executions/${encodeURIComponent(displayId)}`);
}

/**
 * 実行グラフ（ノード・辺）を取得する。
 * @param displayId 実行 displayId
 */
export async function getExecutionGraph(displayId: string): Promise<ExecutionGraphDTO> {
  return apiGet<ExecutionGraphDTO>(`/executions/${encodeURIComponent(displayId)}/graph`);
}

/**
 * 実行のキャンセルを要求する。
 * @param displayId 実行 displayId
 */
export async function cancelExecution(displayId: string): Promise<CommandAccepted> {
  return apiPost<CommandAccepted>(`/executions/${encodeURIComponent(displayId)}/cancel`, {
    reason: "ui",
  });
}

/** テナント内の一意な購読候補。 */
export type EventSubscriptionCandidate = {
  /** 購読の topic。 */
  topic: string;
  /** 購読の key。 */
  key: string;
};

/** `GET /v1/event-subscriptions` の応答。 */
export type EventSubscriptionListResponse = {
  /** 購読候補の一覧。 */
  subscriptions: EventSubscriptionCandidate[];
};

/**
 * 現在テナントのアクティブ購読から、一意な topic / key を取る。
 * @returns 候補一覧。displayId と nodeId は含まない。
 */
export async function listEventSubscriptions(): Promise<EventSubscriptionListResponse> {
  return apiGet<EventSubscriptionListResponse>("/event-subscriptions");
}

/**
 * テナント内の一致購読へ集合配送する。ノード Resume には読み替えない。
 * @param input topic は必須。key の空白は省略する。payload は送らない。
 */
export async function publishTenantEvent(input: { topic: string; key?: string }): Promise<void> {
  const topic = input.topic.trim();
  const key = input.key?.trim() ?? "";
  const body: { topic: string; key?: string } = { topic };
  if (key.length > 0) {
    body.key = key;
  }
  await apiPost<unknown>("/events", body);
}

/**
 * 実行へイベントを送信する。
 * @param displayId 実行 displayId
 * @param eventName イベント名
 */
export async function publishExecutionEvent(
  displayId: string,
  eventName: string
): Promise<CommandAccepted> {
  return apiPost<CommandAccepted>(`/executions/${encodeURIComponent(displayId)}/events`, {
    name: eventName,
  });
}

/**
 * Wait ノードの Resume を要求する。
 * @param displayId 実行 displayId
 * @param nodeId ノード ID
 * @param resumeKey 再開イベント名（必須）
 */
export async function resumeExecutionNode(
  displayId: string,
  nodeId: string,
  resumeKey: string
): Promise<CommandAccepted> {
  return apiPost<CommandAccepted>(
    `/executions/${encodeURIComponent(displayId)}/nodes/${encodeURIComponent(nodeId)}/resume`,
    { resumeKey }
  );
}

/**
 * 実行イベント履歴を取得する。
 * @param displayId 実行 displayId
 * @param query 追加クエリ（fromSeq 等）
 */
export async function listExecutionEvents(
  displayId: string,
  query?: string
): Promise<ExecutionEventsResponse> {
  const suffix = query ? `?${query}` : "";
  return apiGet<ExecutionEventsResponse>(
    `/executions/${encodeURIComponent(displayId)}/events${suffix}`
  );
}

/**
 * 定義グラフを取得する（実行可視化の定義レイヤ）。
 * @param graphId グラフ ID
 */
export async function getGraphDefinitionRaw(graphId: string): Promise<unknown> {
  return apiGet<unknown>(`/graphs/${encodeURIComponent(graphId)}`);
}
