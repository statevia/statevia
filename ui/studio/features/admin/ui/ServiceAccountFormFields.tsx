"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { ASCII_LABEL_DEFAULT_MAX_LENGTH } from "@/shared/lib/validation/formRules";
import type { ServiceAccountFieldErrorKind } from "../serviceAccounts";
import type { AdminGroupListItem } from "../types";

type ServiceAccountNameFieldProps = {
  /** 入力欄の id。ヒントとエラーの id はこれに接尾辞を付ける。 */
  id: string;
  /** 入力値。 */
  value: string;
  /**
   * 入力値を更新する。
   * @param value 入力値。
   */
  onChange: (value: string) => void;
  /** 送信前に止めた項目エラー。無ければ undefined。 */
  error: ServiceAccountFieldErrorKind | undefined;
};

type ServiceAccountGroupsFieldProps = {
  /** エラー文の id に使う接頭辞。 */
  idPrefix: string;
  /** 選べるグループ。読込前と失敗時は null。 */
  groups: AdminGroupListItem[] | null;
  /** グループ一覧の取得に失敗したか。 */
  loadFailed: boolean;
  /** 選択中のグループ ID。 */
  selectedGroupIds: ReadonlySet<string>;
  /**
   * グループの選択を切り替える。
   * @param groupId グループ ID。
   */
  onToggle: (groupId: string) => void;
  /** 送信前に止めた項目エラー。無ければ undefined。 */
  error: ServiceAccountFieldErrorKind | undefined;
};

/**
 * 名前欄の項目エラー文。
 * @param kind エラー種別。
 * @param text ServiceAccount 画面の文言。
 * @returns 表示文。
 */
function nameErrorMessage(
  kind: ServiceAccountFieldErrorKind,
  text: ReturnType<typeof useUiText>["admin"]["serviceAccounts"]
): string {
  const messages: Record<ServiceAccountFieldErrorKind, string> = {
    required: text.nameRequired,
    tooLong: text.nameTooLong(ASCII_LABEL_DEFAULT_MAX_LENGTH),
    format: text.nameInvalidFormat
  };
  return messages[kind];
}

/**
 * ServiceAccount の名前欄（作成と詳細で共通）。エラーが無ければ ASCII ラベルのヒントを出す。
 * @param props 入力値と項目エラー。
 * @returns 名前欄。
 */
export function ServiceAccountNameField({ id, value, onChange, error }: Readonly<ServiceAccountNameFieldProps>) {
  const text = useUiText().admin.serviceAccounts;
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  return (
    <div className="mb-4">
      <label htmlFor={id} className="mb-1 block text-sm font-medium">
        {text.nameLabel}
      </label>
      <input
        id={id}
        type="text"
        autoComplete="off"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error !== undefined}
        aria-describedby={error === undefined ? hintId : errorId}
        className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
      />
      {error === undefined ? (
        <p id={hintId} className="mt-1 text-xs text-md-on-surface-variant">
          {text.nameHint}
        </p>
      ) : (
        <p id={errorId} className="mt-1 text-xs text-md-error">
          {nameErrorMessage(error, text)}
        </p>
      )}
    </div>
  );
}

/**
 * 所属グループの複数選択（作成と詳細で共通）。読込失敗と 0 件はそれぞれ文言を出す。
 * @param props グループ一覧、選択、項目エラー。
 * @returns グループ選択欄。
 */
export function ServiceAccountGroupsField({
  idPrefix,
  groups,
  loadFailed,
  selectedGroupIds,
  onToggle,
  error
}: Readonly<ServiceAccountGroupsFieldProps>) {
  const text = useUiText().admin.serviceAccounts;
  const errorId = `${idPrefix}-groups-error`;

  if (loadFailed) {
    return (
      <p className="text-sm text-md-error" role="alert">
        {text.groupsLoadFailed}
      </p>
    );
  }
  if (groups === null) return null;
  if (groups.length === 0) {
    return <p className="text-sm text-md-on-surface-variant">{text.noGroups}</p>;
  }
  return (
    <fieldset aria-describedby={error === undefined ? undefined : errorId}>
      <legend className="mb-2 text-sm font-medium">{text.groupsLabel}</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        {groups.map((group) => (
          <label key={group.groupId} className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={selectedGroupIds.has(group.groupId)}
              onChange={() => onToggle(group.groupId)}
            />
            {group.name}
          </label>
        ))}
      </div>
      {error === undefined ? null : (
        <p id={errorId} className="mt-1 text-xs text-md-error">
          {text.groupsRequired}
        </p>
      )}
    </fieldset>
  );
}
