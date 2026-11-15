"use client";

import type { FormEvent } from "react";
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH, PASSWORD_PATTERN } from "@/shared/auth/userIdentity";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { TextField } from "@/shared/ui/TextField";
import type { AdminUserListItem } from "../types";

/** パスワード更新フォームの `<form>` の id。ダイアログ下部の送信ボタンから結ぶ。 */
const PASSWORD_FORM_ID = "admin-user-password-form";

/** パスワード更新ダイアログの入力。 */
export type AdminUserPasswordDialogProps = {
  /** 対象ユーザー。 */
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
   * 更新の送信。
   * @param event フォームの submit。
   */
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
};

/**
 * ユーザーのパスワード更新ダイアログ。一致の検証と API は呼び出し側に渡す。
 * @param props 対象・入力値・コマンド。
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
  const text = uiText.admin.users;

  return (
    <Dialog
      open
      onClose={onClose}
      title={text.updatePasswordTitle}
      description={user.username}
      dismissible={!submitting}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={submitting}>
            {text.cancel}
          </Button>
          <Button variant="lead" type="submit" form={PASSWORD_FORM_ID} loading={submitting}>
            {submitting ? text.updatingPassword : text.updatePasswordSubmit}
          </Button>
        </>
      }
    >
      <form id={PASSWORD_FORM_ID} onSubmit={onSubmit} className="flex flex-col gap-4">
        <TextField
          label={text.newPasswordLabel}
          type="password"
          autoComplete="new-password"
          required
          minLength={PASSWORD_MIN_LENGTH}
          maxLength={PASSWORD_MAX_LENGTH}
          pattern={PASSWORD_PATTERN}
          title={text.passwordPolicyHint}
          hint={text.passwordPolicyHint}
          value={newPassword}
          onChange={(event) => onNewPasswordChange(event.target.value)}
        />
        <TextField
          label={text.confirmPasswordLabel}
          type="password"
          autoComplete="new-password"
          required
          minLength={PASSWORD_MIN_LENGTH}
          maxLength={PASSWORD_MAX_LENGTH}
          pattern={PASSWORD_PATTERN}
          title={text.passwordPolicyHint}
          error={passwordMismatch ? text.passwordMismatch : undefined}
          value={confirmPassword}
          onChange={(event) => onConfirmPasswordChange(event.target.value)}
        />
      </form>
    </Dialog>
  );
}
