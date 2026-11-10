import type { ExecutionStatus, NodeStatus } from "@/shared/lib/statusStyle";

/** v2: C# API の実行状態（ExecutionStatus）とノード状態。 */
export type { ExecutionStatus, NodeStatus } from "@/shared/lib/statusStyle";

/** v2: GET /v1/executions/:id のレスポンス（C# ExecutionResponse）。 */
export type ExecutionDTO = {
  /** 実行の表示用 ID。 */
  displayId: string;
  /** 実行のリソース UUID。 */
  resourceId: string;
  /** グラフ ID（定義の表示用 ID）。 */
  graphId: string;
  /** 実行のステータス。 */
  status: ExecutionStatus;
  /** 開始日時（ISO 8601）。 */
  startedAt: string;
  /** 最終更新日時（ISO 8601）。無ければ null または省略。 */
  updatedAt?: string | null;
  /** キャンセルを要求されたか。 */
  cancelRequested: boolean;
  /** 再起動で失われたか。 */
  restartLost: boolean;
};

/**
 * v2: GET /v1/executions/:id/graph のノード（C# ExecutionNode）。JSON は camelCase（Service API）。
 * ノード ID のキーは API／Engine／永続グラフ／View／UI とも `nodeId`。
 */
export type ExecutionGraphNodeDTO = {
  /** ノード ID。 */
  nodeId?: string;
  /** 状態名。 */
  nodeName?: string;
  /** ノードの種別。 */
  nodeType?: string;
  /** 開始日時（ISO 8601）。 */
  startedAt?: string;
  /** 完了日時（ISO 8601）。未完了なら null。 */
  completedAt?: string | null;
  /** 状態が返した Fact。未完了なら null。 */
  fact?: string | null;
  /** ノードの入力。 */
  input?: unknown;
  /** ノードの出力。 */
  output?: unknown;
  /** 試行回数。 */
  attempt?: number;
  /** 処理した Worker の ID。無ければ null。 */
  workerId?: string | null;
  /** Wait の待機キー。無ければ null。 */
  waitKey?: string | null;
  /** Wait が受付可能なイベント名一覧（複数イベント時）。 */
  allowedEvents?: string[] | null;
  /** 実行のキャンセルで止まったノードか。 */
  canceledByExecution?: boolean;
  /** 条件遷移の診断。 */
  conditionRouting?: unknown;
};

/** v2: GET /v1/executions/:id/graph の辺（C# ExecutionEdge）。 */
export type ExecutionGraphEdgeDTO = {
  /** 始点のノード ID。 */
  from?: string;
  /** 終点のノード ID。 */
  to?: string;
  /** 辺の種類（Engine の EdgeType の序数。0 = Next、1 = Fork、2 = Join、3 = Resume、4 = Cancel）。 */
  type?: number;
};

/** ランタイムグラフの辺（from / to / type）。 */
export type RuntimeGraphEdgeDTO = {
  /** 始点のノード ID。 */
  from: string;
  /** 終点のノード ID。 */
  to: string;
  /** 辺の種類（Engine の EdgeType の序数。0 = Next、1 = Fork、2 = Join、3 = Resume、4 = Cancel）。 */
  type?: number;
};

/** v2: GET /v1/executions/:id/graph のレスポンス（C# ExecutionGraphResponse）。 */
export type ExecutionGraphDTO = {
  /** 実行グラフのノード。 */
  nodes: ExecutionGraphNodeDTO[];
  /** 実行グラフの辺。 */
  edges: ExecutionGraphEdgeDTO[];
};

/** グラフ可視化用のノード（状態実行）。v2 では ExecutionGraphDTO から変換。 */
export type ExecutionNodeDTO = {
  /** 実行ノード短名 UUID（API／Engine／View の `nodeId` と同値）。定義キャンバスの `name` / Graph Def の `nodeName` とは別。 */
  nodeId: string;
  /** 状態名。 */
  nodeName?: string;
  /** ノードの種別。 */
  nodeType: string;
  /** ノードのステータス。 */
  status: NodeStatus;
  /** 試行回数。 */
  attempt: number;
  /** 処理した Worker の ID。無ければ null。 */
  workerId: string | null;
  /** Wait の待機キー。無ければ null。 */
  waitKey: string | null;
  /** Wait が受付可能なイベント名一覧（任意。複数イベント時）。 */
  allowedEvents?: string[] | null;
  /** 実行のキャンセルで止まったノードか。 */
  canceledByExecution: boolean;
  /** GET /graph のノードに含まれる場合のみ（ノード詳細のトレース用）。 */
  startedAt?: string;
  /** 完了日時（ISO 8601）。未完了なら null。 */
  completedAt?: string | null;
  /** ノードの入力。 */
  input?: unknown;
  /** ノードの出力。 */
  output?: unknown;
  /** 条件遷移の診断。 */
  conditionRouting?: unknown;
  /** 失敗の情報。無ければ null。 */
  error?: { message?: string } | null;
  /** キャンセルの理由。無ければ null。 */
  cancelReason?: string | null;
};

/** v2: 実行 + グラフから組み立てたビュー。一覧・詳細・グラフで利用。 */
export type ExecutionView = ExecutionDTO & {
  graphId: string;
  nodes: ExecutionNodeDTO[];
  runtimeEdges?: RuntimeGraphEdgeDTO[];
};

/** コマンド受理レスポンス（Service API）。 */
export type CommandAccepted = {
  /** 実行の表示用 ID。 */
  executionId: string; // v2 では displayId を格納
  /** 受理したコマンドの名前。 */
  command: string;
  /** 受理したか（常に true）。 */
  accepted: true;
  /** 相関 ID。無ければ null。 */
  correlationId?: string | null;
  /** 冪等キー。 */
  idempotencyKey: string;
};

/** GraphUpdated 等のパッチ。`nodeId` は UI／REST 向け名（永続グラフ JSON の `nodeId` と同値）。 */
export type GraphPatchNode = {
  /** ノード ID。 */
  nodeId: string;
  /** 状態名。 */
  nodeName?: string;
  /** ノードの種別。 */
  nodeType?: string;
  /** ノードのステータス。 */
  status?: NodeStatus;
  /** 試行回数。 */
  attempt?: number;
  /** 処理した Worker の ID。無ければ null。 */
  workerId?: string | null;
  /** Wait の待機キー。無ければ null。 */
  waitKey?: string | null;
  /** Wait が受付可能なイベント名一覧（任意）。 */
  allowedEvents?: string[] | null;
  /** 実行のキャンセルで止まったノードか。 */
  canceledByExecution?: boolean;
  /** 失敗の情報。無ければ null。 */
  error?: { message?: string } | null;
  /** キャンセルの理由。無ければ null。 */
  cancelReason?: string | null;
};

/** SSE: グラフノードが更新されたイベント。 */
export type GraphUpdatedEvent = {
  /** イベントの種別。 */
  type: "GraphUpdated";
  /** 実行の表示用 ID。 */
  executionId: string;
  /** 更新されたノード。 */
  patch: {
    nodes?: GraphPatchNode[];
  };
  /** 発生日時（ISO 8601）。 */
  at?: string;
};

/** SSE: 実行状態が変化したイベント。 */
export type ExecutionStatusChangedEvent = {
  /** イベントの種別。 */
  type: "ExecutionStatusChanged";
  /** 実行の表示用 ID。 */
  executionId: string;
  /** 変化する前のステータス。 */
  from?: string;
  /** 変化した後のステータス。 */
  to: string;
  /** 変化の理由。 */
  reason?: string;
  /** 発生日時（ISO 8601）。 */
  at?: string;
};

/** SSE: ノードがキャンセルされたイベント。 */
export type NodeCancelledEvent = {
  /** イベントの種別。 */
  type: "NodeCancelled";
  /** 実行の表示用 ID。 */
  executionId: string;
  /** キャンセルされたノードの ID。 */
  nodeId: string;
  /** キャンセルの理由と原因。 */
  cancel?: {
    reason?: string;
    cause?: {
      message?: string;
      at?: string;
    };
  };
  /** 発生日時（ISO 8601）。 */
  at?: string;
};

/** SSE: ノードが失敗したイベント。 */
export type NodeFailedEvent = {
  /** イベントの種別。 */
  type: "NodeFailed";
  /** 実行の表示用 ID。 */
  executionId: string;
  /** 失敗したノードの ID。 */
  nodeId: string;
  /** 失敗の情報。 */
  error?: {
    message?: string;
    at?: string;
  };
  /** 発生日時（ISO 8601）。 */
  at?: string;
};

/** 実行ストリームのイベント判別共用体。 */
export type ExecutionStreamEvent =
  | GraphUpdatedEvent
  | ExecutionStatusChangedEvent
  | NodeCancelledEvent
  | NodeFailedEvent;

/** シーケンス番号付き実行イベント。 */
export type ExecutionEventWithSeq = { seq: number } & ExecutionStreamEvent;

/** 実行イベント一覧 API のレスポンス。 */
export type ExecutionEventsResponse = {
  /** 実行イベント（seq の昇順）。 */
  events: ExecutionEventWithSeq[];
  /** 続きがあるか。 */
  hasMore?: boolean;
};

/** GET /v1/executions?limit=&offset= のページング結果（Service API `PagedResult<T>`）。 */
export type PagedResult<T> = {
  /** ページ内の項目。 */
  items: T[];
  /** 絞り込み後の総件数。 */
  totalCount: number;
  /** ページの先頭の位置（0 始まり）。 */
  offset: number;
  /** 1 ページの件数。 */
  limit: number;
  /** 次のページがあるか。 */
  hasMore: boolean;
};

/** ページング付き実行一覧。 */
export type PagedExecutions = PagedResult<ExecutionDTO>;
