"use client";

import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminUsersPage } from "../hooks/useAdminUsersPage";
import { AdminUserCreateForm } from "./AdminUserCreateForm";
import { AdminUserList } from "./AdminUserList";
import { AdminUserPasswordDialog } from "./AdminUserPasswordDialog";

/**
 * ユーザー一覧・作成・有効化/無効化・パスワード更新。
 * @returns ユーザー管理画面。
 */
export function AdminUsersPageClient() {
  const uiText = useUiText();
  const page = useAdminUsersPage();
  const users = page.users;

  return (
    <PageShell title={uiText.admin.users.title} description={uiText.admin.users.description}>
      <AdminUserCreateForm
        username={page.username}
        email={page.email}
        password={page.password}
        displayName={page.displayName}
        isTenantAdmin={page.isTenantAdmin}
        submitting={page.submitting}
        onUsernameChange={page.setUsername}
        onEmailChange={page.setEmail}
        onPasswordChange={page.setPassword}
        onDisplayNameChange={page.setDisplayName}
        onTenantAdminChange={page.setIsTenantAdmin}
        onSubmit={page.createUser}
      />

      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && users === null ? <PageState state="error" /> : null}
      {!page.loading && users !== null && users.length === 0 ? <PageState state="empty" /> : null}
      {!page.loading && users !== null && users.length > 0 ? (
        <AdminUserList
          users={users}
          onOpenPassword={page.openPasswordDialog}
          onToggleActive={page.toggleActive}
        />
      ) : null}

      {page.passwordTarget ? (
        <AdminUserPasswordDialog
          user={page.passwordTarget}
          newPassword={page.newPassword}
          confirmPassword={page.confirmPassword}
          passwordMismatch={page.passwordMismatch}
          submitting={page.passwordSubmitting}
          onNewPasswordChange={page.changeNewPassword}
          onConfirmPasswordChange={page.changeConfirmPassword}
          onClose={page.closePasswordDialog}
          onSubmit={page.updatePassword}
        />
      ) : null}

      {page.toast ? <Toast toast={page.toast} onClose={page.dismissToast} /> : null}
    </PageShell>
  );
}
