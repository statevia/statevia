"use client";

import { useRouter } from "next/navigation";
import { useDefinitionDetail } from "@/features/definitions/hooks/useDefinitionDetail";
import { useProject } from "@/features/projects/projectContext";
import { canExecute, canPublish } from "@/features/projects/roles";
import {
  DEFINITIONS_WRITE_PERMISSION,
  EXECUTIONS_WRITE_PERMISSION,
  usePermissions,
} from "@/shared/auth/permissionsContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { NAVIGATION_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { Toast } from "@/shared/ui/Toast";

type DefinitionDetailClientProps = {
  definitionId: string;
};

/**
 * Definition 詳細（API のメタ情報、実行一覧・編集・実行開始・論理削除への導線）を表示する。
 */
export function DefinitionDetailClient({ definitionId }: Readonly<DefinitionDetailClientProps>) {
  const { uiText, locale } = useI18n();
  const router = useRouter();
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
    deletedMessage: uiText.definitionDetail.toasts.deleted,
  });
  const { roleOf, loaded: projectsLoaded } = useProject();
  const permissions = usePermissions();
  const role = row ? roleOf(row.projectId) : null;
  // 編集・論理削除・実行開始は、Project ロールとグループ権限の両方を API が要求する。
  // 編集と論理削除は同じ条件（公開以上 + definitions.write）。
  const showEdit = projectsLoaded && canPublish(role) && permissions.has(DEFINITIONS_WRITE_PERMISSION);
  const showRun = projectsLoaded && canExecute(role) && permissions.has(EXECUTIONS_WRITE_PERMISSION);

  /** 削除ボタン用。Promise はここで吸収し、表示は hook 内トーストに任せる。 */
  const onDeleteClick = () => {
    handleDeleteClick().catch(() => undefined);
  };

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-5 p-6">
      <header className="space-y-1">
        <h1 className="text-xl font-semibold text-md-on-surface">
          {uiText.definitionDetail.title}
        </h1>
        <p className="text-sm text-md-on-surface-variant">
          {uiText.definitionDetail.urlPrefix} <span className="font-mono">{definitionId}</span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      {showLoading && (
        <output className="block text-sm text-md-on-surface-variant" aria-live="polite">
          {uiText.actions.loading}
        </output>
      )}

      {!loading && notFound && (
        <p className="text-sm text-md-on-surface-variant">
          {uiText.definitionDetail.notMember}
        </p>
      )}

      {!loading && !row && toast && !notFound && (
        <p className="text-sm text-md-on-surface-variant">
          {uiText.definitionDetail.errorFetchFailed}
        </p>
      )}

      {!loading && row && (
        <section
          className="rounded-lg border border-md-outline bg-md-surface p-4 text-sm text-md-on-surface shadow-xs"
          aria-label={uiText.definitionDetail.ariaMeta}
        >
          <dl className="grid grid-cols-[minmax(7rem,auto)_1fr] gap-x-3 gap-y-2">
            <dt className="text-md-on-surface-variant">
              {uiText.definitionDetail.meta.name}
            </dt>
            <dd className="font-medium">{row.name}</dd>
            <dt className="text-md-on-surface-variant">{uiText.labels.displayId}</dt>
            <dd className="font-mono break-all">{row.displayId}</dd>
            <dt className="text-md-on-surface-variant">{uiText.labels.resourceId}</dt>
            <dd className="font-mono break-all">{row.resourceId}</dd>
            <dt className="text-md-on-surface-variant">
              {uiText.definitionDetail.meta.createdAt}
            </dt>
            <dd>{formatDateTimeLocalized(row.createdAt, dateTimeLocale)}</dd>
          </dl>
        </section>
      )}

      {row && (
        <section className="rounded-lg border border-amber-100 bg-amber-50/80 p-4 text-sm text-amber-950">
          <h2 className="font-medium text-amber-950">{uiText.definitionDetail.relatedExecutions.title}</h2>
          <p className="mt-1 text-amber-900/90">{uiText.definitionDetail.relatedExecutions.description}</p>
          <p className="mt-2">
            <button
              type="button"
              className={NAVIGATION_BUTTON_CLASS}
              onClick={() =>
                router.push(`/executions?definitionId=${encodeURIComponent(definitionId)}`)
              }
            >
              {uiText.definitionDetail.relatedExecutions.openList}
            </button>
          </p>
        </section>
      )}

      {row && (
      <section className="space-y-2 text-sm text-md-on-surface">
        {projectsLoaded && permissions.loaded && !showEdit && !showRun && (
          <p className="text-md-on-surface-variant">{uiText.definitionDetail.roleDenied}</p>
        )}
        <div className="flex flex-wrap items-center gap-3">
          {showEdit && (
            <button
              type="button"
              className={NAVIGATION_BUTTON_CLASS}
              onClick={() => router.push(`/definitions/${encodeURIComponent(definitionId)}/edit`)}
              disabled={deleting}
            >
              {uiText.definitionDetail.actions.edit}
            </button>
          )}
          {showRun && (
            <button
              type="button"
              className="rounded-sm border-2 border-brand-cta-border bg-brand-cta-bg px-3 py-1.5 text-sm font-medium text-brand-cta-fg hover:bg-brand-cta-bg-hover disabled:opacity-60"
              onClick={() => router.push(`/definitions/${encodeURIComponent(definitionId)}/run`)}
              disabled={deleting}
            >
              {uiText.definitionDetail.actions.run}
            </button>
          )}
          {row && showEdit && !confirmDelete && (
            <button
              type="button"
              className="rounded-sm border border-md-outline-variant bg-md-surface-container px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container-high disabled:opacity-60"
              onClick={onDeleteClick}
              disabled={deleting || loading}
            >
              {uiText.definitionDetail.actions.delete}
            </button>
          )}
          {row && showEdit && confirmDelete && (
            <>
              <button
                type="button"
                className="rounded-sm border border-red-700 bg-red-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60"
                onClick={onDeleteClick}
                disabled={deleting}
              >
                {deleting
                  ? uiText.definitionDetail.actions.deleting
                  : uiText.definitionDetail.actions.confirmDelete}
              </button>
              <button
                type="button"
                className="rounded-sm border border-md-outline-variant bg-md-surface-container px-3 py-1.5 text-sm text-md-on-surface"
                onClick={() => setConfirmDelete(false)}
                disabled={deleting}
              >
                {uiText.definitionDetail.actions.cancelConfirm}
              </button>
            </>
          )}
        </div>
      </section>
      )}
    </div>
  );
}
