"use client";

import { useState } from "react";
import { apiPut } from "@/shared/api";
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH, PASSWORD_PATTERN } from "@/shared/auth/userIdentity";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { PageShell } from "@/shared/ui/PageShell";
import { TextField } from "@/shared/ui/TextField";
import { Toast } from "@/shared/ui/Toast";

/**
 * 現行パスワード確認後に自分のパスワードを更新する画面。
 */
export function AccountSettingsPageClient() {
  const uiText = useUiText();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [mismatch, setMismatch] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * 確認欄が一致するときだけ本人パスワード更新 API を呼ぶ。
   * @param event フォームの submit イベント
   */
  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      setMismatch(true);
      setToast(null);
      return;
    }

    setMismatch(false);
    setSubmitting(true);
    setToast(null);
    try {
      await apiPut("/auth/me/password", { currentPassword, newPassword });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setToast({ tone: "success", message: uiText.auth.account.success });
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <PageShell title={uiText.auth.account.title} description={uiText.auth.account.description}>
      <form
        onSubmit={(event) => {
          void handleSubmit(event);
        }}
        className="flex max-w-md flex-col gap-4 rounded-card border border-md-outline bg-md-surface p-6"
      >
        <TextField
          id="account-current-password"
          type="password"
          label={uiText.auth.account.currentPasswordLabel}
          autoComplete="current-password"
          required
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
        />
        <TextField
          id="account-new-password"
          type="password"
          label={uiText.auth.account.newPasswordLabel}
          autoComplete="new-password"
          required
          minLength={PASSWORD_MIN_LENGTH}
          maxLength={PASSWORD_MAX_LENGTH}
          pattern={PASSWORD_PATTERN}
          title={uiText.auth.account.passwordPolicyHint}
          hint={uiText.auth.account.passwordPolicyHint}
          value={newPassword}
          onChange={(e) => {
            setNewPassword(e.target.value);
            setMismatch(false);
          }}
        />
        <TextField
          id="account-confirm-password"
          type="password"
          label={uiText.auth.account.confirmPasswordLabel}
          autoComplete="new-password"
          required
          minLength={PASSWORD_MIN_LENGTH}
          maxLength={PASSWORD_MAX_LENGTH}
          pattern={PASSWORD_PATTERN}
          title={uiText.auth.account.passwordPolicyHint}
          value={confirmPassword}
          error={mismatch ? uiText.auth.account.passwordMismatch : undefined}
          onChange={(e) => {
            setConfirmPassword(e.target.value);
            setMismatch(false);
          }}
        />
        <div className="flex justify-end border-t border-md-outline-variant pt-4">
          <Button variant="lead" type="submit" loading={submitting}>
            {submitting ? uiText.auth.account.submitting : uiText.auth.account.submit}
          </Button>
        </div>
      </form>
      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
    </PageShell>
  );
}
