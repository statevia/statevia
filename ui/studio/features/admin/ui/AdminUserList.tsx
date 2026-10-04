"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import type { AdminUserListItem } from "../types";

/** ユーザー一覧の行。 */
export type AdminUserListProps = {
  /** 現在のユーザー。空や失敗は呼び出し側が出す。 */
  users: readonly AdminUserListItem[];
  /**
   * パスワード更新ダイアログを開く。
   * @param user 対象ユーザー。
   */
  onOpenPassword: (user: AdminUserListItem) => void;
  /**
   * 有効と無効を入れ替える。
   * @param user 対象ユーザー。
   */
  onToggleActive: (user: AdminUserListItem) => void;
};

/**
 * テナントユーザーの一覧。取得中・空・失敗は呼び出し側に残す。
 * @param props 行と操作コマンド。
 * @returns ユーザーのリスト。
 */
export function AdminUserList({
  users,
  onOpenPassword,
  onToggleActive
}: Readonly<AdminUserListProps>) {
  const uiText = useUiText();

  return (
    <ul className="divide-y divide-md-outline rounded-xl border border-md-outline bg-md-surface">
      {users.map((user) => (
        <li
          key={user.userId}
          className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
        >
          <div className="min-w-0">
            <p className="font-medium text-md-on-surface">{user.username}</p>
            <p className="text-md-on-surface-variant">
              {user.email ? `${user.displayName} · ${user.email}` : user.displayName}
            </p>
            <p className="text-xs text-md-on-surface-variant">
              {user.isActive ? uiText.admin.users.active : uiText.admin.users.inactive}
              {user.isTenantAdmin ? ` · ${uiText.admin.users.adminBadge}` : ""}
              {` · ${uiText.admin.users.groupCount(user.groupIds.length)}`}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => onOpenPassword(user)}
              className="rounded-lg border border-md-outline px-3 py-1.5 hover:bg-md-surface-container"
            >
              {uiText.admin.users.updatePassword}
            </button>
            <button
              type="button"
              onClick={() => onToggleActive(user)}
              className="rounded-lg border border-md-outline px-3 py-1.5 hover:bg-md-surface-container"
            >
              {user.isActive ? uiText.admin.users.disable : uiText.admin.users.enable}
            </button>
          </div>
        </li>
      ))}
    </ul>
  );
}
