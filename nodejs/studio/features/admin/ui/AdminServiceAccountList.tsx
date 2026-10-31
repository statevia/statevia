"use client";

import Link from "next/link";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import type { AdminServiceAccount } from "../types";

/** ServiceAccount 一覧の行。 */
export type AdminServiceAccountListProps = {
  /** 表示する ServiceAccount。空や失敗は呼び出し側が出す。 */
  serviceAccounts: readonly AdminServiceAccount[];
};

/**
 * ServiceAccount の一覧。名前、有効、キーの有無、所属グループ数、作成日時と詳細へのリンクを出す。
 * 削除操作は置かない（削除 API が無く、停止は詳細での無効化で行う）。
 * @param props 表示する行。
 * @returns ServiceAccount のリスト。
 */
export function AdminServiceAccountList({ serviceAccounts }: Readonly<AdminServiceAccountListProps>) {
  const { locale, uiText } = useI18n();
  const text = uiText.admin.serviceAccounts;
  const dateTimeLocale = getDateTimeLocale(locale);

  return (
    <ul className="divide-y divide-md-outline rounded-xl border border-md-outline bg-md-surface">
      {serviceAccounts.map((serviceAccount) => (
        <li
          key={serviceAccount.serviceAccountId}
          className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
        >
          <div className="min-w-0">
            <p className="font-medium text-md-on-surface">{serviceAccount.name}</p>
            <p className="text-xs text-md-on-surface-variant">
              {serviceAccount.isActive ? text.active : text.inactive}
              {` · ${serviceAccount.hasApiKey ? text.hasApiKey : text.noApiKey}`}
              {` · ${text.groupCount(serviceAccount.groupIds.length)}`}
            </p>
            <p className="text-xs text-md-on-surface-variant">
              {text.createdLabel(formatDateTimeLocalized(serviceAccount.createdAt, dateTimeLocale))}
            </p>
          </div>
          <Link
            href={`/admin/service-accounts/${encodeURIComponent(serviceAccount.serviceAccountId)}`}
            className="rounded-lg border border-md-outline px-3 py-1.5 hover:bg-md-surface-container"
          >
            {text.openDetail}
          </Link>
        </li>
      ))}
    </ul>
  );
}
