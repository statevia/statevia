"use client";

import { Plus } from "lucide-react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
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
 * 作成は見出しの緑のボタンからダイアログで行い、無効化は確認ダイアログを経てから送る。
 * @returns ユーザー管理画面。
 */
export function AdminUsersPageClient() {
  const uiText = useUiText();
  const text = uiText.admin.users;
  const page = useAdminUsersPage();
  const users = page.users;

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
      {!page.loading && users === null ? <PageState state="error" /> : null}
      {!page.loading && users !== null && users.length === 0 ? <PageState state="empty" /> : null}
      {!page.loading && users !== null && users.length > 0 ? (
        <AdminUserList users={users} onOpenPassword={page.openPasswordDialog} onToggleActive={page.requestToggleActive} />
      ) : null}

      <AdminUserCreateForm
        open={page.createDialogOpen}
        onClose={page.closeCreateDialog}
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

      <ConfirmDialog
        open={page.disableTarget !== null}
        title={page.disableTarget ? text.disableConfirmTitle(page.disableTarget.username) : ""}
        message={text.disableConfirmMessage}
        confirmLabel={text.disableConfirmSubmit}
        onConfirm={page.confirmDisable}
        onCancel={page.cancelDisable}
      />
    </PageShell>
  );
}
