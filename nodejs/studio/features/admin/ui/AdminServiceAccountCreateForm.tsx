"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { Notice } from "@/shared/ui/Notice";
import { Toast } from "@/shared/ui/Toast";
import { useServiceAccountCreateForm, type ServiceAccountCreatedHandler } from "../hooks/useServiceAccountCreateForm";
import { ServiceAccountGroupsField, ServiceAccountNameField } from "./ServiceAccountFormFields";

/** 作成フォームの `<form>` の id。ダイアログ下部の送信ボタンから `form` 属性で結ぶ。 */
const CREATE_FORM_ID = "service-account-create-form";

type AdminServiceAccountCreateFormProps = {
  /** ダイアログが開いているか。 */
  open: boolean;
  /** ダイアログを閉じる。 */
  onClose: () => void;
  /** 作成成功後の後処理。ダイアログを閉じることと一覧の再取得は呼び出し側が行う。 */
  onCreated: ServiceAccountCreatedHandler;
};

/**
 * 資格のない ServiceAccount の作成ダイアログ。名前とグループだけを入力し、API キーの入力は置かない。
 * 一覧の見出しの「サービスアカウントを作成」から開く（studio-ui-visual-refresh の U5）。
 * 送信前の検証で止めた項目は欄の下に、サーバーの 422 などはダイアログの上部に出す。
 * グループの候補はダイアログを閉じても保ち、開くたびに取り直さない。
 * @param props 開閉と作成後の後処理。
 * @returns 作成ダイアログ。
 */
export function AdminServiceAccountCreateForm({ open, onClose, onCreated }: Readonly<AdminServiceAccountCreateFormProps>) {
  const uiText = useUiText();
  const text = uiText.admin.serviceAccounts;
  const form = useServiceAccountCreateForm(onCreated);
  const canSubmit = !form.submitting && form.groups !== null && form.groups.length > 0;

  return (
    <>
      <Toast toast={form.toast} onClose={form.clearToast} />
      <Dialog
        open={open}
        onClose={onClose}
        title={text.createTitle}
        description={text.createDescription}
        size="lg"
        dismissible={!form.submitting}
        footer={
          <>
            <Button variant="outline" onClick={onClose} disabled={form.submitting}>
              {uiText.actions.cancel}
            </Button>
            <Button variant="lead" type="submit" form={CREATE_FORM_ID} disabled={!canSubmit} loading={form.submitting}>
              {form.submitting ? text.creating : text.createSubmit}
            </Button>
          </>
        }
      >
        <form id={CREATE_FORM_ID} noValidate onSubmit={(event) => void form.submit(event)} className="flex flex-col gap-4">
          {form.submitError === null ? null : <Notice tone="error" title={form.submitError} />}
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
        </form>
      </Dialog>
    </>
  );
}
