import { apiGet } from "@/shared/api";
import type { AdminModule, KnownAdminModuleStatus } from "./types";

const knownModuleStatuses: ReadonlySet<string> = new Set<KnownAdminModuleStatus>([
  "Loaded",
  "Failed",
  "Skipped",
  "Duplicate"
]);

/**
 * `status` が画面で意味を知っている値かを判定する。
 * @param status API の `status`。
 * @returns 既知の 4 値のどれかなら true。
 */
export function isKnownModuleStatus(status: string): status is KnownAdminModuleStatus {
  return knownModuleStatuses.has(status);
}

/**
 * 応答から契約の項目だけを写す。項目は削らず、契約外の項目を状態へ持ち込まない。
 * @param row API の 1 件。
 * @returns 画面用の Module。
 */
function toAdminModule(row: AdminModule): AdminModule {
  return {
    moduleId: row.moduleId,
    name: row.name,
    version: row.version,
    status: row.status,
    sha256: row.sha256,
    sourceLabel: row.sourceLabel ?? null,
    loadedAtUtc: row.loadedAtUtc,
    message: row.message ?? null
  };
}

/**
 * 呼び出し元テナントの Module のロード結果（`GET /v1/admin/modules`）。読み取りだけで、再ロード（`POST /internal/modules/reload`）は呼ばない。
 * 再ロードと配置は CLI の運用手順に残す。
 * @returns ロードを試みた Module。0 件もある。
 * @throws ApiError 権限が無い（403）やセッション切れ（401）。空の一覧にはしない。
 */
export async function listAdminModules(): Promise<AdminModule[]> {
  const rows = await apiGet<AdminModule[]>("/admin/modules");
  return rows.map(toAdminModule);
}
