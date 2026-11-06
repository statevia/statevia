"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { classifyScheduleReadError, classifyScheduleWriteError, type ScheduleWriteError } from "../scheduleErrors";
import {
  buildCreateScheduleBody,
  buildUpdateScheduleBody,
  EMPTY_SCHEDULE_FORM,
  toScheduleFormValues,
  type CreateScheduleBody,
  type ScheduleFieldErrors,
  type ScheduleFormValues,
  type UpdateScheduleBody
} from "../schedules";
import {
  createSchedule,
  getSchedule,
  listRunAsCandidates,
  listScheduleDefinitionOptions,
  updateSchedule
} from "../schedulesApi";
import type { RunAsCandidate, ScheduleDefinitionOptionPage, ScheduleDetail } from "../types";

/**
 * フォームの読込状態。作成は最初から `ready`。
 * - `notFound`: 単票の 404（不在・所属外）
 * - `forbidden`: 単票の 403（呼び出し元の実行リソース許可の外）
 * - `error`: それ以外の失敗。再試行できる
 */
export type ScheduleFormLoadState = "loading" | "ready" | "notFound" | "forbidden" | "error";

/**
 * 保存の失敗。`fallbackMessage` は分類が `other` のときに出す。
 * `definitionChanged` は定義を変えた PATCH か（404 / 403 を定義欄に出す）。
 */
export type ScheduleSubmitFailure = Readonly<{
  failure: ScheduleWriteError;
  fallbackMessage: string;
  definitionChanged: boolean;
}>;

/** 作成・編集フォームの描画用状態。ReactNode は含まない。 */
export type ScheduleFormModel = {
  /** 編集なら true。 */
  isEdit: boolean;
  /** 読込状態。 */
  loadState: ScheduleFormLoadState;
  /** 単票を読み直す。 */
  reload: () => Promise<void>;
  /** 編集中の現在値。作成では null。 */
  current: ScheduleDetail | null;
  /** フォームの入力。 */
  values: ScheduleFormValues;
  /**
   * 1 項目を更新する。
   * @param key 項目。
   * @param value 値。
   */
  setValue: <K extends keyof ScheduleFormValues>(key: K, value: ScheduleFormValues[K]) => void;
  /** run-as の候補（有効な ServiceAccount）。読めなかったときは null。 */
  runAsCandidates: RunAsCandidate[] | null;
  /** 定義の候補。読めなかったときは null。 */
  definitionOptions: ScheduleDefinitionOptionPage | null;
  /** 送信前に止めた項目エラー。 */
  fieldErrors: ScheduleFieldErrors;
  /** API が返した失敗。無ければ null。 */
  submitFailure: ScheduleSubmitFailure | null;
  /** 変更が無く送らなかったか。 */
  unchanged: boolean;
  /** 保存中か。 */
  saving: boolean;
  /** 成功通知。 */
  toast: ToastState | null;
  /** 成功通知を閉じる。 */
  clearToast: () => void;
  /**
   * 作成は全必須項目を POST して一覧へ戻る。編集は変えた項目だけを PATCH する。
   * @param event フォームの submit。
   */
  submit: (event: FormEvent<HTMLFormElement>) => Promise<void>;
};

/**
 * 定期実行の作成・編集フォーム。
 * run-as の候補は `GET /v1/admin/service-accounts` の有効な行だけ。定義の Project のメンバーには絞らず、所属の不足は保存時の 422 で出す。
 * 一覧が 403 でも Principal ID の手入力へは落とさない。
 * @param scheduleId 編集する ID。省略時は作成。
 * @returns 描画用の状態と操作。
 */
export function useScheduleForm(scheduleId?: string): ScheduleFormModel {
  const router = useRouter();
  const { locale, uiText } = useI18n();
  const text = uiText.schedules.form;
  const isEdit = scheduleId !== undefined;
  const [loadState, setLoadState] = useState<ScheduleFormLoadState>(isEdit ? "loading" : "ready");
  const [current, setCurrent] = useState<ScheduleDetail | null>(null);
  const [values, setValues] = useState<ScheduleFormValues>(EMPTY_SCHEDULE_FORM);
  const [runAsCandidates, setRunAsCandidates] = useState<RunAsCandidate[] | null>(null);
  const [definitionOptions, setDefinitionOptions] = useState<ScheduleDefinitionOptionPage | null>(null);
  const [fieldErrors, setFieldErrors] = useState<ScheduleFieldErrors>({});
  const [submitFailure, setSubmitFailure] = useState<ScheduleSubmitFailure | null>(null);
  const [unchanged, setUnchanged] = useState(false);
  const [saving, setSaving] = useState(false);
  const savingRef = useRef(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * フォームを現在値に戻す。
   * @param detail 現在値。
   */
  const resetForm = useCallback((detail: ScheduleDetail) => {
    setCurrent(detail);
    setValues(toScheduleFormValues(detail));
    setFieldErrors({});
    setSubmitFailure(null);
    setUnchanged(false);
  }, []);

  const reload = useCallback(async () => {
    if (scheduleId === undefined) return;
    setLoadState("loading");
    try {
      resetForm(await getSchedule(scheduleId));
      setLoadState("ready");
    } catch (error) {
      setCurrent(null);
      setLoadState(classifyScheduleReadError(error));
    }
  }, [resetForm, scheduleId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    let cancelled = false;
    void Promise.allSettled([listRunAsCandidates(), listScheduleDefinitionOptions()]).then(
      ([runAsResult, definitionResult]) => {
        if (cancelled) return;
        setRunAsCandidates(runAsResult.status === "fulfilled" ? runAsResult.value : null);
        setDefinitionOptions(definitionResult.status === "fulfilled" ? definitionResult.value : null);
      }
    );
    return () => {
      cancelled = true;
    };
  }, []);

  const setValue = useCallback(<K extends keyof ScheduleFormValues>(key: K, value: ScheduleFormValues[K]) => {
    setValues((previous) => ({ ...previous, [key]: value }));
  }, []);

  /**
   * 送信前の状態をそろえる。
   * @param errors 項目エラー。
   * @param isUnchanged 変更なしで止めたか。
   */
  const settleBeforeSend = useCallback((errors: ScheduleFieldErrors, isUnchanged: boolean) => {
    setFieldErrors(errors);
    setSubmitFailure(null);
    setUnchanged(isUnchanged);
  }, []);

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      // 編集で単票を読む前（current が null）に送ると作成として扱ってしまうため止める。
      if (savingRef.current || (isEdit && current === null)) return;

      const send = prepareSend(current, values);
      switch (send.kind) {
        case "invalid":
          settleBeforeSend(send.errors, false);
          return;
        case "unchanged":
          settleBeforeSend({}, true);
          return;
        case "create":
        case "update":
          break;
      }

      savingRef.current = true;
      setSaving(true);
      settleBeforeSend({}, false);
      setToast(null);
      try {
        if (send.kind === "create") {
          await createSchedule(send.body);
          router.push("/schedules");
        } else {
          resetForm(await updateSchedule(send.scheduleId, send.body));
          setToast({ tone: "success", message: text.saved });
        }
      } catch (error) {
        setSubmitFailure({
          failure: classifyScheduleWriteError(error, send.enabledAfterSave),
          fallbackMessage: toToastError(error, locale).message,
          definitionChanged: send.definitionChanged
        });
      } finally {
        savingRef.current = false;
        setSaving(false);
      }
    },
    [current, isEdit, locale, resetForm, router, settleBeforeSend, text, values]
  );

  return {
    isEdit,
    loadState,
    reload,
    current,
    values,
    setValue,
    runAsCandidates,
    definitionOptions,
    fieldErrors,
    submitFailure,
    unchanged,
    saving,
    toast,
    clearToast: () => setToast(null),
    submit
  };
}

/** 送信の準備結果。 */
type PreparedSend =
  | { kind: "invalid"; errors: ScheduleFieldErrors }
  | { kind: "unchanged" }
  | {
      kind: "create";
      body: CreateScheduleBody;
      enabledAfterSave: boolean;
      definitionChanged: false;
    }
  | {
      kind: "update";
      scheduleId: string;
      body: UpdateScheduleBody;
      enabledAfterSave: boolean;
      definitionChanged: boolean;
    };

/**
 * 作成か部分更新の本文を作る。
 * @param current 編集中の現在値。作成では null。
 * @param values フォームの入力。
 * @returns 送信の準備結果。
 */
function prepareSend(current: ScheduleDetail | null, values: ScheduleFormValues): PreparedSend {
  if (current === null) {
    const result = buildCreateScheduleBody(values);
    return result.ok
      ? { kind: "create", body: result.body, enabledAfterSave: result.body.enabled, definitionChanged: false }
      : { kind: "invalid", errors: result.errors };
  }
  const result = buildUpdateScheduleBody(current, values);
  switch (result.kind) {
    case "invalid":
      return { kind: "invalid", errors: result.errors };
    case "unchanged":
      return { kind: "unchanged" };
    case "ready":
      return {
        kind: "update",
        scheduleId: current.scheduleId,
        body: result.body,
        enabledAfterSave: result.body.enabled ?? current.enabled,
        definitionChanged: result.body.definitionId !== undefined
      };
  }
}
