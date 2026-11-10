import type { ExecutionStatus } from "@/shared/lib/statusStyle";

/**
 * ダッシュボードが一覧表示する実行（GET /executions の要素のうち利用分）。
 * 正本の実行 DTO は `features/executions/types`。feature 間 import を避けるため表示用に切り出す。
 */
export type DashboardExecutionItem = {
  /** 実行の表示用 ID。 */
  displayId: string;
  /** 実行のステータス。 */
  status: ExecutionStatus;
  /** 開始日時（ISO 8601）。 */
  startedAt: string;
  /** 最終更新日時（ISO 8601）。無ければ null または省略。 */
  updatedAt?: string | null;
};

/** ダッシュボード用のページング付き実行一覧。 */
export type DashboardExecutionsPage = {
  /** ページ内の実行。 */
  items: DashboardExecutionItem[];
  /** 絞り込み後の総件数。 */
  totalCount: number;
};
