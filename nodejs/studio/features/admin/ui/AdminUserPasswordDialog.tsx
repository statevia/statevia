"use client";

import type { FormEvent } from "react";
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH, PASSWORD_PATTERN } from "@/shared/auth/userIdentity";
import { useUiText } from "@/shared/i18n/uiTextContext";
import type { AdminUserListItem } from "../types";

/** 管理者によるパスワード更新ダイアログ。 */
export type AdminUserPasswordDialogProps = {
  /** 更新対象。 */
  user: AdminUserListItem;
  /** 新しいパスワード。 */
  newPassword: string;
  /** 確認用パスワード。 */
  confirmPassword: string;
  /** 確認欄が一致しないか。 */
  passwordMismatch: boolean;
  /** 送信中か。 */
  submitting: boolean;
  /**
   * 新しいパスワードの変更。
   * @param value 入力値。
   */
  onNewPasswordChange: (value: string) => void;
  /**
   * 確認用パスワードの変更。
   * @param value 入力値。
   */
  onConfirmPasswordChange: (value: string) => void;
  /** ダイアログを閉じる。 */
  onClose: () => void;
  /**
   * フォーム送信。
   * @param event ダイアログの submit。
   */
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
};

/**
 * ユーザーのパスワードを更新するダイアログ。開いているときだけ呼び出す。
 * @param props 入力値とコマンド。
 * @returns パスワード更新ダイアログ。
 */
export function AdminUserPasswordDialog({
  user,
  newPassword,
  confirmPassword,
  passwordMismatch,
  submitting,
  onNewPasswordChange,
  onConfirmPasswordChange,
  onClose,
  onSubmit
}: Readonly<AdminUserPasswordDialogProps>) {
  const uiText = useUiText();

  return (
    <dialog
      open
      className="fixed inset-0 z-50 m-0 flex h-full max-h-none w-full max-w-none items-center justify-center border-0 bg-black/40 p-4"
      aria-labelledby="admin-password-title"
    >
      <form
        onSubmit={onSubmit}
        className="w-full max-w-md space-y-4 rounded-xl border border-md-outline bg-md-surface p-6 shadow-lg"
      >
        <h2 id="admin-password-title" className="text-lg font-medium">
          {uiText.admin.users.updatePasswordTitle}
        </h2>
        <p className="text-sm text-md-on-surface-variant">{user.username}</p>
        <div>
          <label htmlFor="admin-user-new-password" className="mb-1 block text-sm font-medium">
            {uiText.admin.users.newPasswordLabel}
          </label>
          <input
            id="admin-user-new-password"
            type="password"
            autoComplete="new-password"
            required
            minLength={PASSWORD_MIN_LENGTH}
            maxLength={PASSWORD_MAX_LENGTH}
            pattern={PASSWORD_PATTERN}
            title={uiText.admin.users.passwordPolicyHint}
            value={newPassword}
            onChange={(event) => onNewPasswordChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
          <p className="mt-1 text-xs text-md-on-surface-variant">
            {uiText.admin.users.passwordPolicyHint}
          </p>
        </div>
        <div>
          <label htmlFor="admin-user-confirm-password" className="mb-1 block text-sm font-medium">
            {uiText.admin.users.confirmPasswordLabel}
          </label>
          <input
            id="admin-user-confirm-password"
            type="password"
            autoComplete="new-password"
            required
            minLength={PASSWORD_MIN_LENGTH}
            maxLength={PASSWORD_MAX_LENGTH}
            pattern={PASSWORD_PATTERN}
            title={uiText.admin.users.passwordPolicyHint}
            value={confirmPassword}
            onChange={(event) => onConfirmPasswordChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
        </div>
        {passwordMismatch ? (
          <p className="text-sm text-red-700" role="alert">
            {uiText.admin.users.passwordMismatch}
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-md-outline px-3 py-1.5 text-sm hover:bg-md-surface-container"
          >
            {uiText.admin.users.cancel}
          </button>
          <button
            type="submit"
            disabled={submitting}
            className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
          >
            {submitting
              ? uiText.admin.users.updatingPassword
              : uiText.admin.users.updatePasswordSubmit}
          </button>
        </div>
      </form>
    </dialog>
  );
}
