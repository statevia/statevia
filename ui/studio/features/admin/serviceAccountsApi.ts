import { apiGet, apiPatch, apiPost } from "@/shared/api";
import type { CreateServiceAccountBody, UpdateServiceAccountBody } from "./serviceAccounts";
import type { AdminServiceAccount } from "./types";

/**
 * 応答から画面で使う項目だけを写す。契約外の項目（平文キーなど）を状態へ持ち込まない。
 * @param row API の 1 件。
 * @returns 画面用の ServiceAccount。
 */
function toServiceAccount(row: AdminServiceAccount): AdminServiceAccount {
  return {
    serviceAccountId: row.serviceAccountId,
    principalId: row.principalId,
    name: row.name,
    isActive: row.isActive,
    hasApiKey: row.hasApiKey,
    groupIds: row.groupIds,
    createdAt: row.createdAt
  };
}

/**
 * 詳細・更新のパス。
 * @param serviceAccountId ServiceAccount ID。
 * @returns エンコード済みのパス。
 */
function serviceAccountPath(serviceAccountId: string): string {
  return `/admin/service-accounts/${encodeURIComponent(serviceAccountId)}`;
}

/**
 * テナント内の ServiceAccount 一覧（`GET /v1/admin/service-accounts`）。
 * この API 群は削除と API キー発行を持たない。キー付き主体の発行は API キー画面に残す。
 * @returns キー付きと資格のない主体の両方。
 * @throws ApiError 非管理者（403）やセッション切れ（401）。
 */
export async function listServiceAccounts(): Promise<AdminServiceAccount[]> {
  const rows = await apiGet<AdminServiceAccount[]>("/admin/service-accounts");
  return rows.map(toServiceAccount);
}

/**
 * ServiceAccount 詳細。
 * @param serviceAccountId ServiceAccount ID。
 * @returns 詳細。
 * @throws ApiError 他テナントや存在しない ID（404）。
 */
export async function getServiceAccount(serviceAccountId: string): Promise<AdminServiceAccount> {
  const row = await apiGet<AdminServiceAccount>(serviceAccountPath(serviceAccountId));
  return toServiceAccount(row);
}

/**
 * 資格のない ServiceAccount を作る。API キーは発行されない。
 * @param body `buildCreateServiceAccountBody` で検証済みの本文。
 * @returns 作成した ServiceAccount。
 * @throws ApiError 名前の形式違反や空のグループ（422）。
 */
export async function createServiceAccount(body: CreateServiceAccountBody): Promise<AdminServiceAccount> {
  const row = await apiPost<AdminServiceAccount>("/admin/service-accounts", body);
  return toServiceAccount(row);
}

/**
 * 有効・表示名・所属を部分更新する。無効化すると Principal が無効になり、紐づくスケジュールは自動では止まらない。
 * @param serviceAccountId ServiceAccount ID。
 * @param body `buildUpdateServiceAccountBody` で作った、変えた項目だけの本文。
 * @returns 更新後の ServiceAccount。
 * @throws ApiError 名前の形式違反や空のグループ（422）、存在しない ID（404）。
 */
export async function updateServiceAccount(
  serviceAccountId: string,
  body: UpdateServiceAccountBody
): Promise<AdminServiceAccount> {
  const row = await apiPatch<AdminServiceAccount>(serviceAccountPath(serviceAccountId), body);
  return toServiceAccount(row);
}
