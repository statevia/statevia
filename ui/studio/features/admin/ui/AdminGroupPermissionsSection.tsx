"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
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
    <section className="rounded-xl border border-md-outline bg-md-surface p-4">
      <h2 className="mb-3 text-lg font-medium">{uiText.admin.groupManagement.permissionsTitle}</h2>
      <ul className="mb-4 max-h-64 space-y-2 overflow-y-auto">
        {permissions.map((permission) => {
          const inputId = checkboxInputId("group-permission", permission.permissionKey);
          const labelText = `${permission.displayLabel} (${permission.permissionKey})`;
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
      <button
        type="button"
        disabled={saving}
        onClick={onSave}
        className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
      >
        {saving ? uiText.admin.groupManagement.saving : uiText.admin.groupManagement.savePermissions}
      </button>
    </section>
  );
}
