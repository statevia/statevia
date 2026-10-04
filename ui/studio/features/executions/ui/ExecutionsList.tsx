"use client";

import { StatusBadge } from "@/shared/ui/StatusBadge";
import { NAVIGATION_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import type { ExecutionDTO } from "../types";

/** 実行一覧の行。 */
export type ExecutionsListProps = {
  /** 現在ページの実行。 */
  items: readonly ExecutionDTO[];
  /**
   * 詳細を開く。
   * @param displayId 実行の displayId。
   */
  onOpen: (displayId: string) => void;
};

/**
 * 実行一覧の行。ページングと空・失敗の出し分けは呼び出し側に残す。
 * @param props 行と詳細を開くコマンド。
 * @returns 実行のリスト。
 */
export function ExecutionsList({ items, onOpen }: Readonly<ExecutionsListProps>) {
  const { uiText, locale } = useI18n();
  const dateTimeLocale = getDateTimeLocale(locale);

  return (
    <ul
      className="divide-y divide-md-outline overflow-hidden rounded-lg border border-md-outline bg-md-surface shadow-sm"
      aria-label={uiText.lists.executions}
    >
      {items.map((execution) => {
        const updated = execution.updatedAt ?? execution.startedAt;
        return (
          <li key={execution.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <StatusBadge status={execution.status} />
                <span className="truncate font-mono text-sm text-md-on-surface" title={execution.displayId}>
                  {execution.displayId}
                </span>
              </div>
              <p className="mt-1 text-xs text-md-on-surface-variant">{uiText.executionsPage.updatedAt(formatDateTimeLocalized(updated, dateTimeLocale))}</p>
            </div>
            <button
              type="button"
              className={`shrink-0 ${NAVIGATION_BUTTON_CLASS}`}
              onClick={() => onOpen(execution.displayId)}
            >
              {uiText.executionsPage.actions.openDetail}
            </button>
          </li>
        );
      })}
    </ul>
  );
}
