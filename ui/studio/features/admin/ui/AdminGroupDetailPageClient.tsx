"use client";

import Link from "next/link";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminGroupDetail } from "../hooks/useAdminGroupDetail";
import { AdminGroupMembersSection } from "./AdminGroupMembersSection";
import { AdminGroupPermissionsSection } from "./AdminGroupPermissionsSection";

type AdminGroupDetailPageClientProps = {
  /** 編集対象のグループ ID。 */
  groupId: string;
};

/**
 * グループのメンバー・権限を編集する。
 * @param props グループ ID。
 * @returns グループ詳細画面。
 */
export function AdminGroupDetailPageClient({ groupId }: Readonly<AdminGroupDetailPageClientProps>) {
  const uiText = useUiText();
  const page = useAdminGroupDetail({ groupId });

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
      secondaryActions={
        <Link href="/admin/groups" className="hover:underline">
          {uiText.admin.groupManagement.backToList}
        </Link>
      }
    >
      <AdminGroupMembersSection
        users={page.users}
        selectedUserIds={page.selectedUserIds}
        saving={page.savingMembers}
        onToggleUser={page.toggleUser}
        onSave={page.saveMembers}
      />
      <AdminGroupPermissionsSection
        permissions={page.assignablePermissions}
        selectedPermissionKeys={page.selectedPermissionKeys}
        saving={page.savingPermissions}
        onTogglePermission={page.togglePermission}
        onSave={page.savePermissions}
      />
      {page.toast ? <Toast toast={page.toast} onClose={page.dismissToast} /> : null}
    </PageShell>
  );
}
