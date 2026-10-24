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

/** ユーザー、API キー、またはシステム。 */
export type ProjectMemberKind = "user" | "apiKey" | "system";

/** メンバーの種別。画面ラベル用。 */
export const PROJECT_MEMBER_KINDS = ["user", "apiKey", "system"] as const satisfies readonly ProjectMemberKind[];

/** GET /v1/projects/{id}/members の 1 件。principalId は付与 API の識別子で、画面には出さない。 */
export type ProjectMember = {
  principalId: string;
  displayName: string;
  kind: ProjectMemberKind;
  role: ProjectRole;
  createdAt: string;
};

/** GET /v1/projects/{id}/member-candidates の 1 件。 */
export type ProjectGrantCandidate = {
  principalId: string;
  displayName: string;
  kind: ProjectMemberKind;
};
