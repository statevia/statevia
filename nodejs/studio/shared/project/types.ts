/** Project ロール。 */
export type ProjectRole = "reader" | "executor" | "publisher" | "admin";

/** 所属 Project の storage ロール。 */
export const PROJECT_ROLES = ["reader", "executor", "publisher", "admin"] as const satisfies readonly ProjectRole[];

/** GET /v1/projects の 1 件。 */
export type ProjectSummary = {
  projectId: string;
  slug: string;
  displayName: string;
  description: string | null;
  createdAt: string;
  /** 呼び出し元の実効ロール。未所属の管理一覧では null。 */
  role: ProjectRole | null;
};
