"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import type { AdminUserListItem } from "../types";

/**
 * チェックボックス用の HTML id。ユーザー ID の記号は置換する。
 * @param prefix id の接頭辞。
 * @param key ユーザー ID。
 * @returns HTML id。
 */
function checkboxInputId(prefix: string, key: string): string {
  return `${prefix}-${key.replaceAll(/[^a-zA-Z0-9_-]/g, "-")}`;
}

/**
 * 無効ユーザーには inactive 表記を付ける。
 * @param user ユーザー。
 * @param inactiveLabel 無効の文言。
 * @returns チェックボックスのラベル。
 */
function memberCheckboxLabel(user: AdminUserListItem, inactiveLabel: string): string {
  return user.isActive ? user.username : `${user.username} (${inactiveLabel})`;
}

/** グループメンバー欄の入力。 */
export type AdminGroupMembersSectionProps = {
  /** メンバー候補。 */
  users: readonly AdminUserListItem[];
  /** 選択中のユーザー ID。 */
  selectedUserIds: ReadonlySet<string>;
  /** 保存中か。 */
  saving: boolean;
  /**
   * 選択を切り替える。
   * @param userId ユーザー ID。
   */
  onToggleUser: (userId: string) => void;
  /** 選択中のメンバーを保存する。 */
  onSave: () => void;
};

/**
 * グループのメンバー選択。取得中と失敗は呼び出し側に残す。
 * @param props 候補と保存コマンド。
 * @returns メンバー欄。
 */
export function AdminGroupMembersSection({
  users,
  selectedUserIds,
  saving,
  onToggleUser,
  onSave
}: Readonly<AdminGroupMembersSectionProps>) {
  const uiText = useUiText();

  return (
    <section className="rounded-xl border border-md-outline bg-md-surface p-4">
      <h2 className="mb-3 text-lg font-medium">{uiText.admin.groupManagement.membersTitle}</h2>
      <ul className="mb-4 max-h-64 space-y-2 overflow-y-auto">
        {users.map((user) => {
          const inputId = checkboxInputId("group-member", user.userId);
          const labelText = memberCheckboxLabel(user, uiText.admin.users.inactive);
          return (
            <li key={user.userId} className="flex items-center gap-2 text-sm">
              <input
                id={inputId}
                type="checkbox"
                checked={selectedUserIds.has(user.userId)}
                onChange={() => onToggleUser(user.userId)}
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
        {saving ? uiText.admin.groupManagement.saving : uiText.admin.groupManagement.saveMembers}
      </button>
    </section>
  );
}
