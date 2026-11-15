"use client";

import { Play } from "lucide-react";
import Link from "next/link";
import { useDefinitionDetail } from "@/features/definitions/hooks/useDefinitionDetail";
import { useProject } from "@/shared/project/projectContext";
import { canExecute, canPublish } from "@/shared/project/roles";
import {
  DEFINITIONS_WRITE_PERMISSION,
  EXECUTIONS_WRITE_PERMISSION,
  usePermissions,
} from "@/shared/auth/permissionsContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button, buttonClassName } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { Toast } from "@/shared/ui/Toast";

type DefinitionDetailClientProps = {
  definitionId: string;
};

/**
 * Definition 詳細（API のメタ情報、実行一覧・編集・実行開始・論理削除への導線）を表示する。
 * 見出しの右に「定義の編集」（紺）・「新規実行を開始」（緑の主導線）・「定義を削除」（赤枠）を置く。
 * 削除は定義名の入力を求める確認ダイアログを経てから送る（U6）。
 */
export function DefinitionDetailClient({ definitionId }: Readonly<DefinitionDetailClientProps>) {
  const { uiText, locale } = useI18n();
  const text = uiText.definitionDetail;
  const dateTimeLocale = getDateTimeLocale(locale);
  const {
    row,
    notFound,
    loading,
    showLoading,
    toast,
    setToast,
    confirmDelete,
    setConfirmDelete,
    deleting,
    handleDeleteClick,
  } = useDefinitionDetail(definitionId, {
    deletedMessage: text.toasts.deleted,
  });
  const { roleOf, loaded: projectsLoaded } = useProject();
  const permissions = usePermissions();
  const role = row ? roleOf(row.projectId) : null;
  // 編集・論理削除・実行開始は、Project ロールとグループ権限の両方を API が要求する。
  // 編集と論理削除は同じ条件（公開以上 + definitions.write）。
  const showEdit = projectsLoaded && canPublish(role) && permissions.has(DEFINITIONS_WRITE_PERMISSION);
  const showRun = projectsLoaded && canExecute(role) && permissions.has(EXECUTIONS_WRITE_PERMISSION);
  const encodedId = encodeURIComponent(definitionId);

  /** 削除の 1 回目（確認を開く）と 2 回目（確定）。Promise はここで吸収し、表示は hook 内トーストに任せる。 */
  const onDeleteClick = () => {
    handleDeleteClick().catch(() => undefined);
  };

  const actions = row ? (
    <>
      {showEdit ? (
        <Link href={`/definitions/${encodedId}/edit`} className={buttonClassName("standard")}>
          {text.actions.edit}
        </Link>
      ) : null}
      {showRun ? (
        <Link href={`/definitions/${encodedId}/run`} className={buttonClassName("lead")}>
          <Play aria-hidden="true" className="h-4 w-4" />
          {text.actions.run}
        </Link>
      ) : null}
      {showEdit ? (
        <Button variant="danger" onClick={onDeleteClick} disabled={deleting || loading}>
          {deleting ? text.actions.deleting : text.actions.delete}
        </Button>
      ) : null}
    </>
  ) : null;

  return (
    <PageShell
      title={row?.name ?? text.title}
      breadcrumbs={
        <Breadcrumbs
          items={[
            { label: uiText.navigation.groups.workflow },
            { label: uiText.lists.definitions, href: "/definitions" },
            { label: row?.name ?? definitionId },
          ]}
        />
      }
      primaryActions={actions}
    >
      <Toast toast={toast} onClose={() => setToast(null)} />

      {showLoading && (
        <output className="block text-sm text-md-on-surface-variant" aria-live="polite">
          {uiText.actions.loading}
        </output>
      )}

      {!loading && notFound && <Notice tone="info" title={text.notMember} />}

      {!loading && !row && toast && !notFound && <Notice tone="error" title={text.errorFetchFailed} />}

      {row && projectsLoaded && permissions.loaded && !showEdit && !showRun && (
        <Notice tone="info" title={text.roleDenied} />
      )}

      {!loading && row && (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <Card title={text.relatedExecutions.title}>
            <p className="text-sm text-md-on-surface-variant">{text.relatedExecutions.description}</p>
            <div className="mt-3">
              <Link
                href={`/executions?definitionId=${encodedId}`}
                className={buttonClassName("outline", "sm")}
              >
                {text.relatedExecutions.openList}
              </Link>
            </div>
          </Card>
          <Card title={text.ariaMeta}>
            <dl
              aria-label={text.ariaMeta}
              className="grid grid-cols-[minmax(6rem,auto)_1fr] gap-x-3 gap-y-2.5 text-sm text-md-on-surface"
            >
              <dt className="text-md-on-surface-variant">{text.meta.name}</dt>
              <dd className="font-medium break-all">{row.name}</dd>
              <dt className="text-md-on-surface-variant">{uiText.labels.displayId}</dt>
              <dd className="font-mono break-all">{row.displayId}</dd>
              <dt className="text-md-on-surface-variant">{uiText.labels.resourceId}</dt>
              <dd className="font-mono text-xs break-all">{row.resourceId}</dd>
              <dt className="text-md-on-surface-variant">{text.meta.createdAt}</dt>
              <dd>{formatDateTimeLocalized(row.createdAt, dateTimeLocale)}</dd>
            </dl>
          </Card>
        </div>
      )}

      <ConfirmDialog
        open={row !== null && showEdit && confirmDelete}
        title={text.deleteConfirm.title(row?.name ?? "")}
        message={text.deleteConfirm.message}
        confirmLabel={text.actions.confirmDelete}
        cancelLabel={text.actions.cancelConfirm}
        requireTypedName={row?.name}
        loading={deleting}
        onConfirm={onDeleteClick}
        onCancel={() => setConfirmDelete(false)}
      />
    </PageShell>
  );
}
