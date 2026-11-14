"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
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
    <Card
      title={uiText.admin.groupManagement.membersTitle}
      actions={
        <Button variant="standard" loading={saving} onClick={onSave}>
          {saving ? uiText.admin.groupManagement.saving : uiText.admin.groupManagement.saveMembers}
        </Button>
      }
    >
      <ul className="max-h-64 space-y-2 overflow-y-auto">
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
    </Card>
  );
}
