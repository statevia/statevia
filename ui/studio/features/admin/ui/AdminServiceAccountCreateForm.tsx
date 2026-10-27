"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Toast } from "@/shared/ui/Toast";
import { useServiceAccountCreateForm, type ServiceAccountCreatedHandler } from "../hooks/useServiceAccountCreateForm";
import { ServiceAccountGroupsField, ServiceAccountNameField } from "./ServiceAccountFormFields";

type AdminServiceAccountCreateFormProps = {
  /** 作成成功後の後処理。一覧の再取得は呼び出し側が行う。 */
  onCreated: ServiceAccountCreatedHandler;
};

/**
 * 資格のない ServiceAccount の作成フォーム。名前とグループだけを入力し、API キーの入力は置かない。
 * 送信前の検証で止めた項目は欄の下に、サーバーの 422 などはフォーム上部に出す。
 * @param props 作成後の後処理。
 * @returns 作成フォーム。
 */
export function AdminServiceAccountCreateForm({ onCreated }: Readonly<AdminServiceAccountCreateFormProps>) {
  const text = useUiText().admin.serviceAccounts;
  const form = useServiceAccountCreateForm(onCreated);
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
        <ServiceAccountNameField
          id="service-account-create-name"
          value={form.name}
          onChange={form.setName}
          error={form.fieldErrors.name}
        />
        <ServiceAccountGroupsField
          idPrefix="service-account-create"
          groups={form.groups}
          loadFailed={form.groupsLoadFailed}
          selectedGroupIds={form.selectedGroupIds}
          onToggle={form.toggleGroup}
          error={form.fieldErrors.groupIds}
        />
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
