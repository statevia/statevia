import { PROJECT_MEMBER_KINDS, PROJECT_ROLES, type ProjectMemberKind, type ProjectRole } from "./types";

// includes は要素型の string を受け取る。リテラル配列のままでは string を渡せない。
const projectRoleValues: readonly string[] = PROJECT_ROLES;
const projectMemberKindValues: readonly string[] = PROJECT_MEMBER_KINDS;

/**
 * storage 文字列が Project ロールか。
 * @param value API の role。文字列以外は false。
 * @returns 既知の 4 ロールなら true。
 */
export function isProjectRole(value: unknown): value is ProjectRole {
  return typeof value === "string" && projectRoleValues.includes(value);
}

/**
 * メンバー種別か。
 * @param value API の kind。文字列以外は false。
 * @returns user / serviceAccount / system なら true。
 */
export function isProjectMemberKind(value: unknown): value is ProjectMemberKind {
  return typeof value === "string" && projectMemberKindValues.includes(value);
}

/**
 * 実行開始できるロールか。
 * @param role 実効ロール。
 * @returns executor 以上なら true。
 */
export function canExecute(role: string | null | undefined): boolean {
  return role === "executor" || role === "publisher" || role === "admin";
}

/**
 * 定義の作成と編集ができるロールか。
 * @param role 実効ロール。
 * @returns publisher 以上なら true。
 */
export function canPublish(role: string | null | undefined): boolean {
  return role === "publisher" || role === "admin";
}

/**
 * メンバーを管理できるロールか。
 * @param role 実効ロール。
 * @returns project.admin なら true。
 */
export function canAdmin(role: string | null | undefined): boolean {
  return role === "admin";
}
