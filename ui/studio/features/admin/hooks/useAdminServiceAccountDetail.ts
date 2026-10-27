"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { isApiNotFound } from "@/features/projects/httpStatus";
import { apiGet } from "@/shared/api";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { buildUpdateServiceAccountBody, type ServiceAccountFieldErrors } from "../serviceAccounts";
import { getServiceAccount, updateServiceAccount } from "../serviceAccountsApi";
import type { AdminGroupListItem, AdminServiceAccount } from "../types";

/**
 * 詳細の読込状態。
 * - `notFound`: 404（存在しない ID や他テナント）。一覧へ戻す
 * - `error`: それ以外の失敗。再試行できる
 */
export type ServiceAccountDetailLoadState = "loading" | "ready" | "notFound" | "error";

/** ServiceAccount 詳細の描画用状態。ReactNode は含まない。 */
export type AdminServiceAccountDetailModel = {
  /** 読込状態。 */
  loadState: ServiceAccountDetailLoadState;
  /** 最後に読んだ（または保存した）現在値。`ready` 以外は null。 */
  serviceAccount: AdminServiceAccount | null;
  /** 詳細を読み直す。 */
  reload: () => Promise<void>;
  /** 有効のチェック状態。 */
  isActive: boolean;
  /**
   * 有効を切り替える。
   * @param value チェック状態。
   */
  setIsActive: (value: boolean) => void;
  /** 名前の入力値。 */
  name: string;
  /**
   * 名前を更新する。
   * @param value 入力値。
   */
  setName: (value: string) => void;
  /** 選べるグループ。読込前と失敗時は null。 */
  groups: AdminGroupListItem[] | null;
  /** グループ一覧の取得に失敗したか。 */
  groupsLoadFailed: boolean;
  /** 選択中のグループ ID。 */
  selectedGroupIds: ReadonlySet<string>;
  /**
   * グループの選択を切り替える。
   * @param groupId グループ ID。
   */
  toggleGroup: (groupId: string) => void;
  /** 送信前に止めた項目エラー。 */
  fieldErrors: ServiceAccountFieldErrors;
  /** サーバーが返したエラー（422 など）。フォームの値は残す。無ければ null。 */
  submitError: string | null;
  /** 変更が無く送らなかったか。 */
  unchanged: boolean;
  /** 保存中か。 */
  saving: boolean;
  /** 成功通知。無ければ null。 */
  toast: ToastState | null;
  /** 成功通知を閉じる。 */
  clearToast: () => void;
  /**
   * 変えた項目だけを PATCH する。変更が無い・契約外なら送らない。
   * @param event フォームの submit。
   */
  submit: (event: FormEvent<HTMLFormElement>) => Promise<void>;
};

/**
 * ServiceAccount 詳細の読込と部分更新。
 * 削除は持たない（API に無い）。止めるときは無効化する。
 * グループ一覧を読めなくても、有効と名前は編集できる（所属は現在値のまま送らない）。
 * @param serviceAccountId 対象の ServiceAccount ID。
 * @returns 描画用の状態と操作。
 */
export function useAdminServiceAccountDetail(serviceAccountId: string): AdminServiceAccountDetailModel {
  const { locale, uiText } = useI18n();
  const text = uiText.admin.serviceAccounts;
  const [loadState, setLoadState] = useState<ServiceAccountDetailLoadState>("loading");
  const [serviceAccount, setServiceAccount] = useState<AdminServiceAccount | null>(null);
  const [isActive, setIsActive] = useState(false);
  const [name, setName] = useState("");
  const [selectedGroupIds, setSelectedGroupIds] = useState<ReadonlySet<string>>(new Set());
  const [groups, setGroups] = useState<AdminGroupListItem[] | null>(null);
  const [groupsLoadFailed, setGroupsLoadFailed] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<ServiceAccountFieldErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [unchanged, setUnchanged] = useState(false);
  const [saving, setSaving] = useState(false);
  const savingRef = useRef(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * フォームを現在値に戻す。
   * @param current 現在値。
   */
  const resetForm = useCallback((current: AdminServiceAccount) => {
    setServiceAccount(current);
    setIsActive(current.isActive);
    setName(current.name);
    setSelectedGroupIds(new Set(current.groupIds));
    setFieldErrors({});
    setSubmitError(null);
    setUnchanged(false);
  }, []);

  const reload = useCallback(async () => {
    setLoadState("loading");
    try {
      resetForm(await getServiceAccount(serviceAccountId));
      setLoadState("ready");
    } catch (error) {
      setServiceAccount(null);
      setLoadState(isApiNotFound(error) ? "notFound" : "error");
    }
  }, [resetForm, serviceAccountId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const rows = await apiGet<AdminGroupListItem[]>("/admin/groups");
        if (!cancelled) setGroups(rows);
      } catch {
        if (!cancelled) setGroupsLoadFailed(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const toggleGroup = useCallback((groupId: string) => {
    setSelectedGroupIds((current) => {
      const next = new Set(current);
      if (next.has(groupId)) {
        next.delete(groupId);
      } else {
        next.add(groupId);
      }
      return next;
    });
  }, []);

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (savingRef.current || serviceAccount === null) return;

      const result = buildUpdateServiceAccountBody(serviceAccount, {
        isActive,
        name,
        groupIds: [...selectedGroupIds]
      });
      switch (result.kind) {
        case "invalid":
          setFieldErrors(result.errors);
          setSubmitError(null);
          setUnchanged(false);
          return;
        case "unchanged":
          setFieldErrors({});
          setSubmitError(null);
          setUnchanged(true);
          return;
        case "ready":
          break;
      }

      savingRef.current = true;
      setSaving(true);
      setFieldErrors({});
      setSubmitError(null);
      setUnchanged(false);
      setToast(null);
      try {
        resetForm(await updateServiceAccount(serviceAccount.serviceAccountId, result.body));
        setToast({ tone: "success", message: text.saved });
      } catch (error) {
        setSubmitError(toToastError(error, locale).message);
      } finally {
        savingRef.current = false;
        setSaving(false);
      }
    },
    [isActive, locale, name, resetForm, selectedGroupIds, serviceAccount, text]
  );

  return {
    loadState,
    serviceAccount,
    reload,
    isActive,
    setIsActive,
    name,
    setName,
    groups,
    groupsLoadFailed,
    selectedGroupIds,
    toggleGroup,
    fieldErrors,
    submitError,
    unchanged,
    saving,
    toast,
    clearToast: () => setToast(null),
    submit
  };
}
