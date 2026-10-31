"use client";

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
 * @returns API キー管理画面。
 */
export function AdminApiKeysPageClient() {
  const uiText = useUiText();
  const page = useAdminApiKeysPage();
  const apiKeys = page.apiKeys;

  return (
    <PageShell title={uiText.admin.apiKeys.title} description={uiText.admin.apiKeys.description}>
      {page.issuedKey ? (
        <AdminApiKeyIssuedNotice
          issuedKey={page.issuedKey}
          copied={page.copied}
          onCopy={page.copyPlainKey}
          onDismiss={page.dismissIssuedKey}
        />
      ) : null}

      <AdminApiKeyCreateForm
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

      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && apiKeys === null ? <PageState state="error" /> : null}
      {!page.loading && apiKeys !== null && apiKeys.length === 0 ? <PageState state="empty" /> : null}
      {!page.loading && apiKeys !== null && apiKeys.length > 0 ? (
        <AdminApiKeyList
          apiKeys={apiKeys}
          revokingId={page.revokingId}
          onRevoke={page.revokeApiKey}
        />
      ) : null}

      {page.toast ? <Toast toast={page.toast} onClose={page.dismissToast} /> : null}
    </PageShell>
  );
}
