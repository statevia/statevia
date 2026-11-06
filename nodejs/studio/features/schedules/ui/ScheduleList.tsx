"use client";

import Link from "next/link";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import type { SchedulesPageModel } from "../hooks/useSchedulesPage";
import type { ScheduleListItem } from "../types";
import { ScheduleWriteErrorMessage } from "./ScheduleWriteErrorMessage";

type ScheduleListProps = {
  /** 表示する行。空や失敗は呼び出し側が出す。 */
  schedules: readonly ScheduleListItem[];
  /** 一覧の状態と操作。 */
  page: SchedulesPageModel;
};

type ScheduleRowActionsProps = {
  /** 対象の行。 */
  schedule: ScheduleListItem;
  /** 一覧の状態と操作。 */
  page: SchedulesPageModel;
};

/** 補助ボタンの見た目。 */
const SECONDARY_BUTTON_CLASS =
  "rounded-lg border border-md-outline px-3 py-1.5 text-sm hover:bg-md-surface-container disabled:opacity-60";

/** 確定ボタンの見た目。 */
const CONFIRM_BUTTON_CLASS =
  "rounded-lg border border-red-700 bg-red-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60";

/**
 * 行の操作。削除と手動実行は確認を挟み、確定で 1 回だけ送る。無効な行には手動実行を出さない。
 * @param props 行と一覧の状態。
 * @returns 操作ボタン群。
 */
function ScheduleRowActions({ schedule, page }: Readonly<ScheduleRowActionsProps>) {
  const text = useI18n().uiText.schedules.actions;
  const pendingAction = page.pending?.scheduleId === schedule.scheduleId ? page.pending.action : null;
  const busy = page.busyScheduleId === schedule.scheduleId;

  if (pendingAction !== null) {
    const isDelete = pendingAction === "delete";
    return (
      <div className="flex flex-col gap-2">
        {isDelete ? null : <p className="text-xs text-md-on-surface-variant">{text.runConfirmNote}</p>}
        <div className="flex flex-wrap gap-2">
          <button type="button" className={CONFIRM_BUTTON_CLASS} disabled={busy} onClick={() => void page.confirm()}>
            {confirmLabel(isDelete, busy, text)}
          </button>
          <button type="button" className={SECONDARY_BUTTON_CLASS} disabled={busy} onClick={page.cancelConfirm}>
            {text.cancel}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-wrap gap-2">
      <Link href={`/schedules/${encodeURIComponent(schedule.scheduleId)}`} className={SECONDARY_BUTTON_CLASS}>
        {text.edit}
      </Link>
      {schedule.enabled ? (
        <button
          type="button"
          className={SECONDARY_BUTTON_CLASS}
          disabled={page.busyScheduleId !== null}
          onClick={() => page.requestConfirm(schedule.scheduleId, "run")}
        >
          {text.run}
        </button>
      ) : null}
      <button
        type="button"
        className={SECONDARY_BUTTON_CLASS}
        disabled={page.busyScheduleId !== null}
        onClick={() => page.requestConfirm(schedule.scheduleId, "delete")}
      >
        {text.delete}
      </button>
    </div>
  );
}

/**
 * 確定ボタンの文言。
 * @param isDelete 削除の確認か。
 * @param busy 送信中か。
 * @param text 操作の辞書。
 * @returns 文言。
 */
function confirmLabel(
  isDelete: boolean,
  busy: boolean,
  text: ReturnType<typeof useI18n>["uiText"]["schedules"]["actions"]
): string {
  if (isDelete) return busy ? text.deleting : text.confirmDelete;
  return busy ? text.running : text.confirmRun;
}

/**
 * 定期実行の一覧。`input` は出さない（一覧の型に無い）。
 * 定義は定義一覧で名前を引き、引けなければ UUID をそのまま出す。
 * @param props 行と一覧の状態。
 * @returns 一覧。
 */
export function ScheduleList({ schedules, page }: Readonly<ScheduleListProps>) {
  const { locale, uiText } = useI18n();
  const text = uiText.schedules;
  const dateTimeLocale = getDateTimeLocale(locale);

  return (
    <ul className="divide-y divide-md-outline rounded-xl border border-md-outline bg-md-surface">
      {schedules.map((schedule) => (
        <li key={schedule.scheduleId} className="flex flex-col gap-2 px-4 py-3 text-sm">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0 space-y-0.5">
              <p className="font-medium text-md-on-surface">{schedule.name}</p>
              <p className="text-xs text-md-on-surface-variant">
                {text.columns.definition}:{" "}
                {page.definitionNames?.get(schedule.definitionId) ?? (
                  <span className="font-mono">{schedule.definitionId}</span>
                )}
              </p>
              <p className="text-xs text-md-on-surface-variant">
                {text.columns.cron}: <span className="font-mono">{schedule.cronExpression}</span>
                {` · ${text.columns.timeZone}: ${schedule.timeZone}`}
              </p>
              <p className="text-xs text-md-on-surface-variant">
                {`${text.columns.overlap}: ${text.overlap[schedule.overlapPolicy]}`}
              </p>
              <p className="text-xs text-md-on-surface-variant">
                {`${text.columns.status}: ${schedule.enabled ? text.enabled : text.disabled}`}
                {` · ${text.columns.nextFireAt}: ${formatDateTimeLocalized(schedule.nextFireAt, dateTimeLocale)}`}
              </p>
            </div>
            <ScheduleRowActions schedule={schedule} page={page} />
          </div>
          {page.runStarted?.scheduleId === schedule.scheduleId ? (
            <output className="text-sm text-emerald-800">
              <Link href={`/executions/${encodeURIComponent(page.runStarted.displayId)}`} className="underline">
                {text.actions.runStarted(page.runStarted.displayId)}
              </Link>
            </output>
          ) : null}
          {page.rowFailure?.scheduleId === schedule.scheduleId ? (
            <ScheduleWriteErrorMessage
              failure={page.rowFailure.failure}
              fallbackMessage={page.rowFailure.fallbackMessage}
            />
          ) : null}
        </li>
      ))}
    </ul>
  );
}
