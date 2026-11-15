"use client";

import Link from "next/link";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminGroupDetail } from "../hooks/useAdminGroupDetail";
import { resolvePermissionLabel } from "../permissionLabel";
import { AdminGroupSelectionSection } from "./AdminGroupSelectionSection";

type AdminGroupDetailPageClientProps = {
  /** 編集対象のグループ ID。 */
  groupId: string;
};

/**
 * グループのメンバー・権限を編集する。どちらも検索して選び、保存前に追加と解除の差分を確かめる（要件 9.5）。
 * @param props グループ ID。
 * @returns グループ詳細画面。
 */
export function AdminGroupDetailPageClient({ groupId }: Readonly<AdminGroupDetailPageClientProps>) {
  const uiText = useUiText();
  const page = useAdminGroupDetail({ groupId });
  const text = uiText.admin.groupManagement;

  if (page.loading) {
    return (
      <PageShell title={uiText.admin.groupManagement.title}>
        <PageState state="loading" />
      </PageShell>
    );
  }

  if (!page.group) {
    return (
      <PageShell title={uiText.admin.groupManagement.title}>
        <PageState state="error" />
        {page.toast ? <Toast toast={page.toast} onClose={page.dismissToast} /> : null}
      </PageShell>
    );
  }

  return (
    <PageShell
      title={page.group.name}
      description={uiText.admin.groupManagement.description}
      breadcrumbs={
        <Breadcrumbs
          items={[
            { label: uiText.navigation.groups.tenantAccess },
            { label: uiText.admin.groupManagement.title, href: "/admin/groups" },
            { label: page.group.name }
          ]}
        />
      }
      secondaryActions={
        <Link href="/admin/groups" className="hover:underline">
          {uiText.admin.groupManagement.backToList}
        </Link>
      }
    >
      <AdminGroupSelectionSection
        title={text.membersTitle}
        saveLabel={text.saveMembers}
        confirmTitle={text.confirmMembersTitle}
        items={page.users.map((user) => ({
          value: user.userId,
          label: user.isActive ? user.username : `${user.username} (${uiText.admin.users.inactive})`,
          description: user.displayName || (user.email ?? undefined)
        }))}
        selected={page.selectedUserIds}
        saved={page.savedUserIds}
        saving={page.savingMembers}
        onToggle={page.toggleUser}
        onReset={page.resetMembers}
        onSave={page.saveMembers}
      />
      <AdminGroupSelectionSection
        title={text.permissionsTitle}
        saveLabel={text.savePermissions}
        confirmTitle={text.confirmPermissionsTitle}
        items={page.assignablePermissions.map((permission) => ({
          value: permission.permissionKey,
          label: resolvePermissionLabel(permission, uiText.admin.permissions.names),
          description: permission.permissionKey
        }))}
        selected={page.selectedPermissionKeys}
        saved={page.savedPermissionKeys}
        saving={page.savingPermissions}
        onToggle={page.togglePermission}
        onReset={page.resetPermissions}
        onSave={page.savePermissions}
      />
      {page.toast ? <Toast toast={page.toast} onClose={page.dismissToast} /> : null}
    </PageShell>
  );
}
