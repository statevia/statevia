"use client";

import Link from "next/link";
import { DataList, type DataListColumn } from "@/shared/ui/DataList";
import { StatusBadge } from "@/shared/ui/StatusBadge";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import type { ExecutionDTO } from "../types";

/** 実行一覧の行。 */
export type ExecutionsListProps = {
  /** 現在ページの実行。 */
  items: readonly ExecutionDTO[];
};

/**
 * 実行一覧の列（状態・実行 ID・開始・更新）。
 * @param text 実行一覧の辞書。
 * @param dateTimeLocale 日時の書式のロケール。
 * @returns 列の定義。
 */
function buildExecutionColumns(
  text: ReturnType<typeof useI18n>["uiText"]["executionsPage"],
  dateTimeLocale: string
): DataListColumn<ExecutionDTO>[] {
  return [
    {
      id: "status",
      header: text.columns.status,
      width: "11rem",
      hideLabelOnMobile: true,
      cell: (execution) => <StatusBadge status={execution.status} waiting={execution.waiting} />
    },
    {
      id: "execution",
      header: text.columns.execution,
      width: "minmax(0,1fr)",
      hideLabelOnMobile: true,
      cell: (execution) => (
        <Link
          href={`/executions/${encodeURIComponent(execution.displayId)}`}
          className="block truncate font-mono text-md-primary hover:underline"
          title={execution.displayId}
        >
          {execution.displayId}
        </Link>
      )
    },
    {
      id: "startedAt",
      header: text.columns.startedAt,
      width: "10rem",
      cell: (execution) => (
        <span className="whitespace-nowrap text-xs text-md-on-surface-variant">
          {formatDateTimeLocalized(execution.startedAt, dateTimeLocale)}
        </span>
      )
    },
    {
      id: "updatedAt",
      header: text.columns.updatedAt,
      width: "10rem",
      align: "end",
      cell: (execution) => (
        <span className="whitespace-nowrap text-xs text-md-on-surface-variant">
          {formatDateTimeLocalized(execution.updatedAt ?? execution.startedAt, dateTimeLocale)}
        </span>
      )
    }
  ];
}

/**
 * 実行一覧の行（共通の `DataList`。デスクトップは表、スマートフォンはカード）。ページングと空・失敗の出し分けは呼び出し側に残す。
 * 実行 ID が詳細へのリンクを兼ねる。状態の列は幅を固定し、右の ID の開始位置を揃える。
 * @param props 行。
 * @returns 実行の一覧。
 */
export function ExecutionsList({ items }: Readonly<ExecutionsListProps>) {
  const { uiText, locale } = useI18n();
  const text = uiText.executionsPage;
  const dateTimeLocale = getDateTimeLocale(locale);

  const columns = buildExecutionColumns(text, dateTimeLocale);

  return (
    <DataList
      ariaLabel={uiText.lists.executions}
      columns={columns}
      items={items}
      getRowKey={(execution) => execution.displayId}
    />
  );
}
