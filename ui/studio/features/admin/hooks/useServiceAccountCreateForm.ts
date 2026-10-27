"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { apiGet } from "@/shared/api";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { buildCreateServiceAccountBody, type ServiceAccountFieldErrors } from "../serviceAccounts";
import { createServiceAccount } from "../serviceAccountsApi";
import type { AdminGroupListItem, AdminServiceAccount } from "../types";

/**
 * 作成成功後に呼び出し側が行う後処理（一覧の再取得など）。
 * @param serviceAccount 作成した ServiceAccount。
 */
export type ServiceAccountCreatedHandler = (serviceAccount: AdminServiceAccount) => Promise<void>;

/** ServiceAccount 作成フォームの描画用状態。ReactNode は含まない。 */
export type ServiceAccountCreateFormModel = {
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
  /** 送信中か。 */
  submitting: boolean;
  /** 成功通知。無ければ null。 */
  toast: ToastState | null;
  /** 成功通知を閉じる。 */
  clearToast: () => void;
  /**
   * 検証して作成する。契約外なら送らない。
   * @param event フォームの submit。
   */
  submit: (event: FormEvent<HTMLFormElement>) => Promise<void>;
};

/**
 * ServiceAccount 作成フォームの入力、グループ一覧の読込、検証、送信。
 * 本文は `name` と `groupIds` だけで、API キーは扱わない。送信中は二重送信しない。
 * 失敗時は自動で再送せず、入力を残す（201 のあとに再送すると重複して作られるため）。
 * @param onCreated 作成成功後の後処理。
 * @returns フォームの状態と操作。
 */
export function useServiceAccountCreateForm(onCreated: ServiceAccountCreatedHandler): ServiceAccountCreateFormModel {
  const { locale, uiText } = useI18n();
  const text = uiText.admin.serviceAccounts;
  const [name, setName] = useState("");
  const [groups, setGroups] = useState<AdminGroupListItem[] | null>(null);
  const [groupsLoadFailed, setGroupsLoadFailed] = useState(false);
  const [selectedGroupIds, setSelectedGroupIds] = useState<ReadonlySet<string>>(new Set());
  const [fieldErrors, setFieldErrors] = useState<ServiceAccountFieldErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const [toast, setToast] = useState<ToastState | null>(null);

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
      if (submittingRef.current) return;

      const result = buildCreateServiceAccountBody({ name, groupIds: [...selectedGroupIds] });
      if (!result.ok) {
        setFieldErrors(result.errors);
        setSubmitError(null);
        return;
      }

      submittingRef.current = true;
      setSubmitting(true);
      setFieldErrors({});
      setSubmitError(null);
      setToast(null);
      try {
        const created = await createServiceAccount(result.body);
        setName("");
        setSelectedGroupIds(new Set());
        setToast({ tone: "success", message: text.created(created.name) });
        await onCreated(created);
      } catch (error) {
        setSubmitError(toToastError(error, locale).message);
      } finally {
        submittingRef.current = false;
        setSubmitting(false);
      }
    },
    [locale, name, onCreated, selectedGroupIds, text]
  );

  return {
    name,
    setName,
    groups,
    groupsLoadFailed,
    selectedGroupIds,
    toggleGroup,
    fieldErrors,
    submitError,
    submitting,
    toast,
    clearToast: () => setToast(null),
    submit
  };
}
