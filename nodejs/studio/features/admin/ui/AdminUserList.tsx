"use client";

import Link from "next/link";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button, buttonClassName } from "@/shared/ui/Button";
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
   * 有効と無効を入れ替える（無効化は呼び出し側で確認を出す）。
   * @param user 対象ユーザー。
   */
  onToggleActive: (user: AdminUserListItem) => void;
};

/**
 * テナントユーザーの一覧。取得中・空・失敗は呼び出し側に残す。
 * 行の中の操作は小さいボタンにし、無効化は赤枠・有効化は紺にする（緑は見出しの作成だけ）。
 * 各行から実行リソース許可の画面へ進める。
 * @param props 行と操作コマンド。
 * @returns ユーザーのリスト。
 */
export function AdminUserList({
  users,
  onOpenPassword,
  onToggleActive
}: Readonly<AdminUserListProps>) {
  const uiText = useUiText();
  const text = uiText.admin.users;

  return (
    <ul className="divide-y divide-md-outline overflow-hidden rounded-card border border-md-outline bg-md-surface">
      {users.map((user) => (
        <li key={user.userId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-semibold text-md-on-surface">{user.username}</span>
              {user.isTenantAdmin ? (
                <span className="inline-flex h-5 items-center rounded-pill bg-md-primary-container px-2 text-[11px] font-semibold text-md-on-primary-container">
                  {text.adminBadge}
                </span>
              ) : null}
            </div>
            <p className="text-xs text-md-on-surface-variant">
              {user.email ? `${user.displayName} · ${user.email}` : user.displayName}
            </p>
            <p className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-md-on-surface-variant">
              <span className={user.isActive ? "text-md-success" : undefined}>
                {user.isActive ? text.active : text.inactive}
              </span>
              <span aria-hidden="true">·</span>
              <span>{text.groupCount(user.groupIds.length)}</span>
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link
              href={`/admin/users/${encodeURIComponent(user.userId)}/resource-grants`}
              aria-label={`${user.username}: ${text.resourceGrants}`}
              className={buttonClassName("outline", "sm")}
            >
              {text.resourceGrants}
            </Link>
            <Button variant="outline" size="sm" onClick={() => onOpenPassword(user)}>
              {text.updatePassword}
            </Button>
            <Button variant={user.isActive ? "danger" : "standard"} size="sm" onClick={() => onToggleActive(user)}>
              {user.isActive ? text.disable : text.enable}
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}
