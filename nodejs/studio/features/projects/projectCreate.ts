import type { ApiError } from "@/shared/api/apiError";

/** slug の最大長。Service API `CreateProjectRequest.Slug` の `MaxLength(128)` に合わせる。 */
export const PROJECT_SLUG_MAX_LENGTH = 128;

/** 表示名の最大長。Service API `CreateProjectRequest.DisplayName` の `MaxLength(256)` に合わせる。 */
export const PROJECT_DISPLAY_NAME_MAX_LENGTH = 256;

/** 作成フォームの入力（未加工）。 */
export type ProjectCreateInput = {
  slug: string;
  displayName: string;
  description: string;
};

/** `POST /v1/projects` の本文。説明が空白だけのときは項目ごと省く。 */
export type CreateProjectBody = {
  slug: string;
  displayName: string;
  description?: string;
};

/** 項目エラーを出す欄。 */
export type ProjectCreateField = "slug" | "displayName";

/**
 * 項目エラーの種類。文言は画面側の辞書で出す。
 * - `required`: trim 後に空
 * - `tooLong`: trim 後に上限超過
 * - `duplicate`: テナント内で slug が重複（サーバーの 422）
 */
export type ProjectCreateFieldErrorKind = "required" | "tooLong" | "duplicate";

/** 欄ごとの項目エラー。 */
export type ProjectCreateFieldErrors = Partial<Record<ProjectCreateField, ProjectCreateFieldErrorKind>>;

/** 本文の組み立て結果。 */
export type ProjectCreateBuildResult =
  | { ok: true; body: CreateProjectBody }
  | { ok: false; errors: ProjectCreateFieldErrors };

/**
 * trim 済みの値を必須・長さで検査する。
 * @param value trim 済みの値。
 * @param maxLength 上限。
 * @returns 違反の種類。問題なければ undefined。
 */
function checkRequiredLength(value: string, maxLength: number): ProjectCreateFieldErrorKind | undefined {
  if (value.length === 0) return "required";
  if (value.length > maxLength) return "tooLong";
  return undefined;
}

/**
 * 入力を trim して検証し、送信本文を作る。
 * 文字種の規則と表示名からの slug 生成は置かない（Service API の契約に無いため）。
 * @param input フォームの入力。
 * @returns 送信できる本文、または欄ごとの項目エラー。
 */
export function buildCreateProjectBody(input: ProjectCreateInput): ProjectCreateBuildResult {
  const slug = input.slug.trim();
  const displayName = input.displayName.trim();
  const description = input.description.trim();

  const errors: ProjectCreateFieldErrors = {
    slug: checkRequiredLength(slug, PROJECT_SLUG_MAX_LENGTH),
    displayName: checkRequiredLength(displayName, PROJECT_DISPLAY_NAME_MAX_LENGTH),
  };
  if (errors.slug !== undefined || errors.displayName !== undefined) {
    return { ok: false, errors };
  }

  return {
    ok: true,
    body: description.length > 0 ? { slug, displayName, description } : { slug, displayName },
  };
}

/**
 * API エラーが「slug 重複」の 422 か。Service API は `details.field` に `slug` を入れる。
 * @param error `apiPost` が投げた値。
 * @returns slug 欄に出すべきなら true。
 */
export function isSlugConflictError(error: unknown): boolean {
  if (typeof error !== "object" || error === null || !("status" in error) || !("error" in error)) {
    return false;
  }
  const apiError = error as ApiError;
  const details = apiError.error?.details;
  return apiError.status === 422 && typeof details === "object" && details !== null && details.field === "slug";
}
