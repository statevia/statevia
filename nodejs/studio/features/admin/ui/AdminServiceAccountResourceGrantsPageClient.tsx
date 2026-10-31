"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { PageShell } from "@/shared/ui/PageShell";
import type { ResourceGrantSubject } from "../resourceGrants";
import { ResourceGrantsForm } from "./ResourceGrantsForm";

type AdminServiceAccountResourceGrantsPageClientProps = {
  /** 対象の ServiceAccount ID。 */
  serviceAccountId: string;
};

/**
 * ServiceAccount の実行リソース許可。ServiceAccount 詳細から開く。
 * 定期実行の run-as にも同じ許可が効く。テナント管理者以外は `app/admin/layout.tsx` が戻す。
 * @param props ServiceAccount ID。
 * @returns ServiceAccount の許可画面。
 */
export function AdminServiceAccountResourceGrantsPageClient({
  serviceAccountId
}: Readonly<AdminServiceAccountResourceGrantsPageClientProps>) {
  const text = useUiText().admin.resourceGrants;
  // フックは対象の参照が変わると読み直すため、ID ごとに 1 つに固定する。
  const subject = useMemo<ResourceGrantSubject>(
    () => ({ kind: "serviceAccount", serviceAccountId }),
    [serviceAccountId]
  );

  return (
    <PageShell
      title={text.title}
      secondaryActions={
        <Link href={`/admin/service-accounts/${encodeURIComponent(serviceAccountId)}`} className="hover:underline">
          {text.backToServiceAccount}
        </Link>
      }
    >
      <ResourceGrantsForm subject={subject} />
    </PageShell>
  );
}
