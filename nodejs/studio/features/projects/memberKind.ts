import { PROJECT_MEMBER_KINDS, type ProjectMemberKind } from "./types";

// includes は要素型の string を受け取る。リテラル配列のままでは string を渡せない。
const projectMemberKindValues: readonly string[] = PROJECT_MEMBER_KINDS;

/**
 * メンバー種別か。
 * @param value API の kind。文字列以外は false。
 * @returns user / serviceAccount / system なら true。
 */
export function isProjectMemberKind(value: unknown): value is ProjectMemberKind {
  return typeof value === "string" && projectMemberKindValues.includes(value);
}
