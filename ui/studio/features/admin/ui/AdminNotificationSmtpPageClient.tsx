"use client";

import type { FormEvent } from "react";
import { PageShell } from "@/shared/ui/PageShell";
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
  const text = useUiText().admin.notificationSmtp;
  const page = useAdminNotificationSmtpPage();
  const fieldMessage = fieldErrorMessage(text, page.fieldError);
  const failureMessage = failureText(text, page.failure, page.errorMessage);

  const onSave = (event: FormEvent) => {
    event.preventDefault();
    void page.save();
  };

  return (
    <PageShell title={text.title} description={text.description}>
      {failureMessage ? (
        <p className="mb-4 text-sm text-red-700" role="alert">
          {failureMessage}
        </p>
      ) : null}
      {page.failure === "load" ? (
        <button
          type="button"
          onClick={() => void page.reload()}
          className="mb-4 rounded-lg border border-md-outline px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container"
        >
          {text.retry}
        </button>
      ) : null}
      <form onSubmit={onSave} className="rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs">
        <p className="mb-3 text-sm">{page.configured ? text.configured : text.notConfigured}</p>
        {fieldMessage ? (
          <p className="mb-3 text-sm text-red-700" role="alert">
            {fieldMessage}
          </p>
        ) : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block text-sm font-medium" htmlFor="smtp-host">
            {text.hostLabel}
            <input
              id="smtp-host"
              value={page.host}
              autoComplete="off"
              onChange={(event) => page.setHost(event.target.value)}
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
          <label className="block text-sm font-medium" htmlFor="smtp-port">
            {text.portLabel}
            <input
              id="smtp-port"
              value={page.port}
              inputMode="numeric"
              autoComplete="off"
              onChange={(event) => page.setPort(event.target.value)}
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
          <label className="block text-sm font-medium" htmlFor="smtp-user">
            {text.userLabel}
            <input
              id="smtp-user"
              value={page.user}
              autoComplete="off"
              onChange={(event) => page.setUser(event.target.value)}
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
          <label className="block text-sm font-medium" htmlFor="smtp-password">
            {text.passwordLabel}
            <input
              id="smtp-password"
              type="password"
              value={page.password}
              autoComplete="new-password"
              onChange={(event) => page.setPassword(event.target.value)}
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
          <label className="block text-sm font-medium sm:col-span-2" htmlFor="smtp-from">
            {text.defaultFromLabel}
            <input
              id="smtp-from"
              value={page.defaultFrom}
              autoComplete="off"
              onChange={(event) => page.setDefaultFrom(event.target.value)}
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
        </div>
        {page.passwordConfigured ? <p className="mt-3 text-xs">{text.passwordConfigured}</p> : null}
        <p className="mt-3 text-xs">{text.replaceHint}</p>
        <div className="mt-4 flex gap-2">
          <button
            type="submit"
            disabled={page.pending !== null || page.loading}
            className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
          >
            {page.pending === "save" ? text.saving : text.save}
          </button>
          {page.configured ? (
            <button
              type="button"
              disabled={page.pending !== null}
              onClick={page.requestDelete}
              className="rounded-lg border border-md-outline px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container disabled:opacity-60"
            >
              {page.pending === "delete" ? text.deleting : text.delete}
            </button>
          ) : null}
        </div>
      </form>
      {page.deleteOpen ? (
        <div
          role="dialog"
          aria-labelledby="smtp-delete-title"
          className="mt-4 rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs"
        >
          <h2 id="smtp-delete-title" className="mb-2 text-lg font-medium">
            {text.deleteConfirmTitle}
          </h2>
          <p className="mb-4 text-sm">{text.deleteConfirmBody}</p>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page.pending !== null}
              onClick={() => void page.confirmDelete()}
              className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
            >
              {text.deleteConfirm}
            </button>
            <button
              type="button"
              disabled={page.pending !== null}
              onClick={page.cancelDelete}
              className="rounded-lg border border-md-outline px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container disabled:opacity-60"
            >
              {text.cancel}
            </button>
          </div>
        </div>
      ) : null}
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
