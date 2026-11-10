"use client";

import { useCallback, useEffect, useState } from "react";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { listDefinitions, type DefinitionsListQuery } from "../api";
import type { DefinitionDTO } from "../types";

/** useDefinitionsList の戻り値。 */
export type UseDefinitionsListResult = {
  /** ページ内の定義。未取得なら null。 */
  items: DefinitionDTO[] | null;
  /** 絞り込み後の総件数。未取得なら null。 */
  totalCount: number | null;
  /** 取得中か。 */
  loading: boolean;
  /** 表示中のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを設定する。null で消す。 */
  setToast: (toast: ToastState | null) => void;
  /** 定義の一覧を取得し直す。 */
  loadDefinitions: (options?: { clearToast?: boolean }) => Promise<void>;
  /** 前のページがあるか。 */
  hasPrev: boolean;
  /** 次のページがあるか。 */
  hasNext: boolean;
  /** 取得し終えて 1 件も無いか。 */
  empty: boolean;
};

/**
 * 定義一覧の取得状態を管理する。
 * @param listQuery URL 由来の一覧クエリ
 */
export function useDefinitionsList(listQuery: DefinitionsListQuery): UseDefinitionsListResult {
  const [items, setItems] = useState<DefinitionDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * 定義一覧を再取得する。
   * @param options.clearToast 取得開始時にトーストを消すか（既定 true）
   */
  const loadDefinitions = useCallback(
    async (options?: { clearToast?: boolean }) => {
      setLoading(true);
      if (options?.clearToast !== false) {
        setToast(null);
      }
      try {
        const page = await listDefinitions(listQuery);
        setItems(page.items);
        setTotalCount(page.totalCount);
      } catch (error) {
        setToast(toToastError(error));
        setItems(null);
        setTotalCount(null);
      } finally {
        setLoading(false);
      }
    },
    [listQuery],
  );

  useEffect(() => {
    void loadDefinitions({ clearToast: true });
  }, [loadDefinitions]);

  const hasPrev = listQuery.pagination.offset > 0;
  const hasNext =
    totalCount !== null && listQuery.pagination.offset + (items?.length ?? 0) < totalCount;
  const empty = !loading && items !== null && items.length === 0;

  return {
    items,
    totalCount,
    loading,
    toast,
    setToast,
    loadDefinitions,
    hasPrev,
    hasNext,
    empty,
  };
}
