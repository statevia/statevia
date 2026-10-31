import { PROJECT_ROLES, type ProjectRole } from "./types";

// includes は要素型の string を受け取る。リテラル配列のままでは string を渡せない。
const projectRoleValues: readonly string[] = PROJECT_ROLES;

/**
 * storage 文字列が Project ロールか。
 * @param value API の role。文字列以外は false。
 * @returns 既知の 4 ロールなら true。
 */
export function isProjectRole(value: unknown): value is ProjectRole {
  return typeof value === "string" && projectRoleValues.includes(value);
}

/**
 * 既知の Project ロールへ狭める。
 * @param value API の role。
 * @returns 一致したロール。それ以外は null。
 */
export function readProjectRole(value: unknown): ProjectRole | null {
  return PROJECT_ROLES.find((role) => role === value) ?? null;
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
