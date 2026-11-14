"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
import { resolvePermissionLabel } from "../permissionLabel";
import type { PermissionDefinitionDto } from "../types";

/**
 * チェックボックス用の HTML id。権限キーの `.` などは置換する。
 * @param prefix id の接頭辞。
 * @param key 権限キー。
 * @returns HTML id。
 */
function checkboxInputId(prefix: string, key: string): string {
  return `${prefix}-${key.replaceAll(/[^a-zA-Z0-9_-]/g, "-")}`;
}

/** グループ権限欄の入力。 */
export type AdminGroupPermissionsSectionProps = {
  /** 選べる権限。 */
  permissions: readonly PermissionDefinitionDto[];
  /** 選択中の権限キー。 */
  selectedPermissionKeys: ReadonlySet<string>;
  /** 保存中か。 */
  saving: boolean;
  /**
   * 選択を切り替える。
   * @param permissionKey 権限キー。
   */
  onTogglePermission: (permissionKey: string) => void;
  /** 選択中の権限を保存する。 */
  onSave: () => void;
};

/**
 * グループの権限選択。取得中と失敗は呼び出し側に残す。
 * @param props 権限と保存コマンド。
 * @returns 権限欄。
 */
export function AdminGroupPermissionsSection({
  permissions,
  selectedPermissionKeys,
  saving,
  onTogglePermission,
  onSave
}: Readonly<AdminGroupPermissionsSectionProps>) {
  const uiText = useUiText();

  return (
    <Card
      title={uiText.admin.groupManagement.permissionsTitle}
      actions={
        <Button variant="standard" loading={saving} onClick={onSave}>
          {saving ? uiText.admin.groupManagement.saving : uiText.admin.groupManagement.savePermissions}
        </Button>
      }
    >
      <ul className="max-h-64 space-y-2 overflow-y-auto">
        {permissions.map((permission) => {
          const inputId = checkboxInputId("group-permission", permission.permissionKey);
          const labelText = `${resolvePermissionLabel(permission, uiText.admin.permissions.names)} (${permission.permissionKey})`;
          return (
            <li key={permission.permissionKey} className="flex items-start gap-2 text-sm">
              <input
                id={inputId}
                type="checkbox"
                checked={selectedPermissionKeys.has(permission.permissionKey)}
                onChange={() => onTogglePermission(permission.permissionKey)}
              />
              <label htmlFor={inputId} className="cursor-pointer">
                {labelText}
              </label>
            </li>
          );
        })}
      </ul>
    </Card>
  );
}
