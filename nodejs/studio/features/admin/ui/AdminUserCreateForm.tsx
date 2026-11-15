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
import { Button } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { TextField } from "@/shared/ui/TextField";

/** 作成フォームの `<form>` の id。ダイアログ下部の送信ボタンから `form` 属性で結ぶ。 */
const CREATE_FORM_ID = "admin-user-create-form";

/** ユーザー作成ダイアログの入力。 */
export type AdminUserCreateFormProps = {
  /** ダイアログが開いているか。 */
  open: boolean;
  /** ダイアログを閉じる。 */
  onClose: () => void;
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
 * テナントユーザーの作成ダイアログ。一覧の見出しの「ユーザーを作成」から開く（studio-ui-visual-refresh の U5）。
 * 検証と API は呼び出し側に渡す。送信中は閉じさせない。
 * @param props 開閉・入力値・コマンド。
 * @returns 作成ダイアログ。
 */
export function AdminUserCreateForm({
  open,
  onClose,
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
  const text = uiText.admin.users;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={text.createTitle}
      size="lg"
      dismissible={!submitting}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={submitting}>
            {text.cancel}
          </Button>
          <Button variant="lead" type="submit" form={CREATE_FORM_ID} loading={submitting}>
            {submitting ? text.creating : text.createSubmit}
          </Button>
        </>
      }
    >
      <form id={CREATE_FORM_ID} onSubmit={onSubmit} className="flex flex-col gap-4">
        <TextField
          label={text.usernameLabel}
          type="text"
          autoComplete="username"
          required
          maxLength={USERNAME_MAX_LENGTH}
          pattern={USERNAME_PATTERN}
          value={username}
          onChange={(event) => onUsernameChange(event.target.value)}
        />
        <TextField
          label={text.emailLabel}
          type="email"
          maxLength={USER_EMAIL_MAX_LENGTH}
          value={email}
          onChange={(event) => onEmailChange(event.target.value)}
        />
        <TextField
          label={text.displayNameLabel}
          type="text"
          value={displayName}
          onChange={(event) => onDisplayNameChange(event.target.value)}
        />
        <TextField
          label={text.passwordLabel}
          type="password"
          autoComplete="new-password"
          required
          minLength={PASSWORD_MIN_LENGTH}
          maxLength={PASSWORD_MAX_LENGTH}
          pattern={PASSWORD_PATTERN}
          title={text.passwordPolicyHint}
          hint={text.passwordPolicyHint}
          value={password}
          onChange={(event) => onPasswordChange(event.target.value)}
        />
        <label className="flex items-center gap-2 rounded-card border border-md-outline px-3.5 py-3 text-sm">
          <input type="checkbox" checked={isTenantAdmin} onChange={(event) => onTenantAdminChange(event.target.checked)} />
          {text.isTenantAdminLabel}
        </label>
      </form>
    </Dialog>
  );
}
