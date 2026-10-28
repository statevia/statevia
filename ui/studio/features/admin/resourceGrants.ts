/**
 * 実行リソース許可（`/v1/admin/.../resource-grants`）の画面用の型と本文の組み立て。
 * 契約は `docs/specifications/api-http.md` §4.1.3。空配列は「禁止」ではなく、その種別の追加制限が無いことを表す。
 */

/**
 * 1 Principal に付けられる project 許可の上限。
 * Start が許可をメモリで評価する契約のため API が 32 件で 422 を返す。画面は超える本文を送らない。
 */
export const MAX_GRANTED_PROJECTS = 32;

/**
 * 1 Principal に付けられる定義許可の上限。理由は {@link MAX_GRANTED_PROJECTS} と同じ（API は 128 件）。
 */
export const MAX_GRANTED_DEFINITIONS = 128;

/**
 * 定義候補を読む 1 ページの件数。`GET /v1/definitions` の `limit` 上限（500）に合わせる。
 * 候補の不足を補うためにページを重ねて読まない（`hasMore` は画面で注記する）。
 */
export const DEFINITION_CANDIDATES_PAGE_LIMIT = 500;

/** 許可を編集する主体。種別ごとに API のパスだけが違う。 */
export type ResourceGrantSubject =
  | { kind: "user"; userId: string }
  | { kind: "serviceAccount"; serviceAccountId: string };

/** Principal の実行リソース許可（GET の応答、PUT の本文と同形）。 */
export type PrincipalResourceGrants = {
  projectIds: string[];
  definitionIds: string[];
};

/** 定義候補 1 件。`GET /v1/definitions` から画面で使う項目だけを写したもの。 */
export type ResourceGrantDefinitionCandidate = {
  resourceId: string;
  name: string;
  displayId: string;
  /** 所属 Project。API が返さないときは null（Project 名でまとめられない）。 */
  projectId: string | null;
};

/** 件数上限を超えた種別。 */
export type ResourceGrantField = "projectIds" | "definitionIds";

/** 種別ごとの上限超過。文言は画面側の辞書で出す。 */
export type ResourceGrantLimitErrors = Partial<Record<ResourceGrantField, "tooMany">>;

/** 置換本文の組み立て結果。 */
export type ResourceGrantsBuildResult =
  | { ok: true; body: PrincipalResourceGrants }
  | { ok: false; errors: ResourceGrantLimitErrors };

/** 本文を作るための選択（未加工）。 */
export type ResourceGrantsSelection = {
  projectIds: Iterable<string>;
  definitionIds: Iterable<string>;
};

/**
 * 主体ごとの許可のパス。ID はエンコードする。
 * @param subject 対象の主体。
 * @returns `/admin/.../resource-grants`。
 */
export function resourceGrantsPath(subject: ResourceGrantSubject): string {
  switch (subject.kind) {
    case "user":
      return `/admin/users/${encodeURIComponent(subject.userId)}/resource-grants`;
    case "serviceAccount":
      return `/admin/service-accounts/${encodeURIComponent(subject.serviceAccountId)}/resource-grants`;
  }
}

/**
 * PUT の本文を作る。両方の配列を必ず持ち、重複を除く。上限を超えるときは送らない。
 * 候補に無い付与済み ID も選択にあれば残す（候補の欠けで許可を黙って落とさない）。
 * @param selection 画面の選択。
 * @returns 送信できる本文、または種別ごとの上限超過。
 */
export function buildReplaceResourceGrantsBody(selection: ResourceGrantsSelection): ResourceGrantsBuildResult {
  const projectIds = [...new Set(selection.projectIds)];
  const definitionIds = [...new Set(selection.definitionIds)];
  const errors: ResourceGrantLimitErrors = {
    ...(projectIds.length > MAX_GRANTED_PROJECTS ? { projectIds: "tooMany" } : {}),
    ...(definitionIds.length > MAX_GRANTED_DEFINITIONS ? { definitionIds: "tooMany" } : {})
  };
  return Object.keys(errors).length === 0 ? { ok: true, body: { projectIds, definitionIds } } : { ok: false, errors };
}
