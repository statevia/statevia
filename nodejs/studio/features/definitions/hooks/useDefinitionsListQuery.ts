"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useCallback, useMemo } from "react";
import {
  buildDefinitionsListPath,
  type DefinitionsListQuery,
  type SortOrder,
} from "../api";

/** 定義一覧の既定ページサイズ。 */
export const DEFINITIONS_PAGE_SIZE = 20;

/** 定義一覧のソートキー。 */
export type DefinitionsSortBy = "createdAt" | "updatedAt" | "name";

/**
 * URL searchParams から定義一覧クエリを読む。
 * @param searchParams Next.js searchParams（`get` のみ必要）
 * @returns DefinitionsListQuery
 */
export function readDefinitionsListQuery(searchParams: {
  get: (name: string) => string | null;
}): DefinitionsListQuery {
  const limitRaw = Number.parseInt(searchParams.get("limit") ?? "", 10);
  const limit = Number.isFinite(limitRaw) ? Math.max(1, limitRaw) : DEFINITIONS_PAGE_SIZE;
  const offsetRaw = Number.parseInt(searchParams.get("offset") ?? "0", 10);
  const offset = Number.isFinite(offsetRaw) && offsetRaw >= 0 ? offsetRaw : 0;
  const name = searchParams.get("name")?.trim() ?? "";
  const sortByRaw = searchParams.get("sortBy")?.trim() ?? "";
  const sortOrderRaw = searchParams.get("sortOrder")?.trim() ?? "";
  const sortBy = toDefinitionsSortBy(sortByRaw);
  const sortOrder: SortOrder = sortOrderRaw === "asc" ? "asc" : "desc";
  const includeDeleted = searchParams.get("includeDeleted") === "true";
  return {
    pagination: { limit, offset },
    sort: { sortBy, sortOrder },
    name: name || undefined,
    includeDeleted: includeDeleted || undefined,
  };
}

/**
 * 並び替えの項目へ寄せる。未知値は作成日時（API の既定）。
 * @param value URL または選択欄の文字列。
 * @returns 許可された項目。
 */
function toDefinitionsSortBy(value: string | undefined): DefinitionsSortBy {
  switch (value) {
    case "name":
    case "updatedAt":
      return value;
    default:
      return "createdAt";
  }
}

/** 定義一覧 URL 同期の戻り値。 */
export type UseDefinitionsListQueryResult = {
  /** URL から読んだ一覧のクエリ。 */
  listQuery: DefinitionsListQuery;
  /** 今のページ番号（1 始まり）。 */
  currentPage: number;
  /** 実際に使う並び替えの項目（未知値は createdAt）。 */
  effectiveSortBy: DefinitionsSortBy;
  /** 実際に使う並び順（未指定なら desc）。 */
  effectiveSortOrder: SortOrder;
  /** 論理削除済みの定義も含めるか。 */
  includeDeleted: boolean;
  /** クエリを URL に反映する（履歴は積まない）。 */
  goTo: (query: DefinitionsListQuery) => void;
};

/**
 * 定義一覧の URL クエリ読み取りと置換ナビゲーション。
 */
export function useDefinitionsListQuery(): UseDefinitionsListQueryResult {
  const router = useRouter();
  const searchParams = useSearchParams();
  const listQuery = useMemo(() => readDefinitionsListQuery(searchParams), [searchParams]);
  const currentPage = useMemo(
    () => Math.floor(listQuery.pagination.offset / listQuery.pagination.limit) + 1,
    [listQuery.pagination.limit, listQuery.pagination.offset],
  );
  const effectiveSortBy = toDefinitionsSortBy(listQuery.sort.sortBy);
  const effectiveSortOrder: SortOrder = listQuery.sort.sortOrder ?? "desc";
  const includeDeleted = listQuery.includeDeleted === true;

  const goTo = useCallback(
    (query: DefinitionsListQuery) => {
      router.replace(buildDefinitionsListPath(query), { scroll: false });
    },
    [router],
  );

  return {
    listQuery,
    currentPage,
    effectiveSortBy,
    effectiveSortOrder,
    includeDeleted,
    goTo,
  };
}
