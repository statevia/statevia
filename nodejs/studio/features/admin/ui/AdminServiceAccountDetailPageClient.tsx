"use client";

import Link from "next/link";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button, buttonClassName } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useAdminServiceAccountDetail, type AdminServiceAccountDetailModel } from "../hooks/useAdminServiceAccountDetail";
import type { AdminServiceAccount } from "../types";
import { ServiceAccountGroupsField, ServiceAccountNameField } from "./ServiceAccountFormFields";

type AdminServiceAccountDetailPageClientProps = {
  /** 対象の ServiceAccount ID。 */
  serviceAccountId: string;
};

type EditFormProps = {
  /** 詳細の状態。 */
  detail: AdminServiceAccountDetailModel;
  /** 現在値。 */
  serviceAccount: AdminServiceAccount;
};

/**
 * 有効・名前・所属の編集フォーム。キー付きの行も同じフォームで、削除ボタンは置かない。
 * @param props 詳細の状態と現在値。
 * @returns 編集フォーム。
 */
function EditForm({ detail, serviceAccount }: Readonly<EditFormProps>) {
  const text = useI18n().uiText.admin.serviceAccounts;

  return (
    <Card title={text.editTitle}>
      <form noValidate onSubmit={(event) => void detail.submit(event)} className="flex flex-col gap-4">
        {detail.submitError === null ? null : <Notice tone="error" title={detail.submitError} />}
        {detail.unchanged ? (
          <output className="block text-sm text-md-on-surface-variant">{text.unchanged}</output>
        ) : null}
        <div>
          <label className="flex items-center gap-2 text-sm font-medium">
            <input
              type="checkbox"
              checked={detail.isActive}
              onChange={(event) => detail.setIsActive(event.target.checked)}
              aria-describedby="service-account-detail-active-note"
            />
            {text.activeLabel}
          </label>
          <p id="service-account-detail-active-note" className="mt-1 text-xs text-md-on-surface-variant">
            {text.deactivationNote}
          </p>
          {serviceAccount.hasApiKey ? (
            <p className="mt-1 text-xs text-md-on-surface-variant">{text.apiKeyNote}</p>
          ) : null}
        </div>
        <ServiceAccountNameField
          id="service-account-detail-name"
          value={detail.name}
          onChange={detail.setName}
          error={detail.fieldErrors.name}
        />
        <ServiceAccountGroupsField
          idPrefix="service-account-detail"
          groups={detail.groups}
          loadFailed={detail.groupsLoadFailed}
          selectedGroupIds={detail.selectedGroupIds}
          onToggle={detail.toggleGroup}
          error={detail.fieldErrors.groupIds}
        />
        <div className="flex justify-end">
          <Button variant="lead" type="submit" loading={detail.saving}>
            {detail.saving ? text.saving : text.saveSubmit}
          </Button>
        </div>
      </form>
    </Card>
  );
}

/**
 * ServiceAccount 詳細。有効・表示名・所属を部分更新する。実行リソース許可の画面へ進める。
 * 404 は一覧へ戻るリンクだけを出し、それ以外の失敗は再試行できる。
 * テナント管理者以外は `app/admin/layout.tsx` が戻す。
 * @param props ServiceAccount ID。
 * @returns ServiceAccount 詳細画面。
 */
export function AdminServiceAccountDetailPageClient({
  serviceAccountId
}: Readonly<AdminServiceAccountDetailPageClientProps>) {
  const { locale, uiText } = useI18n();
  const text = uiText.admin.serviceAccounts;
  const detail = useAdminServiceAccountDetail(serviceAccountId);
  const serviceAccount = detail.serviceAccount;
  const backLink = (
    <Link href="/admin/service-accounts" className="hover:underline">
      {text.backToList}
    </Link>
  );
  const breadcrumbs = (currentLabel: string) => (
    <Breadcrumbs
      items={[
        { label: uiText.navigation.groups.tenantAccess },
        { label: text.title, href: "/admin/service-accounts" },
        { label: currentLabel }
      ]}
    />
  );

  switch (detail.loadState) {
    case "loading":
      return (
        <PageShell title={text.title} secondaryActions={backLink}>
          <PageState state="loading" />
        </PageShell>
      );
    case "notFound":
      return (
        <PageShell title={text.title} secondaryActions={backLink}>
          <PageState state="error" message={text.notFound} />
        </PageShell>
      );
    case "error":
      return (
        <PageShell title={text.title} secondaryActions={backLink}>
          <PageState
            state="error"
            message={text.detailLoadFailed}
            retryLabel={text.retry}
            onRetry={() => void detail.reload()}
          />
        </PageShell>
      );
    case "ready":
      break;
  }
  if (serviceAccount === null) return null;

  return (
    <PageShell
      title={serviceAccount.name}
      description={text.description}
      breadcrumbs={breadcrumbs(serviceAccount.name)}
      secondaryActions={backLink}
      primaryActions={
        <Link
          href={`/admin/service-accounts/${encodeURIComponent(serviceAccount.serviceAccountId)}/resource-grants`}
          className={buttonClassName("outline")}
        >
          {text.resourceGrants}
        </Link>
      }
    >
      <Toast toast={detail.toast} onClose={detail.clearToast} />
      <div className="space-y-1 text-sm text-md-on-surface-variant">
        <p>
          {text.principalIdLabel}: <span className="font-mono">{serviceAccount.principalId}</span>
        </p>
        <p>
          {serviceAccount.isActive ? text.active : text.inactive} ·{" "}
          {serviceAccount.hasApiKey ? text.hasApiKey : text.noApiKey}
        </p>
        <p>{text.createdLabel(formatDateTimeLocalized(serviceAccount.createdAt, getDateTimeLocale(locale)))}</p>
      </div>
      <EditForm detail={detail} serviceAccount={serviceAccount} />
    </PageShell>
  );
}
