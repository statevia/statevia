"use client";

import Link from "next/link";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { Button, buttonClassName } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { Notice } from "@/shared/ui/Notice";
import type { SchedulesPageModel } from "../hooks/useSchedulesPage";
import type { ScheduleListItem } from "../types";
import { ScheduleWriteErrorMessage } from "./ScheduleWriteErrorMessage";

type ScheduleListProps = {
  /** 表示する定期実行。空や失敗は呼び出し側が出す。 */
  schedules: readonly ScheduleListItem[];
  /** 一覧の状態と操作。 */
  page: SchedulesPageModel;
};

type ScheduleRowProps = {
  /** 対象の定期実行。 */
  schedule: ScheduleListItem;
  /** 一覧の状態と操作。 */
  page: SchedulesPageModel;
};

/** 表の見出しセルの共通クラス。 */
const HEADER_CELL_CLASS = "px-4 py-2.5 font-medium";

/** 表の本文セルの共通クラス。 */
const BODY_CELL_CLASS = "px-4 py-3 align-top";

/**
 * 定期実行の操作。削除と手動実行は確認ダイアログを挟む。無効な定期実行には手動実行を出さない。
 * @param props 定期実行と一覧の状態。
 * @returns 操作ボタン群。
 */
function ScheduleRowActions({ schedule, page }: Readonly<ScheduleRowProps>) {
  const text = useI18n().uiText.schedules.actions;
  const disabled = page.busyScheduleId !== null;

  return (
    <div className="flex flex-wrap justify-end gap-2">
      <Link href={`/schedules/${encodeURIComponent(schedule.scheduleId)}`} className={buttonClassName("outline", "sm")}>
        {text.edit}
      </Link>
      {schedule.enabled ? (
        <Button variant="standard" size="sm" disabled={disabled} onClick={() => page.requestConfirm(schedule.scheduleId, "run")}>
          {text.run}
        </Button>
      ) : null}
      <Button variant="danger" size="sm" disabled={disabled} onClick={() => page.requestConfirm(schedule.scheduleId, "delete")}>
        {text.delete}
      </Button>
    </div>
  );
}

/**
 * 有効・無効の表示。実行の状態とは別物なので `StatusBadge` は使わない。
 * @param props 有効か。
 * @returns ピル型の表示。
 */
function EnabledPill({ enabled }: Readonly<{ enabled: boolean }>) {
  const text = useI18n().uiText.schedules;
  const toneClass = enabled
    ? "bg-md-success-container text-md-on-success-container"
    : "bg-md-surface-container-high text-md-on-surface-variant";
  return (
    <span className={`inline-flex h-6 items-center rounded-pill px-2.5 text-xs font-semibold ${toneClass}`}>
      {enabled ? text.enabled : text.disabled}
    </span>
  );
}

/**
 * 1 件の定期実行の行と、その下に出す結果（手動実行の開始・失敗）の行。
 * @param props 定期実行と一覧の状態。
 * @returns 表の行。
 */
function ScheduleRow({ schedule, page }: Readonly<ScheduleRowProps>) {
  const { locale, uiText } = useI18n();
  const text = uiText.schedules;
  const dateTimeLocale = getDateTimeLocale(locale);
  const runStarted = page.runStarted?.scheduleId === schedule.scheduleId ? page.runStarted : null;
  const rowFailure = page.rowFailure?.scheduleId === schedule.scheduleId ? page.rowFailure : null;

  return (
    <>
      <tr className="border-t border-md-outline-variant">
        <td className={BODY_CELL_CLASS}>
          <p className="font-medium text-md-on-surface">{schedule.name}</p>
          <p className="mt-0.5 text-xs text-md-on-surface-variant">
            {page.definitionNames?.get(schedule.definitionId) ?? <span className="font-mono">{schedule.definitionId}</span>}
          </p>
        </td>
        <td className={BODY_CELL_CLASS}>
          <p className="font-mono text-md-on-surface">{schedule.cronExpression}</p>
          <p className="mt-0.5 text-xs text-md-on-surface-variant">{schedule.timeZone}</p>
        </td>
        <td className={`${BODY_CELL_CLASS} text-xs text-md-on-surface-variant`}>{text.overlap[schedule.overlapPolicy]}</td>
        <td className={BODY_CELL_CLASS}>
          <EnabledPill enabled={schedule.enabled} />
        </td>
        <td className={`${BODY_CELL_CLASS} whitespace-nowrap text-md-on-surface`}>
          {formatDateTimeLocalized(schedule.nextFireAt, dateTimeLocale)}
        </td>
        <td className={BODY_CELL_CLASS}>
          <ScheduleRowActions schedule={schedule} page={page} />
        </td>
      </tr>
      {runStarted !== null || rowFailure !== null ? (
        <tr>
          <td colSpan={6} className="px-4 pb-3">
            {runStarted === null ? null : (
              <Notice
                tone="success"
                title={
                  <Link href={`/executions/${encodeURIComponent(runStarted.displayId)}`} className="underline">
                    {text.actions.runStarted(runStarted.displayId)}
                  </Link>
                }
              />
            )}
            {rowFailure === null ? null : (
              <ScheduleWriteErrorMessage failure={rowFailure.failure} fallbackMessage={rowFailure.fallbackMessage} />
            )}
          </td>
        </tr>
      ) : null}
    </>
  );
}

/**
 * 定期実行の一覧。`input` は出さない（一覧の型に無い）。
 * 定義は定義一覧で名前を引き、引けなければ UUID をそのまま出す。狭い幅では表を横にスクロールする。
 * 削除と手動実行の確認は 1 つの確認ダイアログで受け、確定は 1 回だけ送る（連打は hook が止める）。
 * @param props 定期実行と一覧の状態。
 * @returns 一覧と確認ダイアログ。
 */
export function ScheduleList({ schedules, page }: Readonly<ScheduleListProps>) {
  const text = useI18n().uiText.schedules;
  const pendingSchedule =
    page.pending === null ? undefined : schedules.find((schedule) => schedule.scheduleId === page.pending?.scheduleId);
  const isDelete = page.pending?.action === "delete";
  const busy = page.busyScheduleId !== null;
  const pendingName = pendingSchedule?.name ?? "";

  return (
    <>
      <div className="overflow-x-auto rounded-card border border-md-outline bg-md-surface">
        <table className="w-full min-w-[56rem] text-left text-sm">
          <thead className="bg-md-surface-container text-xs text-md-on-surface-variant">
            <tr>
              <th className={HEADER_CELL_CLASS}>{text.columns.name}</th>
              <th className={HEADER_CELL_CLASS}>{text.columns.cron}</th>
              <th className={HEADER_CELL_CLASS}>{text.columns.overlap}</th>
              <th className={`${HEADER_CELL_CLASS} w-24`}>{text.columns.status}</th>
              <th className={HEADER_CELL_CLASS}>{text.columns.nextFireAt}</th>
              <th className={`${HEADER_CELL_CLASS} text-right`}>{text.columns.actions}</th>
            </tr>
          </thead>
          <tbody>
            {schedules.map((schedule) => (
              <ScheduleRow key={schedule.scheduleId} schedule={schedule} page={page} />
            ))}
          </tbody>
        </table>
      </div>
      <ConfirmDialog
        open={pendingSchedule !== undefined}
        title={isDelete ? text.actions.deleteConfirmTitle(pendingName) : text.actions.runConfirmTitle(pendingName)}
        message={isDelete ? text.actions.deleteConfirmMessage : text.actions.runConfirmNote}
        confirmLabel={confirmLabel(isDelete, busy, text.actions)}
        cancelLabel={text.actions.cancel}
        confirmVariant={isDelete ? "dangerSolid" : "standard"}
        loading={busy}
        onConfirm={() => void page.confirm()}
        onCancel={page.cancelConfirm}
      />
    </>
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
