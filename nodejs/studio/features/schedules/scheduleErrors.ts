import type { ApiError } from "@/shared/api";
import type { ScheduleField } from "./schedules";

/**
 * 保存・手動実行の失敗を、画面の出し分けに使う種類へ分けたもの。文言は画面側の辞書で出す。
 * 契約は `docs/specifications/api-http.md` §3.12 の発火準備検査。
 *
 * - `runAsUnusable`: 422 `field=runAsPrincipalId`。有効な ServiceAccount でないか、定義の Project で executor 未満。
 *   API は 2 つを field で分けないため、画面は同じ文言とメンバー管理へのリンクを出す
 * - `definitionUnavailable`: 422 `field=definitionId`。定義が論理削除されている
 * - `fieldMessage`: 上記以外で欄が分かる 422。API のメッセージをその欄に出す
 * - `runAsGrantDenied`: 保存・手動実行の 403 `RESOURCE_GRANT_DENIED`。run-as の実行リソース許可に定義が入っていない
 *   （呼び出し元の許可ではない）
 * - `projectAccessDenied`: 403 `PROJECT_ACCESS_DENIED`。呼び出し元が定義の Project で executor 未満
 * - `notFound`: 404。見つからない、または所属外
 * - `other`: それ以外。汎用のエラー表示に回す
 *
 * `disableHint` は「無効にすれば保存できる」を添えるか。保存後の行が有効なときの 422 だけ true（無効な行では API が検査しない）。
 */
export type ScheduleWriteError =
  | { kind: "runAsUnusable"; disableHint: boolean }
  | { kind: "definitionUnavailable"; disableHint: boolean }
  | { kind: "fieldMessage"; field: ScheduleField; message: string }
  | { kind: "runAsGrantDenied" }
  | { kind: "projectAccessDenied" }
  | { kind: "notFound" }
  | { kind: "other" };

/** API の項目名（小文字）から画面の欄への対応。 */
const FIELD_BY_API_NAME: Readonly<Record<string, ScheduleField>> = {
  name: "name",
  definitionid: "definitionId",
  cronexpression: "cronExpression",
  timezone: "timeZone",
  overlappolicy: "overlapPolicy",
  runasprincipalid: "runAsPrincipalId",
  definitionversion: "definitionVersion",
  input: "input"
};

/**
 * 値が `ApiError` の形か。
 * @param error 捕まえた値。
 * @returns `status` と `error.code` を持てば true。
 */
function isApiError(error: unknown): error is ApiError {
  if (typeof error !== "object" || error === null || !("error" in error)) return false;
  const body: unknown = (error as { error?: unknown }).error;
  return typeof body === "object" && body !== null && typeof (body as { code?: unknown }).code === "string";
}

/**
 * API の項目名を画面の欄へ写す。形式検証は `Name` や `$.name` の形でも来るため、大文字小文字と `$.` を無視する。
 * @param raw API の項目名。
 * @returns 画面の欄。対応が無ければ undefined。
 */
function toScheduleField(raw: unknown): ScheduleField | undefined {
  if (typeof raw !== "string") return undefined;
  return FIELD_BY_API_NAME[raw.replace(/^\$\./, "").toLowerCase()];
}

/**
 * `error.details` から最初の欄を読む。`{ field }`（ドメイン検証）と `[{ field, message }]`（形式検証）の両方を扱う。
 * @param details 422 の details。
 * @returns 欄。無ければ undefined。
 */
export function readErrorField(details: ApiError["error"]["details"]): ScheduleField | undefined {
  if (details === undefined) return undefined;
  if (Array.isArray(details)) {
    return details
      .map((item: unknown) => (typeof item === "object" && item !== null ? toScheduleField((item as { field?: unknown }).field) : undefined))
      .find((field) => field !== undefined);
  }
  return toScheduleField((details as Record<string, unknown>).field);
}

/**
 * 保存・手動実行の失敗を分類する。
 * @param error 捕まえた値。
 * @param enabledAfterSave 保存後の行が有効か。手動実行では false を渡す（無効化の案内を出さない）。
 * @returns 画面の出し分けに使う種類。
 */
export function classifyScheduleWriteError(error: unknown, enabledAfterSave: boolean): ScheduleWriteError {
  if (!isApiError(error)) return { kind: "other" };
  switch (error.status) {
    case 422: {
      const field = readErrorField(error.error.details);
      switch (field) {
        case "runAsPrincipalId":
          return { kind: "runAsUnusable", disableHint: enabledAfterSave };
        case "definitionId":
          return { kind: "definitionUnavailable", disableHint: enabledAfterSave };
        case undefined:
          return { kind: "other" };
        default:
          return { kind: "fieldMessage", field, message: error.error.message };
      }
    }
    case 403:
      switch (error.error.code) {
        case "RESOURCE_GRANT_DENIED":
          return { kind: "runAsGrantDenied" };
        case "PROJECT_ACCESS_DENIED":
          return { kind: "projectAccessDenied" };
        default:
          return { kind: "other" };
      }
    case 404:
      return { kind: "notFound" };
    default:
      return { kind: "other" };
  }
}

/**
 * 単票 GET の失敗の種類。403 は呼び出し元の実行リソース許可の外で、保存時の run-as の 403 とは意味が違う。
 * @param error 捕まえた値。
 * @returns `notFound`（404）、`forbidden`（403）、`error`（それ以外）。
 */
export function classifyScheduleReadError(error: unknown): "notFound" | "forbidden" | "error" {
  if (!isApiError(error)) return "error";
  switch (error.status) {
    case 404:
      return "notFound";
    case 403:
      return "forbidden";
    default:
      return "error";
  }
}
