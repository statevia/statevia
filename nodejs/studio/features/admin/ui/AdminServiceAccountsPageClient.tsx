"use client";

import { Plus } from "lucide-react";
import { useCallback, useState } from "react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminServiceAccountsPage } from "../hooks/useAdminServiceAccountsPage";
import { AdminServiceAccountCreateForm } from "./AdminServiceAccountCreateForm";
import { AdminServiceAccountList } from "./AdminServiceAccountList";

/**
 * サービスアカウントの一覧と作成。作成は見出しの緑のボタンからダイアログで行い、作成できたら閉じて一覧を取り直す。
 * 削除と API キーの発行はこの画面に置かない。
 * @returns サービスアカウント管理画面。
 */
export function AdminServiceAccountsPageClient() {
  const uiText = useUiText();
  const text = uiText.admin.serviceAccounts;
  const page = useAdminServiceAccountsPage();
  const serviceAccounts = page.serviceAccounts;
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const { reload } = page;

  const handleCreated = useCallback(async () => {
    setCreateDialogOpen(false);
    await reload();
  }, [reload]);

  return (
    <PageShell
      title={text.title}
      description={text.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantAccess }, { label: text.title }]} />}
      primaryActions={
        <Button variant="lead" onClick={() => setCreateDialogOpen(true)}>
          <Plus aria-hidden="true" className="h-4 w-4" />
          {text.createTitle}
        </Button>
      }
    >
      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && serviceAccounts === null ? (
        <PageState state="error" message={text.loadFailed} retryLabel={text.retry} onRetry={() => void page.reload()} />
      ) : null}
      {!page.loading && serviceAccounts !== null && serviceAccounts.length === 0 ? (
        <PageState state="empty" message={text.empty} />
      ) : null}
      {!page.loading && serviceAccounts !== null && serviceAccounts.length > 0 ? (
        <AdminServiceAccountList serviceAccounts={serviceAccounts} />
      ) : null}

      <AdminServiceAccountCreateForm open={createDialogOpen} onClose={() => setCreateDialogOpen(false)} onCreated={handleCreated} />
    </PageShell>
  );
}
