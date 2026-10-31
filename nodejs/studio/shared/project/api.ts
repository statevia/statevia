import { apiGet } from "@/shared/api";
import { readProjectRole } from "./roles";
import type { ProjectSummary } from "./types";

/**
 * 応答の role を既知のロールへ狭める。未知値は null。
 * @param row API の Project。
 * @returns 画面で使う Project。
 */
export function normalizeProject(row: ProjectSummary): ProjectSummary {
  return {
    ...row,
    role: readProjectRole(row.role),
  };
}

/**
 * 所属 Project 一覧。ヘッダーの Project 切り替えと、画面のロール判定が使う。
 * @returns 呼び出し元がメンバーの Project。
 */
export async function listMembershipProjects(): Promise<ProjectSummary[]> {
  const rows = await apiGet<ProjectSummary[]>("/projects");
  return rows.map(normalizeProject);
}
