"use client";

import { useI18n } from "@/shared/i18n/uiTextContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { Button } from "@/shared/ui/Button";
import { Notice } from "@/shared/ui/Notice";
import type { WaitTarget } from "../lib/waitTargets";

/** 待機のお知らせの props。 */
export type ExecutionWaitNoticeProps = {
  /** 待っているノード。空なら何も出さない。 */
  waits: readonly WaitTarget[];
  /**
   * 待機の行の「このイベントを送る」を押したとき。未指定なら行の操作を出さない（executions.write が無い・送れない状態）。
   * @param nodeId 届け先に選んでおくノード ID。
   */
  onSendTo?: (nodeId: string) => void;
};

/**
 * 実行の詳細の見出しの下に出す、待機のお知らせ（要件 5.2・5.3）。
 * @remarks 待機が 1 つなら、待っているノードとイベント名を文で出す。2 つ以上（並列分岐）なら待機を 1 件ずつ並べ、
 * 各行の「このイベントを送る」から、そのノードを届け先に選んだ状態でイベント送信ダイアログを開く。
 */
export function ExecutionWaitNotice({ waits, onSendTo }: Readonly<ExecutionWaitNoticeProps>) {
  const { uiText, locale } = useI18n();
  const text = uiText.executionDashboard.waitNotice;
  const dateTimeLocale = getDateTimeLocale(locale);
  const [first] = waits;
  if (!first) return null;

  if (waits.length === 1) {
    return (
      <Notice tone="warning" title={text.singleTitle(first.nodeName)}>
        {text.singleBody(first.events.join(" / "))}
      </Notice>
    );
  }

  return (
    <Notice tone="warning" title={text.multiTitle(waits.length)} className="[&>div]:w-full [&>div>div]:w-full">
      <p>{text.multiBody}</p>
      <ul aria-label={text.listLabel} className="mt-2 flex w-full flex-col divide-y divide-md-warning/30 border-t border-md-warning/30">
        {waits.map((wait) => (
          <li key={wait.nodeId} className="flex flex-wrap items-center gap-x-4 gap-y-1 py-2">
            <span className="min-w-32 font-semibold">{wait.nodeName}</span>
            <span className="min-w-0 flex-1 truncate font-mono text-xs">{wait.events.join(" / ")}</span>
            {wait.waitingSince ? (
              <span className="text-xs whitespace-nowrap opacity-80">
                {text.waitingSince(formatDateTimeLocalized(wait.waitingSince, dateTimeLocale))}
              </span>
            ) : null}
            {onSendTo ? (
              <Button
                variant="outline"
                size="sm"
                aria-label={`${uiText.executionDashboard.sendEvent.sendThisEvent}: ${wait.nodeName}`}
                onClick={() => onSendTo(wait.nodeId)}
              >
                {uiText.executionDashboard.sendEvent.sendThisEvent}
              </Button>
            ) : null}
          </li>
        ))}
      </ul>
    </Notice>
  );
}
