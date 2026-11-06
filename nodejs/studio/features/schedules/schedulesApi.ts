import { apiDelete, apiGet, apiPatch, apiPost } from "@/shared/api";
import { listDefinitions } from "@/features/definitions";
import type { CreateScheduleBody, UpdateScheduleBody } from "./schedules";
import type {
  RunAsCandidate,
  ScheduleDefinitionOptionPage,
  ScheduleDetail,
  ScheduleListItem,
  ScheduleRunResult
} from "./types";

/**
 * 定義候補を読む 1 ページの件数。`GET /v1/definitions` の `limit` 上限（500）に合わせる。
 * 一覧は呼び出し元の所属と許可で絞られるため、スケジュールを作れる定義はこの中にある。
 */
export const SCHEDULE_DEFINITION_OPTIONS_LIMIT = 500;

/** 定期実行の API の基底パス（`/api/core` が `/v1` を付ける）。 */
const SCHEDULES_PATH = "/schedules";

/**
 * 1 件のパス。
 * @param scheduleId スケジュール ID。
 * @returns エンコード済みのパス。
 */
function schedulePath(scheduleId: string): string {
  return `${SCHEDULES_PATH}/${encodeURIComponent(scheduleId)}`;
}

/**
 * 応答から一覧の項目だけを写す。一覧型に `input` を持ち込まない。
 * @param row API の 1 件。
 * @returns 画面用の一覧行。
 */
function toListItem(row: ScheduleListItem): ScheduleListItem {
  return {
    scheduleId: row.scheduleId,
    name: row.name,
    definitionId: row.definitionId,
    definitionVersionId: row.definitionVersionId,
    runAsPrincipalId: row.runAsPrincipalId,
    cronExpression: row.cronExpression,
    timeZone: row.timeZone,
    overlapPolicy: row.overlapPolicy,
    enabled: row.enabled,
    nextFireAt: row.nextFireAt,
    createdAt: row.createdAt,
    updatedAt: row.updatedAt
  };
}

/**
 * 呼び出し元が閲覧できるスケジュールの一覧（`GET /v1/schedules`）。
 * 所属の無い Project の行と、システムの点検行は API が返さない。行ごとの単票 GET はしない。
 * @returns 一覧（`input` なし）。
 * @throws ApiError 権限不足（403）やセッション切れ（401）。
 */
export async function listSchedules(): Promise<ScheduleListItem[]> {
  const rows = await apiGet<ScheduleListItem[]>(SCHEDULES_PATH);
  return rows.map(toListItem);
}

/**
 * 単票（`input` を含む）。編集フォームでだけ呼ぶ。
 * @param scheduleId スケジュール ID。
 * @returns 単票。
 * @throws ApiError 不在・所属外（404）、呼び出し元の実行リソース許可の外（403）。
 */
export async function getSchedule(scheduleId: string): Promise<ScheduleDetail> {
  const row = await apiGet<ScheduleDetail>(schedulePath(scheduleId));
  return { ...toListItem(row), createdByPrincipalId: row.createdByPrincipalId, input: row.input ?? null };
}

/**
 * スケジュールを作る。有効なら API が発火準備検査をする。
 * @param body `buildCreateScheduleBody` で検証済みの本文。
 * @returns 作成した単票。
 * @throws ApiError 422（項目・発火準備検査）、403、404。
 */
export async function createSchedule(body: CreateScheduleBody): Promise<ScheduleDetail> {
  return apiPost<ScheduleDetail>(SCHEDULES_PATH, body);
}

/**
 * 変えた項目だけを部分更新する。保存後の行が有効なら API が発火準備検査をする。
 * @param scheduleId スケジュール ID。
 * @param body `buildUpdateScheduleBody` で作った本文。
 * @returns 更新後の単票。
 * @throws ApiError 422（項目・発火準備検査）、403、404。
 */
export async function updateSchedule(scheduleId: string, body: UpdateScheduleBody): Promise<ScheduleDetail> {
  return apiPatch<ScheduleDetail>(schedulePath(scheduleId), body);
}

/**
 * スケジュールを論理削除する。run-as と定義の状態では止まらない。
 * @param scheduleId スケジュール ID。
 * @throws ApiError 不在・所属外（404）、403。
 */
export async function deleteSchedule(scheduleId: string): Promise<void> {
  await apiDelete(schedulePath(scheduleId));
}

/**
 * 手動で 1 回 Start する。次枠は進まず、重なりの skip は適用されない。
 * 冪等キーは付けない（確認後に 1 回だけ呼ぶ）。
 * @param scheduleId スケジュール ID。
 * @returns 開始した実行の display ID。
 * @throws ApiError 無効な行（422 `field=enabled`）、発火準備検査の 422 / 403、404。
 */
export async function runSchedule(scheduleId: string): Promise<ScheduleRunResult> {
  const response = await apiPost<ScheduleRunResult>(`${schedulePath(scheduleId)}/run`, {});
  return { displayId: response.displayId };
}

/** `GET /v1/admin/service-accounts` の 1 件のうち、候補に使う項目。 */
type ServiceAccountRow = {
  principalId: string;
  name: string;
  isActive: boolean;
};

/**
 * run-as の候補（有効な ServiceAccount）。API キーの有無では絞らない。定義の Project のメンバーにも絞らない
 * （所属の不足は保存時の 422 で知らせる）。
 * @returns 名前順の候補。
 * @throws ApiError テナント管理者でない（403）。
 */
export async function listRunAsCandidates(): Promise<RunAsCandidate[]> {
  const rows = await apiGet<ServiceAccountRow[]>("/admin/service-accounts");
  return rows
    .filter((row) => row.isActive)
    .map((row) => ({ principalId: row.principalId, name: row.name }))
    .sort((left, right) => left.name.localeCompare(right.name));
}

/**
 * 定義候補を 1 ページだけ読む（未削除、名前順）。一覧の定義名の対応表にも使う。
 * @returns 候補と、欠けがあるか。
 * @throws ApiError 一覧の取得失敗。
 */
export async function listScheduleDefinitionOptions(): Promise<ScheduleDefinitionOptionPage> {
  const page = await listDefinitions({
    pagination: { limit: SCHEDULE_DEFINITION_OPTIONS_LIMIT, offset: 0 },
    sort: { sortBy: "name", sortOrder: "asc" }
  });
  return {
    options: page.items.map((definition) => ({
      resourceId: definition.resourceId,
      displayId: definition.displayId,
      name: definition.name
    })),
    hasMore: page.hasMore
  };
}
