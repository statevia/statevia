import type { ProjectRole } from "@/shared/project/types";

/** ユーザー、ServiceAccount、またはシステム。ServiceAccount は API キーの有無で分けない。 */
export type ProjectMemberKind = "user" | "serviceAccount" | "system";

/** メンバーの種別。画面ラベル用。 */
export const PROJECT_MEMBER_KINDS = ["user", "serviceAccount", "system"] as const satisfies readonly ProjectMemberKind[];

/** GET /v1/projects/{id}/members の 1 件。principalId は付与 API の識別子で、画面には出さない。 */
export type ProjectMember = {
  /** Principal ID。 */
  principalId: string;
  /** 表示名（ユーザー表示名または ServiceAccount 名）。 */
  displayName: string;
  /** メンバーの種別。 */
  kind: ProjectMemberKind;
  /** Project のロール。 */
  role: ProjectRole;
  /** 付与日時（ISO 8601）。 */
  createdAt: string;
};

/** GET /v1/projects/{id}/member-candidates の 1 件。 */
export type ProjectGrantCandidate = {
  /** Principal ID。 */
  principalId: string;
  /** 表示名。 */
  displayName: string;
  /** 候補の種別。 */
  kind: ProjectMemberKind;
};
