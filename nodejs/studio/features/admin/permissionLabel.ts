import type { PermissionDefinitionDto } from "./types";

/**
 * 権限カタログ 1 件の表示名を決める。
 * API の `displayKey` は i18n 辞書のキーであり、画面にそのまま出さない。
 * 辞書に `displayKey` があればその語を使い、無ければ（`displayKey` が null・未知のキーのとき）API の `displayLabel` を出す。
 * @param permission 権限カタログ 1 件。
 * @param names 現在の言語の権限名の辞書（`uiText.admin.permissions.names`）。
 * @returns 画面に出す権限名。
 */
export function resolvePermissionLabel(
  permission: Pick<PermissionDefinitionDto, "displayKey" | "displayLabel">,
  names: Readonly<Record<string, string>>
): string {
  const { displayKey, displayLabel } = permission;
  return displayKey != null && Object.hasOwn(names, displayKey) ? names[displayKey] : displayLabel;
}
