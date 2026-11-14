"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { useProject } from "@/shared/project/projectContext";
import { apiGet } from "@/shared/api";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { matchesPattern } from "@/shared/lib/validation/primitives";
import { DEFINITION_ID_PATTERN, SEARCH_NAME_PATTERN } from "@/shared/lib/validation/searchRules";
import {
  buildExecutionsListPath,
  type ExecutionsListQuery,
  type SortOrder
} from "../api";
import type { ExecutionDTO, PagedExecutions } from "../types";

const DEFAULT_LIMIT = 20;
const MAX_LIMIT = 500;

/** 一覧フィルタの状態セレクト値（API の契約の値）。空文字は未指定。`UNKNOWN` は API が受けないので含めない。 */
export type ExecutionStatusFilter = "" | "ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED";

/** 一覧のソートキー。URL の未知値は更新日時に寄せる。 */
export type ExecutionSortBy = "updatedAt" | "displayId";

/** 実行一覧の描画用状態。ReactNode は含まない。 */
export type ExecutionsPageModel = {
  /** 取得済みの実行。失敗時は null。 */
  items: ExecutionDTO[] | null;
  /** 全件数。未取得または失敗時は null。 */
  totalCount: number | null;
  /** 一覧取得中か。 */
  loading: boolean;
  /** 取得失敗・検証失敗・受理のトースト。無ければ null。 */
  toast: ToastState | null;
  /** URL 上の定義 ID。条件のチップに使う。無ければ undefined。 */
  definitionId: string | undefined;
  /** URL 上の実行 ID の検索語。条件のチップに使う。無ければ undefined。 */
  name: string | undefined;
  /** 実行 ID の検索語の検証エラー。入力欄の下に出す。無ければ null。 */
  nameError: string | null;
  /** 定義 ID の検証エラー。入力欄の下に出す。無ければ null。 */
  definitionError: string | null;
  /** 状態フィルタ。空文字はすべて。 */
  status: ExecutionStatusFilter;
  /** ソートキー。 */
  sortBy: ExecutionSortBy;
  /** ソート順。 */
  sortOrder: SortOrder;
  /** 名前フィルタの入力中の値。URL とは送信まで別。 */
  nameDraft: string;
  /** 定義 ID フィルタの入力中の値。URL とは送信まで別。 */
  definitionDraft: string;
  /** 1 ページの件数。 */
  limit: number;
  /** 現在ページの先頭オフセット。 */
  offset: number;
  /** 1 始まりのページ番号。 */
  currentPage: number;
  /** 前ページがあるか。 */
  hasPrev: boolean;
  /** 次ページがあるか。 */
  hasNext: boolean;
  /**
   * 名前入力を更新する。
   * @param value 入力値。
   */
  setNameDraft: (value: string) => void;
  /**
   * 定義 ID 入力を更新する。
   * @param value 入力値。
   */
  setDefinitionDraft: (value: string) => void;
  /**
   * フィルタを検証して URL を更新する。不正なら一覧は取り直さない。
   * @param event フォームの submit。
   */
  submitFilters: (event: FormEvent) => void;
  /**
   * 状態を変えて先頭ページへ移す。名前と定義 ID は入力中の値を使う。
   * @param value セレクトの値。
   */
  changeStatus: (value: string) => void;
  /**
   * 並びの項目と順序を一度に変えて先頭ページへ移す（1 つの選択欄で選ぶため）。
   * @param sortBy 並びの項目。
   * @param sortOrder 並びの順序。
   */
  changeSort: (sortBy: ExecutionSortBy, sortOrder: SortOrder) => void;
  /** フィルタを消して先頭ページへ移す。 */
  clearFilters: () => void;
  /** 定義 ID の条件だけ外す。名前は入力中の値を残す。 */
  clearDefinitionContext: () => void;
  /** 実行 ID の検索語の条件だけ外す。 */
  clearName: () => void;
  /** 前のページへ移す。 */
  goToPreviousPage: () => void;
  /** 次のページへ移す。 */
  goToNextPage: () => void;
  /**
   * 一覧を再取得する。選択中 Project で絞る。所属一覧の取得前は取得せず、選択が無ければ取得せず空にする。
   * @returns 取得できたか（選択が無く空にしたときも true）。所属一覧の取得前と失敗時は false。失敗時はトーストを残す。
   */
  reload: () => Promise<boolean>;
  /** 集合配送の受理トーストを出す。 */
  notifyIngressAccepted: () => void;
  /** トーストを閉じる。 */
  dismissToast: () => void;
};

/**
 * クエリから一覧の取得条件を正規化する。無効な値は既定に寄せる。
 * @param searchParams URL の検索パラメータ。
 * @returns 一覧 API と画面 URL で共有するクエリ。
 */
function readListQuery(searchParams: { get: (name: string) => string | null }): ExecutionsListQuery {
  const limitRaw = Number.parseInt(searchParams.get("limit") ?? "", 10);
  const limit = Number.isFinite(limitRaw) ? Math.min(MAX_LIMIT, Math.max(1, limitRaw)) : DEFAULT_LIMIT;
  const offsetRaw = Number.parseInt(searchParams.get("offset") ?? "0", 10);
  const offset = Number.isFinite(offsetRaw) && offsetRaw >= 0 ? offsetRaw : 0;
  const status = toStatusFilter(searchParams.get("status")?.trim() ?? "");
  const name = searchParams.get("name")?.trim() ?? "";
  const definitionId = searchParams.get("definitionId")?.trim() ?? "";
  const sortBy = toSortBy(searchParams.get("sortBy")?.trim() ?? "");
  const sortOrder = toSortOrder(searchParams.get("sortOrder")?.trim() ?? "");
  return {
    pagination: { limit, offset },
    sort: { sortBy, sortOrder },
    status: status || undefined,
    name: name || undefined,
    definitionId: definitionId || undefined
  };
}

/**
 * 状態セレクト値へ寄せる。未知値は未指定。
 * @param value URL またはセレクトの文字列。
 * @returns 空文字または許可された状態。
 */
function toStatusFilter(value: string): ExecutionStatusFilter {
  switch (value) {
    case "ACTIVE":
    case "COMPLETED":
    case "FAILED":
    case "CANCELED":
      return value;
    default:
      return "";
  }
}

/**
 * ソートキーへ寄せる。未知値は更新日時。
 * @param value URL またはセレクトの文字列。
 * @returns 許可されたソートキー。
 */
function toSortBy(value: string): ExecutionSortBy {
  return value === "displayId" ? "displayId" : "updatedAt";
}

/**
 * ソート順へ寄せる。未知値は降順。
 * @param value URL またはセレクトの文字列。
 * @returns asc または desc。
 */
function toSortOrder(value: string): SortOrder {
  return value === "asc" ? "asc" : "desc";
}

/**
 * 実行一覧の取得、URL 更新、フィルタ適用を持つ。
 *
 * 画面は戻り値を描画する。loading / empty / error の出し分けとページング部品は呼び出し側に残す。
 *
 * @returns 描画用の状態とコマンド。
 */
export function useExecutionsPage(): ExecutionsPageModel {
  const { uiText } = useI18n();
  const { selectedProject, loaded: projectsLoaded } = useProject();
  const projectId = selectedProject?.projectId ?? null;
  const searchParams = useSearchParams();
  const router = useRouter();
  const [items, setItems] = useState<ExecutionDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [nameDraft, setNameDraft] = useState("");
  const [definitionDraft, setDefinitionDraft] = useState("");
  const [nameError, setNameError] = useState<string | null>(null);
  const [definitionError, setDefinitionError] = useState<string | null>(null);

  // 参照が毎レンダー変わっても、クエリ文字列が同じなら一覧を再取得しない。
  const searchParamsKey = searchParams.toString();
  const listQuery = useMemo(
    () => readListQuery(new URLSearchParams(searchParamsKey)),
    [searchParamsKey]
  );

  useEffect(() => {
    setNameDraft(listQuery.name ?? "");
    setDefinitionDraft(listQuery.definitionId ?? "");
  }, [listQuery.name, listQuery.definitionId]);

  const status = toStatusFilter(listQuery.status ?? "");
  const sortBy = toSortBy(listQuery.sort.sortBy ?? "");
  const sortOrder = toSortOrder(listQuery.sort.sortOrder ?? "");
  const currentPage = useMemo(
    () => Math.floor(listQuery.pagination.offset / listQuery.pagination.limit) + 1,
    [listQuery.pagination.offset, listQuery.pagination.limit]
  );
  const hasPrev = listQuery.pagination.offset > 0;
  const hasNext = totalCount !== null && listQuery.pagination.offset + (items?.length ?? 0) < totalCount;

  const reload = useCallback(async (): Promise<boolean> => {
    // projectId を付けずに呼ぶと所属 Project の和が返る。所属一覧の取得前は取得せず、選択が無ければ空にする。
    if (!projectsLoaded) {
      return false;
    }
    if (projectId === null) {
      setItems([]);
      setTotalCount(0);
      setToast(null);
      setLoading(false);
      return true;
    }
    setLoading(true);
    setToast(null);
    try {
      const path = buildExecutionsListPath({
        pagination: listQuery.pagination,
        sort: listQuery.sort,
        status: listQuery.status,
        name: listQuery.name,
        definitionId: listQuery.definitionId,
        projectId,
      });
      const page = await apiGet<PagedExecutions>(path);
      setItems(page.items);
      setTotalCount(page.totalCount);
      return true;
    } catch (error) {
      setToast(toToastError(error));
      setItems(null);
      setTotalCount(null);
      return false;
    } finally {
      setLoading(false);
    }
  }, [listQuery, projectId, projectsLoaded]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const goTo = useCallback(
    (query: ExecutionsListQuery) => {
      router.replace(buildExecutionsListPath(query), { scroll: false });
    },
    [router]
  );

  const submitFilters = useCallback(
    (event: FormEvent) => {
      event.preventDefault();
      // 無効な値は入力欄の下にエラーを出し、URL を変えない（要件 8.5）。
      const nextNameError = matchesPattern(nameDraft.trim(), SEARCH_NAME_PATTERN)
        ? null
        : uiText.executionsPage.filter.invalidName;
      const nextDefinitionError = matchesPattern(definitionDraft.trim(), DEFINITION_ID_PATTERN)
        ? null
        : uiText.executionsPage.filter.invalidDefinitionId;
      setNameError(nextNameError);
      setDefinitionError(nextDefinitionError);
      if (nextNameError !== null || nextDefinitionError !== null) {
        return;
      }
      goTo({
        pagination: { ...listQuery.pagination, offset: 0 },
        sort: listQuery.sort,
        status: status || undefined,
        name: nameDraft || undefined,
        definitionId: definitionDraft || undefined
      });
    },
    [definitionDraft, goTo, listQuery.pagination, listQuery.sort, nameDraft, status, uiText.executionsPage.filter.invalidDefinitionId, uiText.executionsPage.filter.invalidName]
  );

  const changeStatus = useCallback(
    (value: string) => {
      goTo({
        pagination: { ...listQuery.pagination, offset: 0 },
        sort: listQuery.sort,
        status: toStatusFilter(value) || undefined,
        name: nameDraft || undefined,
        definitionId: definitionDraft || undefined
      });
    },
    [definitionDraft, goTo, listQuery.pagination, listQuery.sort, nameDraft]
  );

  const changeSort = useCallback(
    (nextSortBy: ExecutionSortBy, nextSortOrder: SortOrder) => {
      goTo({
        ...listQuery,
        pagination: { ...listQuery.pagination, offset: 0 },
        sort: { sortBy: nextSortBy, sortOrder: nextSortOrder }
      });
    },
    [goTo, listQuery]
  );

  const clearFilters = useCallback(() => {
    setNameDraft("");
    setDefinitionDraft("");
    setNameError(null);
    setDefinitionError(null);
    goTo({
      pagination: { ...listQuery.pagination, offset: 0 },
      sort: listQuery.sort
    });
  }, [goTo, listQuery.pagination, listQuery.sort]);

  const clearDefinitionContext = useCallback(() => {
    setDefinitionDraft("");
    setDefinitionError(null);
    goTo({
      pagination: { ...listQuery.pagination, offset: 0 },
      sort: listQuery.sort,
      status: status || undefined,
      name: nameDraft || undefined
    });
  }, [goTo, listQuery.pagination, listQuery.sort, nameDraft, status]);

  const changeNameDraft = useCallback((value: string) => {
    setNameDraft(value);
    setNameError(null);
  }, []);

  const changeDefinitionDraft = useCallback((value: string) => {
    setDefinitionDraft(value);
    setDefinitionError(null);
  }, []);

  const clearName = useCallback(() => {
    setNameDraft("");
    setNameError(null);
    goTo({
      pagination: { ...listQuery.pagination, offset: 0 },
      sort: listQuery.sort,
      status: status || undefined,
      definitionId: listQuery.definitionId
    });
  }, [goTo, listQuery.definitionId, listQuery.pagination, listQuery.sort, status]);

  const goToPreviousPage = useCallback(() => {
    goTo({
      ...listQuery,
      pagination: {
        ...listQuery.pagination,
        offset: Math.max(0, listQuery.pagination.offset - listQuery.pagination.limit)
      }
    });
  }, [goTo, listQuery]);

  const goToNextPage = useCallback(() => {
    goTo({
      ...listQuery,
      pagination: {
        ...listQuery.pagination,
        offset: listQuery.pagination.offset + listQuery.pagination.limit
      }
    });
  }, [goTo, listQuery]);

  const notifyIngressAccepted = useCallback(() => {
    setToast({
      tone: "success",
      message: uiText.executionsPage.ingress.accepted
    });
  }, [uiText.executionsPage.ingress.accepted]);

  const dismissToast = useCallback(() => {
    setToast(null);
  }, []);

  return {
    items,
    totalCount,
    loading,
    toast,
    definitionId: listQuery.definitionId,
    name: listQuery.name,
    nameError,
    definitionError,
    status,
    sortBy,
    sortOrder,
    nameDraft,
    definitionDraft,
    limit: listQuery.pagination.limit,
    offset: listQuery.pagination.offset,
    currentPage,
    hasPrev,
    hasNext,
    setNameDraft: changeNameDraft,
    setDefinitionDraft: changeDefinitionDraft,
    submitFilters,
    changeStatus,
    changeSort,
    clearFilters,
    clearDefinitionContext,
    clearName,
    goToPreviousPage,
    goToNextPage,
    reload,
    notifyIngressAccepted,
    dismissToast
  };
}
