"use client";

import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminServiceAccountsPage } from "../hooks/useAdminServiceAccountsPage";
import { AdminServiceAccountCreateForm } from "./AdminServiceAccountCreateForm";
import { AdminServiceAccountList } from "./AdminServiceAccountList";

/**
 * ServiceAccount の作成と一覧。作成フォームは 0 件のときの作成導線を兼ねる。
 * テナント管理者以外は `app/admin/layout.tsx` が戻す。
 * @returns ServiceAccount 管理画面。
 */
export function AdminServiceAccountsPageClient() {
  const text = useUiText().admin.serviceAccounts;
  const page = useAdminServiceAccountsPage();
  const serviceAccounts = page.serviceAccounts;

  return (
    <PageShell title={text.title} description={text.description}>
      <AdminServiceAccountCreateForm onCreated={page.reload} />
      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && serviceAccounts === null ? (
        <PageState
          state="error"
          message={text.loadFailed}
          retryLabel={text.retry}
          onRetry={() => void page.reload()}
        />
      ) : null}
      {!page.loading && serviceAccounts !== null && serviceAccounts.length === 0 ? (
        <PageState state="empty" message={text.empty} />
      ) : null}
      {!page.loading && serviceAccounts !== null && serviceAccounts.length > 0 ? (
        <AdminServiceAccountList serviceAccounts={serviceAccounts} />
      ) : null}
    </PageShell>
  );
}
