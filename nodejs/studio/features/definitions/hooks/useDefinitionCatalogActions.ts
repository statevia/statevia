"use client";

import { useCallback, useState } from "react";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { restoreDefinition, softDeleteDefinition } from "../api";

/** 行単位のインライン確認対象。 */
export type PendingConfirm =
  | { kind: "delete"; displayId: string }
  | { kind: "restore"; displayId: string };

/** useDefinitionCatalogActions のオプション。 */
export type UseDefinitionCatalogActionsOptions = {
  /** 削除・復元後に一覧を再取得する。 */
  reload: (options?: { clearToast?: boolean }) => Promise<void>;
  /** 成功・失敗トーストを親に伝える。 */
  setToast: (toast: ToastState | null) => void;
  /** 削除に成功したときのトースト文言。 */
  deletedMessage: string;
  /** 復元に成功したときのトースト文言。 */
  restoredMessage: string;
};

/** useDefinitionCatalogActions の戻り値。 */
export type UseDefinitionCatalogActionsResult = {
  /** 確認待ちの操作。無ければ null。 */
  pendingConfirm: PendingConfirm | null;
  /** 確認待ちの操作を設定する。null で取り消す。 */
  setPendingConfirm: (value: PendingConfirm | null) => void;
  /** 削除中の定義の表示用 ID。無ければ null。 */
  deletingId: string | null;
  /** 復元中の定義の表示用 ID。無ければ null。 */
  restoringId: string | null;
  /** 削除のボタンを押したときの処理。1 回目で確認待ちにし、同じ定義で 2 回目に削除する。 */
  handleDeleteClick: (displayId: string) => void;
  /** 復元のボタンを押したときの処理。1 回目で確認待ちにし、同じ定義で 2 回目に復元する。 */
  handleRestoreClick: (displayId: string) => void;
};

/**
 * 定義 catalog の論理削除・復元（インライン二段階確認付き）。
 * @param options 再取得・トースト文言
 */
export function useDefinitionCatalogActions(
  options: UseDefinitionCatalogActionsOptions,
): UseDefinitionCatalogActionsResult {
  const { reload, setToast, deletedMessage, restoredMessage } = options;
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [restoringId, setRestoringId] = useState<string | null>(null);

  const runDelete = useCallback(
    async (displayId: string) => {
      setDeletingId(displayId);
      setPendingConfirm(null);
      try {
        await softDeleteDefinition(displayId);
        setToast({ tone: "success", message: deletedMessage });
        await reload({ clearToast: false });
      } catch (error) {
        setToast(toToastError(error));
      } finally {
        setDeletingId(null);
      }
    },
    [deletedMessage, reload, setToast],
  );

  const runRestore = useCallback(
    async (displayId: string) => {
      setRestoringId(displayId);
      setPendingConfirm(null);
      try {
        await restoreDefinition(displayId);
        setToast({ tone: "success", message: restoredMessage });
        await reload({ clearToast: false });
      } catch (error) {
        setToast(toToastError(error));
      } finally {
        setRestoringId(null);
      }
    },
    [reload, restoredMessage, setToast],
  );

  /**
   * 削除のインライン二段階確認を進める。
   * @param displayId 対象定義の displayId
   */
  const handleDeleteClick = useCallback(
    (displayId: string) => {
      if (pendingConfirm?.kind === "delete" && pendingConfirm.displayId === displayId) {
        void runDelete(displayId);
        return;
      }
      setPendingConfirm({ kind: "delete", displayId });
    },
    [pendingConfirm, runDelete],
  );

  /**
   * 復元のインライン二段階確認を進める。
   * @param displayId 対象定義の displayId
   */
  const handleRestoreClick = useCallback(
    (displayId: string) => {
      if (pendingConfirm?.kind === "restore" && pendingConfirm.displayId === displayId) {
        void runRestore(displayId);
        return;
      }
      setPendingConfirm({ kind: "restore", displayId });
    },
    [pendingConfirm, runRestore],
  );

  return {
    pendingConfirm,
    setPendingConfirm,
    deletingId,
    restoringId,
    handleDeleteClick,
    handleRestoreClick,
  };
}
