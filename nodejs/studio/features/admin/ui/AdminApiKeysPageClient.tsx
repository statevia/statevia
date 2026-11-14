"use client";

import { Plus } from "lucide-react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminApiKeysPage } from "../hooks/useAdminApiKeysPage";
import { AdminApiKeyCreateForm } from "./AdminApiKeyCreateForm";
import { AdminApiKeyIssuedNotice } from "./AdminApiKeyIssuedNotice";
import { AdminApiKeyList } from "./AdminApiKeyList";

/**
 * API キー一覧・発行・失効。
 * 発行は見出しの緑のボタンからダイアログで行い、平文は発行直後のダイアログにだけ出す。失効は確認を経てから送る。
 * @returns API キー管理画面。
 */
export function AdminApiKeysPageClient() {
  const uiText = useUiText();
  const text = uiText.admin.apiKeys;
  const page = useAdminApiKeysPage();
  const apiKeys = page.apiKeys;

  return (
    <PageShell
      title={text.title}
      description={text.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantAccess }, { label: text.title }]} />}
      primaryActions={
        <Button variant="lead" onClick={page.openCreateDialog}>
          <Plus aria-hidden="true" className="h-4 w-4" />
          {text.createTitle}
        </Button>
      }
    >
      {page.toast ? <Toast toast={page.toast} onClose={page.dismissToast} /> : null}

      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && apiKeys === null ? <PageState state="error" /> : null}
      {!page.loading && apiKeys !== null && apiKeys.length === 0 ? <PageState state="empty" /> : null}
      {!page.loading && apiKeys !== null && apiKeys.length > 0 ? (
        <AdminApiKeyList apiKeys={apiKeys} permissions={page.permissions} revokingId={page.revokingId} onRevoke={page.requestRevoke} />
      ) : null}

      <AdminApiKeyCreateForm
        open={page.createDialogOpen}
        onClose={page.closeCreateDialog}
        name={page.name}
        expiresAt={page.expiresAt}
        selectedScopes={page.selectedScopes}
        assignablePermissions={page.assignablePermissions}
        submitting={page.submitting}
        onNameChange={page.setName}
        onExpiresAtChange={page.setExpiresAt}
        onToggleScope={page.toggleScope}
        onSubmit={page.createApiKey}
      />

      {page.issuedKey ? (
        <AdminApiKeyIssuedNotice issuedKey={page.issuedKey} copied={page.copied} onCopy={page.copyPlainKey} onDismiss={page.dismissIssuedKey} />
      ) : null}

      <ConfirmDialog
        open={page.revokeTarget !== null}
        title={page.revokeTarget ? text.revokeConfirmTitle(page.revokeTarget.name) : ""}
        message={text.revokeConfirmMessage}
        confirmLabel={text.revokeConfirmSubmit}
        onConfirm={page.confirmRevoke}
        onCancel={page.cancelRevoke}
      />
    </PageShell>
  );
}
