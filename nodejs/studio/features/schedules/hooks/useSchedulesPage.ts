"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { classifyScheduleWriteError, type ScheduleWriteError } from "../scheduleErrors";
import { deleteSchedule, listScheduleDefinitionOptions, listSchedules, runSchedule } from "../schedulesApi";
import type { ScheduleListItem } from "../types";

/** 確認待ちの操作。確定するまで API は呼ばない。 */
export type SchedulePendingAction = Readonly<{ scheduleId: string; action: "delete" | "run" }>;

/** 定期実行の操作の失敗。`fallbackMessage` は分類が `other` のときに出す。 */
export type ScheduleRowFailure = Readonly<{
  scheduleId: string;
  failure: ScheduleWriteError;
  fallbackMessage: string;
}>;

/** 手動実行で開始した実行。定期実行の下にリンクを出す。 */
export type ScheduleRunStarted = Readonly<{ scheduleId: string; displayId: string }>;

/** 定期実行一覧の描画用状態。ReactNode は含まない。 */
export type SchedulesPageModel = {
  /** 取得済みの一覧。失敗時は null（403 / 通信失敗を空一覧に見せない）。 */
  schedules: ScheduleListItem[] | null;
  /** 一覧の取得中か。 */
  loading: boolean;
  /** 定義 UUID → 定義名。読めなかったときは null（UUID をそのまま出す）。 */
  definitionNames: ReadonlyMap<string, string> | null;
  /** 一覧を取り直す。 */
  reload: () => Promise<void>;
  /** 確認待ちの操作。無ければ null。 */
  pending: SchedulePendingAction | null;
  /**
   * 操作の確認を始める。無効な定期実行の手動実行は受け付けない。
   * @param scheduleId 対象の定期実行。
   * @param action 削除か手動実行。
   */
  requestConfirm: (scheduleId: string, action: SchedulePendingAction["action"]) => void;
  /** 確認をやめる。 */
  cancelConfirm: () => void;
  /** 確認中の操作を 1 回だけ送る。送信中の連打は無視する。 */
  confirm: () => Promise<void>;
  /** 送信中の定期実行。無ければ null。 */
  busyScheduleId: string | null;
  /** 直近の手動実行の結果。 */
  runStarted: ScheduleRunStarted | null;
  /** 直近の定期実行の操作の失敗。 */
  rowFailure: ScheduleRowFailure | null;
  /** 成功通知。 */
  toast: ToastState | null;
  /** 成功通知を閉じる。 */
  clearToast: () => void;
};

/**
 * 定期実行一覧の読込と、定期実行の削除・手動実行。
 * 一覧は `GET /v1/schedules` を 1 回だけ読み、定期実行ごとの単票 GET はしない（`input` を取りに行かない）。
 * 定義名は定義一覧を 1 回読んで引く。読めなくても一覧は出す。
 * @returns 描画用の状態と操作。
 */
export function useSchedulesPage(): SchedulesPageModel {
  const { locale, uiText } = useI18n();
  const text = uiText.schedules;
  const [schedules, setSchedules] = useState<ScheduleListItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [definitionNames, setDefinitionNames] = useState<ReadonlyMap<string, string> | null>(null);
  const [pending, setPending] = useState<SchedulePendingAction | null>(null);
  const [busyScheduleId, setBusyScheduleId] = useState<string | null>(null);
  const busyRef = useRef(false);
  const [runStarted, setRunStarted] = useState<ScheduleRunStarted | null>(null);
  const [rowFailure, setRowFailure] = useState<ScheduleRowFailure | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    const [scheduleResult, definitionResult] = await Promise.allSettled([
      listSchedules(),
      listScheduleDefinitionOptions()
    ]);
    setSchedules(scheduleResult.status === "fulfilled" ? scheduleResult.value : null);
    setDefinitionNames(
      definitionResult.status === "fulfilled"
        ? new Map(definitionResult.value.options.map((option) => [option.resourceId, option.name]))
        : null
    );
    setLoading(false);
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  const requestConfirm = useCallback(
    (scheduleId: string, action: SchedulePendingAction["action"]) => {
      const row = schedules?.find((schedule) => schedule.scheduleId === scheduleId);
      if (row === undefined || (action === "run" && !row.enabled)) return;
      setPending({ scheduleId, action });
      setRowFailure(null);
      setRunStarted(null);
    },
    [schedules]
  );

  const confirm = useCallback(async () => {
    if (busyRef.current || pending === null) return;
    const row = schedules?.find((schedule) => schedule.scheduleId === pending.scheduleId);
    if (row === undefined) return;

    busyRef.current = true;
    setBusyScheduleId(row.scheduleId);
    setToast(null);
    try {
      switch (pending.action) {
        case "delete":
          await deleteSchedule(row.scheduleId);
          setToast({ tone: "success", message: text.actions.deleted(row.name) });
          setPending(null);
          await reload();
          break;
        case "run": {
          const result = await runSchedule(row.scheduleId);
          setRunStarted({ scheduleId: row.scheduleId, displayId: result.displayId });
          setPending(null);
          break;
        }
      }
    } catch (error) {
      setPending(null);
      setRowFailure({
        scheduleId: row.scheduleId,
        failure: classifyScheduleWriteError(error, false),
        fallbackMessage: toToastError(error, locale).message
      });
    } finally {
      busyRef.current = false;
      setBusyScheduleId(null);
    }
  }, [locale, pending, reload, schedules, text]);

  return {
    schedules,
    loading,
    definitionNames,
    reload,
    pending,
    requestConfirm,
    cancelConfirm: () => setPending(null),
    confirm,
    busyScheduleId,
    runStarted,
    rowFailure,
    toast,
    clearToast: () => setToast(null)
  };
}
