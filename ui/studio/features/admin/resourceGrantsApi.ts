import { apiGet, apiPut } from "@/shared/api";
import { listDefinitions } from "@/features/definitions/api";
import {
  DEFINITION_CANDIDATES_PAGE_LIMIT,
  resourceGrantsPath,
  type PrincipalResourceGrants,
  type ResourceGrantDefinitionCandidate,
  type ResourceGrantSubject
} from "./resourceGrants";

/** 定義候補の 1 ページ。 */
export type ResourceGrantDefinitionCandidatePage = {
  candidates: ResourceGrantDefinitionCandidate[];
  /** 1 ページに収まらない定義があるか。true のとき候補は欠けている。 */
  hasMore: boolean;
};

/**
 * 応答から両配列だけを写す。
 * @param row API の応答。
 * @returns 画面用の許可。
 */
function toGrants(row: PrincipalResourceGrants): PrincipalResourceGrants {
  return { projectIds: [...row.projectIds], definitionIds: [...row.definitionIds] };
}

/**
 * 主体の実行リソース許可を読む。
 * @param subject 対象の主体。
 * @returns 付与済みの project と定義の ID。空配列はその種別の追加制限が無い。
 * @throws ApiError 他テナントや存在しない主体（404）、非管理者（403）。
 */
export async function getResourceGrants(subject: ResourceGrantSubject): Promise<PrincipalResourceGrants> {
  return toGrants(await apiGet<PrincipalResourceGrants>(resourceGrantsPath(subject)));
}

/**
 * 主体の実行リソース許可を両種別まとめて置換する。
 * @param subject 対象の主体。
 * @param body `buildReplaceResourceGrantsBody` で作った本文。両配列を必ず含む。
 * @returns 置換後の許可。
 * @throws ApiError 上限超過や、付与先が executor 以上で所属していない project とその定義（422。既存の許可は残る）、未知 ID（404）。
 */
export async function replaceResourceGrants(
  subject: ResourceGrantSubject,
  body: PrincipalResourceGrants
): Promise<PrincipalResourceGrants> {
  return toGrants(await apiPut<PrincipalResourceGrants>(resourceGrantsPath(subject), body));
}

/**
 * 定義候補を 1 ページだけ読む（未削除、名前順）。
 * 一覧は操作する管理者自身が所属し、その許可に入る定義だけを返すため、付与先の許可に足りる全定義とは限らない。
 * @returns 候補と、欠けがあるか。
 * @throws ApiError 一覧の取得失敗。
 */
export async function listDefinitionCandidates(): Promise<ResourceGrantDefinitionCandidatePage> {
  const page = await listDefinitions({
    pagination: { limit: DEFINITION_CANDIDATES_PAGE_LIMIT, offset: 0 },
    sort: { sortBy: "name", sortOrder: "asc" }
  });
  return {
    candidates: page.items.map((definition) => ({
      resourceId: definition.resourceId,
      name: definition.name,
      displayId: definition.displayId,
      projectId: definition.projectId ?? null
    })),
    hasMore: page.hasMore
  };
}
