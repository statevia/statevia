"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { PageShell } from "@/shared/ui/PageShell";
import type { ResourceGrantSubject } from "../resourceGrants";
import { ResourceGrantsForm } from "./ResourceGrantsForm";

type AdminUserResourceGrantsPageClientProps = {
  /** 対象のユーザー ID。 */
  userId: string;
};

/**
 * ユーザーの実行リソース許可。ユーザー一覧の行から開く。
 * テナント管理者以外は `app/admin/layout.tsx` が戻す。
 * @param props ユーザー ID。
 * @returns ユーザーの許可画面。
 */
export function AdminUserResourceGrantsPageClient({ userId }: Readonly<AdminUserResourceGrantsPageClientProps>) {
  const text = useUiText().admin.resourceGrants;
  // フックは対象の参照が変わると読み直すため、ID ごとに 1 つに固定する。
  const subject = useMemo<ResourceGrantSubject>(() => ({ kind: "user", userId }), [userId]);

  return (
    <PageShell
      title={text.title}
      secondaryActions={
        <Link href="/admin/users" className="hover:underline">
          {text.backToUsers}
        </Link>
      }
    >
      <ResourceGrantsForm subject={subject} />
    </PageShell>
  );
}
