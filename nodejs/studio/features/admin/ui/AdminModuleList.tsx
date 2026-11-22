"use client";

import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { DataList, type DataListColumn } from "@/shared/ui/DataList";
import type { AdminModule } from "../types";
import { AdminModuleStatusPill } from "./AdminModuleStatusPill";

/** アクションモジュール一覧の行。 */
export type AdminModuleListProps = {
  /** 表示する Module。空や失敗は呼び出し側が出す。 */
  modules: readonly AdminModule[];
};

/**
 * アクションモジュール一覧の列（名前と ID・バージョン・結果・取得元・読み込んだ日時）。
 * 取得元が無い行は空のままにする。
 * @param text アクションモジュール画面の辞書。
 * @param dateTimeLocale 日時の書式のロケール。
 * @returns 列の定義。
 */
function buildModuleColumns(
  text: ReturnType<typeof useI18n>["uiText"]["admin"]["modules"],
  dateTimeLocale: string
): DataListColumn<AdminModule>[] {
  return [
    {
      id: "module",
      header: text.moduleColumn,
      width: "minmax(0,2fr)",
      hideLabelOnMobile: true,
      cell: (moduleRow) => (
        <div className="min-w-0">
          <p className="font-semibold text-md-on-surface">{moduleRow.name}</p>
          <p className="break-all font-mono text-xs text-md-on-surface-variant">{moduleRow.moduleId}</p>
        </div>
      )
    },
    { id: "version", header: text.versionColumn, width: "6rem", cell: (moduleRow) => moduleRow.version },
    {
      id: "status",
      header: text.statusColumn,
      width: "8rem",
      cell: (moduleRow) => <AdminModuleStatusPill status={moduleRow.status} />
    },
    {
      id: "source",
      header: text.sourceColumn,
      width: "minmax(0,1.5fr)",
      cell: (moduleRow) => <span className="break-all text-xs">{moduleRow.sourceLabel ?? ""}</span>
    },
    {
      id: "loadedAt",
      header: text.loadedAtColumn,
      width: "11rem",
      cell: (moduleRow) => formatDateTimeLocalized(moduleRow.loadedAtUtc, dateTimeLocale)
    }
  ];
}

/**
 * アクションモジュールのロード結果の一覧。契約の全項目を出す。
 * 列は名前と ID・バージョン・結果・取得元・読み込んだ日時で、長い値（メッセージ・SHA256）は行の下に出す。
 * 取得元が無い行は空のままにし、由来を断定する文は足さない。操作（再読み込み・削除）は置かない。
 * @param props 表示する行。
 * @returns アクションモジュールの一覧。
 */
export function AdminModuleList({ modules }: Readonly<AdminModuleListProps>) {
  const { locale, uiText } = useI18n();
  const text = uiText.admin.modules;
  const dateTimeLocale = getDateTimeLocale(locale);

  const columns = buildModuleColumns(text, dateTimeLocale);

  return (
    <DataList
      ariaLabel={text.listLabel}
      columns={columns}
      items={modules}
      getRowKey={(moduleRow) => `${moduleRow.moduleId}:${moduleRow.version}:${moduleRow.sha256}`}
      renderRowFooter={(moduleRow) => (
        <dl className="mt-2 grid gap-x-3 gap-y-1 text-xs text-md-on-surface-variant sm:grid-cols-[auto_minmax(0,1fr)]">
          {moduleRow.message ? (
            <>
              <dt className="font-medium">{text.messageLabel}</dt>
              <dd className="whitespace-pre-wrap break-words text-md-on-surface">{moduleRow.message}</dd>
            </>
          ) : null}
          <dt className="font-medium">{text.sha256Label}</dt>
          <dd className="break-all font-mono">{moduleRow.sha256}</dd>
        </dl>
      )}
    />
  );
}
