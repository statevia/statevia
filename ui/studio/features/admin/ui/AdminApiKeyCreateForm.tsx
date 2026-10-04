"use client";

import type { FormEvent } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
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

/** API キー発行フォームの入力。 */
export type AdminApiKeyCreateFormProps = {
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
 * API キーの発行フォーム。検証と API は呼び出し側に渡す。
 * @param props 入力値とコマンド。
 * @returns 発行フォーム。
 */
export function AdminApiKeyCreateForm({
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

  return (
    <form
      onSubmit={onSubmit}
      className="rounded-xl border border-md-outline bg-md-surface p-4 shadow-sm"
    >
      <h2 className="mb-3 text-lg font-medium">{uiText.admin.apiKeys.createTitle}</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="admin-api-key-name" className="mb-1 block text-sm font-medium">
            {uiText.admin.apiKeys.nameLabel}
          </label>
          <input
            id="admin-api-key-name"
            type="text"
            required
            value={name}
            onChange={(event) => onNameChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label htmlFor="admin-api-key-expires" className="mb-1 block text-sm font-medium">
            {uiText.admin.apiKeys.expiresAtLabel}
          </label>
          <input
            id="admin-api-key-expires"
            type="datetime-local"
            value={expiresAt}
            onChange={(event) => onExpiresAtChange(event.target.value)}
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
          <p className="mt-1 text-xs text-md-on-surface-variant">
            {uiText.admin.apiKeys.expiresAtHint}
          </p>
        </div>
      </div>
      <fieldset className="mt-4">
        <legend className="mb-2 text-sm font-medium">{uiText.admin.apiKeys.scopesTitle}</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          {assignablePermissions.map((permission) => (
            <label key={permission.permissionKey} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                id={checkboxInputId("api-key-scope", permission.permissionKey)}
                checked={selectedScopes.has(permission.permissionKey)}
                onChange={() => onToggleScope(permission.permissionKey)}
              />
              {uiText.admin.permissions.label(permission.displayKey, permission.displayLabel)}
            </label>
          ))}
        </div>
      </fieldset>
      <button
        type="submit"
        disabled={submitting || selectedScopes.size === 0}
        className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
      >
        {submitting ? uiText.admin.apiKeys.creating : uiText.admin.apiKeys.createSubmit}
      </button>
    </form>
  );
}
