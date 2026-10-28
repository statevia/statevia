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

/** 定義候補を Project ごとにまとめた 1 組。 */
export type ResourceGrantDefinitionGroup = {
  /** まとめた Project の ID。所属が分からない定義の組は null。 */
  projectId: string | null;
  /** 組の見出しに使う Project の表示名。所属が分からない組は null（画面の辞書で出す）。 */
  projectName: string | null;
  definitions: ResourceGrantDefinitionCandidate[];
};

/** Project の名前解決に使う最小の形。 */
export type ResourceGrantProjectName = {
  projectId: string;
  displayName: string;
};

/**
 * 定義候補を所属 Project ごとにまとめる。組は Project 名順、所属が分からない組は最後に置く。
 * 組の中は候補の並び（名前順）を保つ。
 * @param candidates 定義候補。
 * @param projects 名前解決に使う Project 一覧。
 * @returns Project ごとの組。
 */
export function groupDefinitionCandidates(
  candidates: readonly ResourceGrantDefinitionCandidate[],
  projects: readonly ResourceGrantProjectName[]
): ResourceGrantDefinitionGroup[] {
  const projectNames = new Map(projects.map((project) => [project.projectId, project.displayName]));
  const groups = candidates.reduce((byProject, definition) => {
    const key = definition.projectId !== null && projectNames.has(definition.projectId) ? definition.projectId : null;
    byProject.set(key, [...(byProject.get(key) ?? []), definition]);
    return byProject;
  }, new Map<string | null, ResourceGrantDefinitionCandidate[]>());

  const named = [...groups.entries()]
    .flatMap(([projectId, definitions]) =>
      projectId === null ? [] : [{ projectId, projectName: projectNames.get(projectId) ?? null, definitions }]
    )
    .sort((left, right) => (left.projectName ?? "").localeCompare(right.projectName ?? ""));
  const unassigned = groups.get(null);
  return unassigned === undefined ? named : [...named, { projectId: null, projectName: null, definitions: unassigned }];
}

/**
 * 選択中の ID のうち候補に無いものを返す。候補の欠けで付与済み ID を表示から落とさないために使う。
 * @param selectedIds 選択中の ID。
 * @param candidateIds 候補の ID。
 * @returns 候補に無い ID（選択の並び）。
 */
export function idsOutsideCandidates(selectedIds: Iterable<string>, candidateIds: Iterable<string>): string[] {
  const known = new Set(candidateIds);
  return [...selectedIds].filter((id) => !known.has(id));
}

/**
 * project と定義の両方を付けたとき、選んだ project の外にあって許可されない定義を返す。
 * 両方の行があると「両方を満たす定義だけ」が許可されるため、画面で注意を出す（送信は止めない）。
 * 所属が分からない定義（候補に無い ID や `projectId` が無い候補）は判定しない。
 * @param selection 画面の選択。
 * @param candidates 定義候補。
 * @returns 選んだ project の外にある、選択中の定義候補。
 */
export function definitionsOutsideSelectedProjects(
  selection: { projectIds: ReadonlySet<string>; definitionIds: ReadonlySet<string> },
  candidates: readonly ResourceGrantDefinitionCandidate[]
): ResourceGrantDefinitionCandidate[] {
  if (selection.projectIds.size === 0) return [];
  return candidates.filter(
    (definition) =>
      selection.definitionIds.has(definition.resourceId)
      && definition.projectId !== null
      && !selection.projectIds.has(definition.projectId)
  );
}

/**
 * 保存済みでは付いていたのに、今回の本文で空になる種別を返す。
 * 空配列はその種別の追加制限を消すため、保存前に確認する。
 * @param saved 保存済みの許可。
 * @param body 送る本文。
 * @returns 空になる種別（project、定義の順）。
 */
export function clearedGrantFields(saved: PrincipalResourceGrants, body: PrincipalResourceGrants): ResourceGrantField[] {
  const fields: ResourceGrantField[] = ["projectIds", "definitionIds"];
  return fields.filter((field) => saved[field].length > 0 && body[field].length === 0);
}
