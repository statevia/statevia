"use client";

import { useMemo } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { resolvePermissionLabel } from "../permissionLabel";
import type { AdminApiKeyListItem, PermissionDefinitionDto } from "../types";

/**
 * ISO 日時をローカル表示用に整形する。解釈できない値はそのまま返す。
 * @param value ISO 日時。無ければ null。
 * @returns 表示文字列。空入力は null。
 */
function formatDateTime(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}

/** API キー一覧の行。 */
export type AdminApiKeyListProps = {
  /** 現在の API キー。空や失敗は呼び出し側が出す。 */
  apiKeys: readonly AdminApiKeyListItem[];
  /** 権限カタログ。許可スコープを権限名で出すのに使う。カタログに無い権限キーはキーのまま出す。 */
  permissions: readonly PermissionDefinitionDto[];
  /** 失効中の API キー ID。無ければ null。 */
  revokingId: string | null;
  /**
   * 有効なキーの失効を求める（確認ダイアログは呼び出し側が出す）。
   * @param apiKey 対象キー。
   */
  onRevoke: (apiKey: AdminApiKeyListItem) => void;
};

/**
 * API キーの一覧。取得中・空・失敗は呼び出し側に残す。
 * @param props 行と失効コマンド。
 * @returns API キーのリスト。
 */
export function AdminApiKeyList({
  apiKeys,
  permissions,
  revokingId,
  onRevoke
}: Readonly<AdminApiKeyListProps>) {
  const uiText = useUiText();
  const permissionNames = uiText.admin.permissions.names;
  const scopeLabels = useMemo(
    () =>
      new Map(
        permissions.map((permission) => [permission.permissionKey, resolvePermissionLabel(permission, permissionNames)])
      ),
    [permissions, permissionNames]
  );

  return (
    <ul className="divide-y divide-md-outline overflow-hidden rounded-card border border-md-outline bg-md-surface">
      {apiKeys.map((apiKey) => (
        <li
          key={apiKey.apiKeyId}
          className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
        >
          <div className="min-w-0">
            <p className="font-semibold text-md-on-surface">{apiKey.name}</p>
            <p className="text-md-on-surface-variant">
              {uiText.admin.apiKeys.prefixLabel(apiKey.keyPrefix)}
            </p>
            <p className="text-xs text-md-on-surface-variant">
              {apiKey.isActive ? uiText.admin.apiKeys.active : uiText.admin.apiKeys.inactive}
              {` · ${apiKey.allowedScopes.map((scope) => scopeLabels.get(scope) ?? scope).join(uiText.admin.apiKeys.scopeSeparator)}`}
            </p>
            <p className="text-xs text-md-on-surface-variant">
              {uiText.admin.apiKeys.createdLabel(formatDateTime(apiKey.createdAt) ?? apiKey.createdAt)}
              {` · ${
                apiKey.expiresAt
                  ? uiText.admin.apiKeys.expiresLabel(formatDateTime(apiKey.expiresAt) ?? apiKey.expiresAt)
                  : uiText.admin.apiKeys.noExpiry
              }`}
              {` · ${
                apiKey.lastUsedAt
                  ? uiText.admin.apiKeys.lastUsedLabel(formatDateTime(apiKey.lastUsedAt) ?? apiKey.lastUsedAt)
                  : uiText.admin.apiKeys.neverUsed
              }`}
            </p>
          </div>
          {apiKey.isActive ? (
            <Button variant="danger" size="sm" loading={revokingId === apiKey.apiKeyId} onClick={() => onRevoke(apiKey)}>
              {revokingId === apiKey.apiKeyId ? uiText.admin.apiKeys.revoking : uiText.admin.apiKeys.revoke}
            </Button>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
