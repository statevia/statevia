"use client";

import { Send } from "lucide-react";
import type { ExecutionView } from "../types";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { Button } from "@/shared/ui/Button";
import { StatusBadge } from "@/shared/ui/StatusBadge";

/** 実行の詳細の見出しの props。 */
export type ExecutionHeaderProps = {
  /** URL の実行 ID。見出しに出す。 */
  executionId: string;
  /** 現在の実行。未取得なら null（状態のバッジと開始日時を出さない）。 */
  execution: ExecutionView | null;
  /** 待っているノードがあるか。`ACTIVE` の横に待機の印を出す（U1）。 */
  waiting: boolean;
  /** 実行の取得・操作の最中か。 */
  loading: boolean;
  /** キャンセルを受け付けるか。 */
  canCancel: boolean;
  /** キャンセルの操作を出すか（executions.write があるとき）。 */
  showCancelAction: boolean;
  /** キャンセルを要求する。 */
  onCancel: () => void;
  /** 比較の操作を出すか。 */
  showCompareAction: boolean;
  /** 比較を開いているか。 */
  compareMode: boolean;
  /**
   * 比較の開閉を切り替える。
   * @param enabled 開くとき true。
   */
  onCompareModeChange: (enabled: boolean) => void;
  /** 「イベントを送信」を出すか（executions.write があり、待機があって送れる状態のとき）。 */
  showSendEventAction: boolean;
  /** イベント送信ダイアログを開く。 */
  onOpenSendEvent: () => void;
};

/**
 * 実行の詳細の見出し（要件 5.1）。左に実行 ID・状態・開始日時、右に赤枠の「キャンセル」、枠線の「比較」、緑の「イベントを送信」を並べる。
 * @remarks 緑（主導線）は「イベントを送信」だけにする。操作の出し分けは案内で、認可の正本は Service API。
 */
export function ExecutionHeader({
  executionId,
  execution,
  waiting,
  loading,
  canCancel,
  showCancelAction,
  onCancel,
  showCompareAction,
  compareMode,
  onCompareModeChange,
  showSendEventAction,
  onOpenSendEvent
}: Readonly<ExecutionHeaderProps>) {
  const { uiText, locale } = useI18n();
  const text = uiText.executionHeader;
  const meta = execution
    ? [
        text.startedAt(formatDateTimeLocalized(execution.startedAt, getDateTimeLocale(locale))),
        execution.cancelRequested ? text.cancelRequested : null
      ].filter((part): part is string => part !== null)
    : [];

  return (
    <header className="flex flex-col items-start gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-4">
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2.5">
          <h1 className="font-mono text-2xl font-bold break-all text-md-on-surface">{executionId}</h1>
          {execution ? <StatusBadge status={execution.status} waiting={waiting} /> : null}
        </div>
        {meta.length > 0 ? <p className="text-sm text-md-on-surface-variant">{meta.join(" ・ ")}</p> : null}
      </div>
      <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto sm:justify-end">
        {showCancelAction && (
          <Button variant="danger" onClick={onCancel} disabled={!canCancel || loading}>
            {uiText.actions.cancel}
          </Button>
        )}
        {showCompareAction && (
          <Button variant="outline" aria-pressed={compareMode} onClick={() => onCompareModeChange(!compareMode)}>
            {text.compareLabel}
          </Button>
        )}
        {showSendEventAction && (
          <Button variant="lead" onClick={onOpenSendEvent} disabled={loading}>
            <Send aria-hidden="true" className="h-4 w-4" />
            {uiText.executionDashboard.sendEvent.open}
          </Button>
        )}
      </div>
    </header>
  );
}
