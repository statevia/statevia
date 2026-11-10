"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { useDelayedVisibility } from "@/shared/lib/useDelayedVisibility";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { getDefinition, softDeleteDefinition } from "../api";
import { isApiNotFound } from "@/shared/api/httpStatus";
import type { DefinitionDTO } from "../types";

/** useDefinitionDetail のオプション。 */
export type UseDefinitionDetailOptions = {
  /** 削除成功時のトースト文言。 */
  deletedMessage: string;
  /** 削除成功後の遷移先（既定 `/definitions`）。 */
  afterDeleteHref?: string;
};

/** useDefinitionDetail の戻り値。 */
export type UseDefinitionDetailResult = {
  /** 取得した定義。未取得なら null。 */
  row: DefinitionDTO | null;
  /** 未所属などで 404 のとき true。 */
  notFound: boolean;
  /** 取得中か。 */
  loading: boolean;
  /** ローディング表示を出すか（取得が少し長引いたときだけ true）。 */
  showLoading: boolean;
  /** 表示中のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを設定する。null で消す。 */
  setToast: (toast: ToastState | null) => void;
  /** 削除の確認待ちか。 */
  confirmDelete: boolean;
  /** 削除の確認待ちを設定する。 */
  setConfirmDelete: (value: boolean) => void;
  /** 削除中か。 */
  deleting: boolean;
  /** 定義を取得し直す。 */
  load: () => Promise<void>;
  /** 削除のボタンを押したときの処理。1 回目で確認待ちにし、2 回目に削除して一覧へ移る。 */
  handleDeleteClick: () => Promise<void>;
};

/**
 * 定義詳細の取得と論理削除を管理する。
 * @param definitionId 対象 displayId
 * @param options 文言・遷移先
 */
export function useDefinitionDetail(
  definitionId: string,
  options: UseDefinitionDetailOptions,
): UseDefinitionDetailResult {
  const { deletedMessage, afterDeleteHref = "/definitions" } = options;
  const router = useRouter();
  const [row, setRow] = useState<DefinitionDTO | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [loading, setLoading] = useState(true);
  const showLoading = useDelayedVisibility(loading);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const definition = await getDefinition(definitionId);
      setNotFound(false);
      setRow(definition);
    } catch (error) {
      setNotFound(isApiNotFound(error));
      setToast(toToastError(error));
      setRow(null);
    } finally {
      setLoading(false);
    }
  }, [definitionId]);

  useEffect(() => {
    void load();
  }, [load]);

  /**
   * 論理削除のインライン二段階確認を進める。
   * UI 側は戻り Promise を `.catch` で扱う（エラー表示は本関数内で完結）。
   */
  const handleDeleteClick = useCallback(async () => {
    if (!confirmDelete) {
      setConfirmDelete(true);
      return;
    }
    setDeleting(true);
    setConfirmDelete(false);
    try {
      await softDeleteDefinition(definitionId);
      setToast({ tone: "success", message: deletedMessage });
      router.push(afterDeleteHref);
    } catch (error) {
      setToast(toToastError(error));
      setDeleting(false);
    }
  }, [afterDeleteHref, confirmDelete, definitionId, deletedMessage, router]);

  return {
    row,
    notFound,
    loading,
    showLoading,
    toast,
    setToast,
    confirmDelete,
    setConfirmDelete,
    deleting,
    load,
    handleDeleteClick,
  };
}
