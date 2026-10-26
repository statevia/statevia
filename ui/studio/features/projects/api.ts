import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api";
import type { CreateProjectBody } from "./projectCreate";
import {
  PROJECT_MEMBER_KINDS,
  PROJECT_ROLES,
  type ProjectGrantCandidate,
  type ProjectMember,
  type ProjectMemberKind,
  type ProjectRole,
  type ProjectSummary,
} from "./types";

export type { ProjectGrantCandidate, ProjectMember, ProjectRole, ProjectSummary } from "./types";

type MemberWire = {
  principalId?: unknown;
  displayName?: unknown;
  kind?: unknown;
  role?: unknown;
  createdAt?: unknown;
};

type CandidateWire = {
  principalId?: unknown;
  displayName?: unknown;
  kind?: unknown;
};

/**
 * 既知の Project ロールへ狭める。
 * @param value API の role。
 * @returns 一致したロール。それ以外は null。
 */
function readProjectRole(value: unknown): ProjectRole | null {
  return PROJECT_ROLES.find((role) => role === value) ?? null;
}

/**
 * 既知のメンバー種別へ狭める。
 * @param value API の kind。
 * @returns 一致した種別。それ以外は null。
 */
function readMemberKind(value: unknown): ProjectMemberKind | null {
  return PROJECT_MEMBER_KINDS.find((kind) => kind === value) ?? null;
}

/**
 * メンバー応答を画面用の型へ狭める。未知の role / kind は捨てる。
 * @param row API の 1 件。
 * @returns 使えるメンバー。不正なら null。
 */
function toProjectMember(row: MemberWire): ProjectMember | null {
  if (typeof row.principalId !== "string" || typeof row.createdAt !== "string") return null;
  const role = readProjectRole(row.role);
  const kind = readMemberKind(row.kind);
  if (role === null || kind === null) return null;
  return {
    principalId: row.principalId,
    displayName: typeof row.displayName === "string" ? row.displayName : "",
    kind,
    role,
    createdAt: row.createdAt,
  };
}

/**
 * 付与候補を画面用の型へ狭める。system は含めない。
 * @param row API の 1 件。
 * @returns 使える候補。不正なら null。
 */
function toGrantCandidate(row: CandidateWire): ProjectGrantCandidate | null {
  if (typeof row.principalId !== "string") return null;
  const kind = readMemberKind(row.kind);
  if (kind === null || kind === "system") return null;
  return {
    principalId: row.principalId,
    displayName: typeof row.displayName === "string" ? row.displayName : "",
    kind,
  };
}

/**
 * 応答の role を既知のロールへ狭める。未知値は null。
 * @param row API の Project。
 * @returns 画面で使う Project。
 */
function normalizeProject(row: ProjectSummary): ProjectSummary {
  return {
    ...row,
    role: readProjectRole(row.role),
  };
}

/**
 * 所属 Project 一覧。
 * @returns 呼び出し元がメンバーの Project。
 */
export async function listMembershipProjects(): Promise<ProjectSummary[]> {
  const rows = await apiGet<ProjectSummary[]>("/projects");
  return rows.map(normalizeProject);
}

/**
 * Project を作成する。作成者はその Project の `admin` になる（Service API の同一トランザクション）。
 * @param body 検証済みの本文。説明が無いときは項目ごと送らない。
 * @returns 作成した Project。
 * @throws ApiError 権限なし（403）や slug 重複（422、`details.field` が `slug`）。
 */
export async function createProject(body: CreateProjectBody): Promise<ProjectSummary> {
  const row = await apiPost<ProjectSummary>("/projects", body);
  return normalizeProject(row);
}

/**
 * テナント内の全 Project。tenant.admin 専用。
 * @returns 未所属を含む一覧。未所属の role は null。
 */
export async function listAdminProjects(): Promise<ProjectSummary[]> {
  const rows = await apiGet<ProjectSummary[]>("/admin/projects");
  return rows.map(normalizeProject);
}

/**
 * Project メンバー一覧。
 * @param projectId Project UUID。
 * @returns メンバー。
 */
export async function listProjectMembers(projectId: string): Promise<ProjectMember[]> {
  const rows = await apiGet<readonly MemberWire[]>(`/projects/${encodeURIComponent(projectId)}/members`);
  return rows
    .map(toProjectMember)
    .filter((row): row is ProjectMember => row !== null);
}

/**
 * 付与できるユーザーと API キー。
 * @param projectId Project UUID。
 * @returns 名前付きの候補。system は含めない。
 */
export async function listGrantCandidates(projectId: string): Promise<ProjectGrantCandidate[]> {
  const rows = await apiGet<readonly CandidateWire[]>(
    `/projects/${encodeURIComponent(projectId)}/member-candidates`,
  );
  return rows
    .map(toGrantCandidate)
    .filter((row): row is ProjectGrantCandidate => row !== null);
}

/**
 * メンバーを付与またはロール変更する。
 * @param projectId Project UUID。
 * @param principalId 付与先 Principal。
 * @param role storage ロール。
 */
export async function upsertProjectMember(
  projectId: string,
  principalId: string,
  role: ProjectRole,
): Promise<void> {
  await apiPut<null>(
    `/projects/${encodeURIComponent(projectId)}/members/${encodeURIComponent(principalId)}`,
    { role },
  );
}

/**
 * メンバーを剥奪する。
 * @param projectId Project UUID。
 * @param principalId 対象 Principal。
 */
export async function removeProjectMember(projectId: string, principalId: string): Promise<void> {
  await apiDelete(`/projects/${encodeURIComponent(projectId)}/members/${encodeURIComponent(principalId)}`);
}
