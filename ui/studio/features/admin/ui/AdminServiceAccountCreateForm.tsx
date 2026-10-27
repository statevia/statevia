"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { ASCII_LABEL_DEFAULT_MAX_LENGTH } from "@/shared/lib/validation/formRules";
import { Toast } from "@/shared/ui/Toast";
import {
  useServiceAccountCreateForm,
  type ServiceAccountCreateFormModel,
  type ServiceAccountCreatedHandler
} from "../hooks/useServiceAccountCreateForm";
import type { ServiceAccountFieldErrorKind } from "../serviceAccounts";

type AdminServiceAccountCreateFormProps = {
  /** 作成成功後の後処理。一覧の再取得は呼び出し側が行う。 */
  onCreated: ServiceAccountCreatedHandler;
};

type GroupsFieldProps = {
  form: ServiceAccountCreateFormModel;
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
 * 所属グループの複数選択。読込失敗と 0 件はそれぞれ文言を出す。
 * @param props フォームの状態。
 * @returns グループ選択欄。
 */
function GroupsField({ form }: Readonly<GroupsFieldProps>) {
  const text = useUiText().admin.serviceAccounts;
  const groupsError = form.fieldErrors.groupIds;

  if (form.groupsLoadFailed) {
    return (
      <p className="text-sm text-md-error" role="alert">
        {text.groupsLoadFailed}
      </p>
    );
  }
  if (form.groups === null) return null;
  if (form.groups.length === 0) {
    return <p className="text-sm text-md-on-surface-variant">{text.noGroups}</p>;
  }
  return (
    <fieldset aria-describedby={groupsError === undefined ? undefined : "service-account-create-groups-error"}>
      <legend className="mb-2 text-sm font-medium">{text.groupsLabel}</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        {form.groups.map((group) => (
          <label key={group.groupId} className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.selectedGroupIds.has(group.groupId)}
              onChange={() => form.toggleGroup(group.groupId)}
            />
            {group.name}
          </label>
        ))}
      </div>
      {groupsError === undefined ? null : (
        <p id="service-account-create-groups-error" className="mt-1 text-xs text-md-error">
          {text.groupsRequired}
        </p>
      )}
    </fieldset>
  );
}

/**
 * 資格のない ServiceAccount の作成フォーム。名前とグループだけを入力し、API キーの入力は置かない。
 * 送信前の検証で止めた項目は欄の下に、サーバーの 422 などはフォーム上部に出す。
 * @param props 作成後の後処理。
 * @returns 作成フォーム。
 */
export function AdminServiceAccountCreateForm({ onCreated }: Readonly<AdminServiceAccountCreateFormProps>) {
  const text = useUiText().admin.serviceAccounts;
  const form = useServiceAccountCreateForm(onCreated);
  const nameError = form.fieldErrors.name;
  const canSubmit = !form.submitting && form.groups !== null && form.groups.length > 0;

  return (
    <>
      <Toast toast={form.toast} onClose={form.clearToast} />
      <form
        noValidate
        onSubmit={(event) => void form.submit(event)}
        className="mb-4 rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs"
      >
        <h2 className="mb-1 text-lg font-medium">{text.createTitle}</h2>
        <p className="mb-3 text-sm text-md-on-surface-variant">{text.createDescription}</p>
        {form.submitError === null ? null : (
          <p className="mb-3 text-sm text-md-error" role="alert">
            {form.submitError}
          </p>
        )}
        <div className="mb-4">
          <label htmlFor="service-account-create-name" className="mb-1 block text-sm font-medium">
            {text.nameLabel}
          </label>
          <input
            id="service-account-create-name"
            type="text"
            autoComplete="off"
            value={form.name}
            onChange={(event) => form.setName(event.target.value)}
            aria-invalid={nameError !== undefined}
            aria-describedby={
              nameError === undefined ? "service-account-create-name-hint" : "service-account-create-name-error"
            }
            className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
          />
          {nameError === undefined ? (
            <p id="service-account-create-name-hint" className="mt-1 text-xs text-md-on-surface-variant">
              {text.nameHint}
            </p>
          ) : (
            <p id="service-account-create-name-error" className="mt-1 text-xs text-md-error">
              {nameErrorMessage(nameError, text)}
            </p>
          )}
        </div>
        <GroupsField form={form} />
        <button
          type="submit"
          disabled={!canSubmit}
          className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {form.submitting ? text.creating : text.createSubmit}
        </button>
      </form>
    </>
  );
}
