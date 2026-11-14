"use client";

import type { FormEvent } from "react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { TextField } from "@/shared/ui/TextField";
import { useUiText } from "@/shared/i18n/uiTextContext";
import type { NotificationSmtpFieldError } from "../notificationSmtp";
import {
  useAdminNotificationSmtpPage,
  type NotificationSmtpFailure
} from "../hooks/useAdminNotificationSmtpPage";

/**
 * テナント管理者向けの通知 SMTP 画面。
 * パスワードは再表示せず、保存は全体置換である。
 * @returns 表示、置換、削除。
 */
export function AdminNotificationSmtpPageClient() {
  const uiText = useUiText();
  const text = uiText.admin.notificationSmtp;
  const page = useAdminNotificationSmtpPage();
  const fieldMessage = fieldErrorMessage(text, page.fieldError);
  const failureMessage = failureText(text, page.failure, page.errorMessage);

  const onSave = (event: FormEvent) => {
    event.preventDefault();
    void page.save();
  };

  return (
    <PageShell
      title={text.title}
      description={text.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantSettings }, { label: text.title }]} />}
    >
      {failureMessage ? (
        <Notice
          tone="error"
          title={failureMessage}
          actions={
            page.failure === "load" ? (
              <Button variant="outline" size="sm" onClick={() => void page.reload()}>
                {text.retry}
              </Button>
            ) : undefined
          }
        />
      ) : null}
      <form onSubmit={onSave} className="flex flex-col gap-4 rounded-card border border-md-outline bg-md-surface p-5">
        <p className="text-sm text-md-on-surface">{page.configured ? text.configured : text.notConfigured}</p>
        {fieldMessage ? <Notice tone="error" title={fieldMessage} /> : null}
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField id="smtp-host" label={text.hostLabel} value={page.host} autoComplete="off" onChange={(event) => page.setHost(event.target.value)} />
          <TextField
            id="smtp-port"
            label={text.portLabel}
            value={page.port}
            inputMode="numeric"
            autoComplete="off"
            onChange={(event) => page.setPort(event.target.value)}
          />
          <TextField id="smtp-user" label={text.userLabel} value={page.user} autoComplete="off" onChange={(event) => page.setUser(event.target.value)} />
          <TextField
            id="smtp-password"
            label={text.passwordLabel}
            type="password"
            value={page.password}
            autoComplete="new-password"
            hint={page.passwordConfigured ? text.passwordConfigured : undefined}
            onChange={(event) => page.setPassword(event.target.value)}
          />
          <div className="sm:col-span-2">
            <TextField
              id="smtp-from"
              label={text.defaultFromLabel}
              value={page.defaultFrom}
              autoComplete="off"
              onChange={(event) => page.setDefaultFrom(event.target.value)}
            />
          </div>
        </div>
        <p className="text-xs text-md-on-surface-variant">{text.replaceHint}</p>
        <div className="flex flex-wrap justify-between gap-2">
          {page.configured ? (
            <Button variant="danger" disabled={page.pending !== null} loading={page.pending === "delete"} onClick={page.requestDelete}>
              {page.pending === "delete" ? text.deleting : text.delete}
            </Button>
          ) : (
            <span />
          )}
          <Button variant="lead" type="submit" disabled={page.pending !== null || page.loading} loading={page.pending === "save"}>
            {page.pending === "save" ? text.saving : text.save}
          </Button>
        </div>
      </form>
      <ConfirmDialog
        open={page.deleteOpen}
        title={text.deleteConfirmTitle}
        message={text.deleteConfirmBody}
        confirmLabel={text.deleteConfirm}
        cancelLabel={text.cancel}
        loading={page.pending !== null}
        onConfirm={() => void page.confirmDelete()}
        onCancel={page.cancelDelete}
      />
    </PageShell>
  );
}

/**
 * 契約違反の項目を画面文言へ写す。
 * @param text 通知 SMTP の辞書。
 * @param field 止めた項目。無いときは null。
 * @returns 表示文。
 */
function fieldErrorMessage(
  text: ReturnType<typeof useUiText>["admin"]["notificationSmtp"],
  field: NotificationSmtpFieldError | null
): string | null {
  if (field === null) {
    return null;
  }
  const messages: Record<NotificationSmtpFieldError, string> = {
    host: text.hostInvalid,
    port: text.portInvalid,
    user: text.userInvalid,
    passwordPair: text.passwordPairInvalid,
    passwordBytes: text.passwordBytesInvalid,
    defaultFrom: text.defaultFromInvalid
  };
  return messages[field];
}

/**
 * 通信失敗を画面文言へ写す。API の message があればそれを優先し、パスワードは足さない。
 * @param text 通知 SMTP の辞書。
 * @param failure 失敗種別。
 * @param errorMessage API が返した message。
 * @returns 表示文。
 */
function failureText(
  text: ReturnType<typeof useUiText>["admin"]["notificationSmtp"],
  failure: NotificationSmtpFailure | null,
  errorMessage: string | null
): string | null {
  if (failure === null) {
    return null;
  }
  if (errorMessage) {
    return errorMessage;
  }
  const messages: Record<NotificationSmtpFailure, string> = {
    load: text.loadFailed,
    save: text.saveFailed,
    delete: text.deleteFailed
  };
  return messages[failure];
}
