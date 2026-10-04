"use client";

import type { FormEvent } from "react";
import {
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  PASSWORD_PATTERN,
  USER_EMAIL_MAX_LENGTH,
  USERNAME_MAX_LENGTH,
  USERNAME_PATTERN
} from "@/shared/auth/userIdentity";
import { useUiText } from "@/shared/i18n/uiTextContext";

/** ユーザー作成フォームの入力。 */
export type AdminUserCreateFormProps = {
  /** ユーザー名。 */
  username: string;
  /** メール。 */
  email: string;
  /** 初期パスワード。 */
  password: string;
  /** 表示名。 */
  displayName: string;
  /** テナント管理者として作成するか。 */
  isTenantAdmin: boolean;
  /** 作成送信中か。 */
  submitting: boolean;
  /**
   * ユーザー名の変更。
   * @param value 入力値。
   */
  onUsernameChange: (value: string) => void;
  /**
   * メールの変更。
   * @param value 入力値。
   */
  onEmailChange: (value: string) => void;
  /**
   * 初期パスワードの変更。
   * @param value 入力値。
   */
  onPasswordChange: (value: string) => void;
  /**
   * 表示名の変更。
   * @param value 入力値。
   */
  onDisplayNameChange: (value: string) => void;
  /**
   * テナント管理者フラグの変更。
   * @param value チェック状態。
   */
  onTenantAdminChange: (value: boolean) => void;
  /**
   * フォーム送信。
   * @param event フォームの submit。
   */
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
};

/**
 * テナントユーザーの作成フォーム。検証と API は呼び出し側に渡す。
 * @param props 入力値とコマンド。
 * @returns 作成フォーム。
 */
export function AdminUserCreateForm({
  username,
  email,
  password,
  displayName,
  isTenantAdmin,
  submitting,
  onUsernameChange,
  onEmailChange,
  onPasswordChange,
  onDisplayNameChange,
  onTenantAdminChange,
  onSubmit
}: Readonly<AdminUserCreateFormProps>) {
  const uiText = useUiText();

  return (
    <form
      onSubmit={onSubmit}
      className="rounded-xl border border-md-outline bg-md-surface p-4 shadow-sm"
    >
      <h2 className="mb-3 text-lg font-medium">{uiText.admin.users.createTitle}</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="admin-user-username" className="mb-1 block text-sm font-medium">
            {uiText.admin.users.usernameLabel}
          </label>
          <input
            id="admin-user-username"
            type="text"
            autoComplete="username"
            required
            maxLength={USERNAME_MAX_LENGTH}
            pattern={USERNAME_PATTERN}
            value={username}
            onChange={(event) => onUsernameChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label htmlFor="admin-user-email" className="mb-1 block text-sm font-medium">
            {uiText.admin.users.emailLabel}
          </label>
          <input
            id="admin-user-email"
            type="email"
            maxLength={USER_EMAIL_MAX_LENGTH}
            value={email}
            onChange={(event) => onEmailChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label htmlFor="admin-user-password" className="mb-1 block text-sm font-medium">
            {uiText.admin.users.passwordLabel}
          </label>
          <input
            id="admin-user-password"
            type="password"
            required
            minLength={PASSWORD_MIN_LENGTH}
            maxLength={PASSWORD_MAX_LENGTH}
            pattern={PASSWORD_PATTERN}
            title={uiText.admin.users.passwordPolicyHint}
            value={password}
            onChange={(event) => onPasswordChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
          <p className="mt-1 text-xs text-md-on-surface-variant">
            {uiText.admin.users.passwordPolicyHint}
          </p>
        </div>
        <div>
          <label htmlFor="admin-user-display" className="mb-1 block text-sm font-medium">
            {uiText.admin.users.displayNameLabel}
          </label>
          <input
            id="admin-user-display"
            type="text"
            value={displayName}
            onChange={(event) => onDisplayNameChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
        </div>
        <label className="flex items-center gap-2 self-end text-sm">
          <input
            type="checkbox"
            checked={isTenantAdmin}
            onChange={(event) => onTenantAdminChange(event.target.checked)}
          />
          {uiText.admin.users.isTenantAdminLabel}
        </label>
      </div>
      <button
        type="submit"
        disabled={submitting}
        className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
      >
        {submitting ? uiText.admin.users.creating : uiText.admin.users.createSubmit}
      </button>
    </form>
  );
}
