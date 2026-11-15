"use client";

import type { FormEvent } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { TextField } from "@/shared/ui/TextField";
import { resolvePermissionLabel } from "../permissionLabel";
import type { PermissionDefinitionDto } from "../types";

/**
 * チェックボックス用の HTML id。権限キーの `.` などは置換する。
 * @param prefix id の接頭辞。
 * @param key 権限キー。
 * @returns HTML id。
 */
function checkboxInputId(prefix: string, key: string): string {
  return `${prefix}-${key.replaceAll(/[^a-zA-Z0-9_-]/g, "-")}`;
}

/** 発行フォームの `<form>` の id。ダイアログ下部の送信ボタンから `form` 属性で結ぶ。 */
const CREATE_FORM_ID = "admin-api-key-create-form";

/** API キー発行ダイアログの入力。 */
export type AdminApiKeyCreateFormProps = {
  /** ダイアログが開いているか。 */
  open: boolean;
  /** ダイアログを閉じる。 */
  onClose: () => void;
  /** 表示名。 */
  name: string;
  /** 有効期限の datetime-local 値。 */
  expiresAt: string;
  /** 選択中のスコープ。 */
  selectedScopes: ReadonlySet<string>;
  /** 選べる権限。 */
  assignablePermissions: readonly PermissionDefinitionDto[];
  /** 発行送信中か。 */
  submitting: boolean;
  /**
   * 表示名の変更。
   * @param value 入力値。
   */
  onNameChange: (value: string) => void;
  /**
   * 有効期限の変更。
   * @param value datetime-local の値。
   */
  onExpiresAtChange: (value: string) => void;
  /**
   * スコープの選択切替。
   * @param permissionKey 権限キー。
   */
  onToggleScope: (permissionKey: string) => void;
  /**
   * フォーム送信。
   * @param event フォームの submit。
   */
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
};

/**
 * API キーの発行ダイアログ。一覧の見出しの「API キーを発行」から開く（studio-ui-visual-refresh の U5）。
 * 検証と API は呼び出し側に渡す。権限を 1 つも選んでいないと発行できない。
 * @param props 開閉・入力値・コマンド。
 * @returns 発行ダイアログ。
 */
export function AdminApiKeyCreateForm({
  open,
  onClose,
  name,
  expiresAt,
  selectedScopes,
  assignablePermissions,
  submitting,
  onNameChange,
  onExpiresAtChange,
  onToggleScope,
  onSubmit
}: Readonly<AdminApiKeyCreateFormProps>) {
  const uiText = useUiText();
  const text = uiText.admin.apiKeys;

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
            {uiText.actions.cancel}
          </Button>
          <Button variant="lead" type="submit" form={CREATE_FORM_ID} loading={submitting} disabled={selectedScopes.size === 0}>
            {submitting ? text.creating : text.createSubmit}
          </Button>
        </>
      }
    >
      <form id={CREATE_FORM_ID} onSubmit={onSubmit} className="flex flex-col gap-4">
        <TextField label={text.nameLabel} type="text" required value={name} onChange={(event) => onNameChange(event.target.value)} />
        <TextField
          label={text.expiresAtLabel}
          type="datetime-local"
          hint={text.expiresAtHint}
          value={expiresAt}
          onChange={(event) => onExpiresAtChange(event.target.value)}
        />
        <fieldset>
          <legend className="mb-2 text-sm font-medium after:ml-1 after:text-md-error after:content-['*']">{text.scopesTitle}</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {assignablePermissions.map((permission) => (
              <label key={permission.permissionKey} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  id={checkboxInputId("api-key-scope", permission.permissionKey)}
                  checked={selectedScopes.has(permission.permissionKey)}
                  onChange={() => onToggleScope(permission.permissionKey)}
                />
                {resolvePermissionLabel(permission, uiText.admin.permissions.names)}
              </label>
            ))}
          </div>
        </fieldset>
      </form>
    </Dialog>
  );
}
