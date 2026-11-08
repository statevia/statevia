/** Project ロール。 */
export type ProjectRole = "reader" | "executor" | "publisher" | "admin";

/** 所属 Project の storage ロール。 */
export const PROJECT_ROLES = ["reader", "executor", "publisher", "admin"] as const satisfies readonly ProjectRole[];

/** GET /v1/projects の 1 件。 */
export type ProjectSummary = {
  /** Project ID。 */
  projectId: string;
  /** テナント内で一意な Project の識別名（1〜128 文字）。 */
  slug: string;
  /** 画面に出す Project 名。 */
  displayName: string;
  /** Project の説明。未設定なら null。 */
  description: string | null;
  /** 作成日時（ISO 8601 文字列）。 */
  createdAt: string;
  /** 呼び出し元の実効ロール。未所属の管理一覧では null。 */
  role: ProjectRole | null;
};
