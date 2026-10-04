"use client";

import { Suspense, useState } from "react";
import { ListPagination } from "@/shared/ui/ListPagination";
import { OPERATION_TEXT_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { Toast } from "@/shared/ui/Toast";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { useI18n, useUiText } from "@/shared/i18n/uiTextContext";
import { useExecutionsPage } from "../hooks/useExecutionsPage";
import { ExecutionsFilterForm } from "./ExecutionsFilterForm";
import { ExecutionsList } from "./ExecutionsList";
import { SubscribeIngressDialog } from "./SubscribeIngressDialog";

/**
 * ページング・フィルタ（URL 同期）付きの実行一覧。詳細は <code>/executions/[id]</code> へ遷移する。
 */
function ExecutionsPageClientInner() {
  const { uiText } = useI18n();
  const page = useExecutionsPage();
  const [ingressOpen, setIngressOpen] = useState(false);
  const paginationLabels = uiText.executionsPage.pagination;
  const paginationProps = {
    ariaLabel: paginationLabels.ariaLabel,
    currentPageLabel: paginationLabels.currentPage(page.currentPage),
    hasPrev: page.hasPrev,
    hasNext: page.hasNext,
    prevLabel: paginationLabels.prev,
    nextLabel: paginationLabels.next,
    onPrev: page.goToPreviousPage,
    onNext: page.goToNextPage
  };

  return (
    <PageShell
      title={uiText.lists.executions}
      primaryActions={
        <button
          type="button"
          className="rounded border-2 border-brand-cta-border bg-brand-cta-bg px-4 py-2 text-sm font-medium text-brand-cta-fg hover:bg-brand-cta-bg-hover"
          onClick={() => setIngressOpen(true)}
        >
          {uiText.executionsPage.ingress.open}
        </button>
      }
    >
      <SubscribeIngressDialog
        open={ingressOpen}
        onClose={() => setIngressOpen(false)}
        onAccepted={() => {
          void page.reload().then((loaded) => {
            if (loaded) {
              page.notifyIngressAccepted();
            }
          });
        }}
      />

      {page.definitionId && (
        <output className="block rounded border border-md-primary bg-md-primary-container px-3 py-2 text-sm text-md-on-primary-container" aria-live="polite">
          <span className="text-md-on-primary-container">{uiText.executionsPage.filter.contextActivePrefix} </span>
          <span className="font-mono break-all">{page.definitionId}</span>
          <button
            type="button"
            className={`ml-2 ${OPERATION_TEXT_BUTTON_CLASS}`}
            onClick={page.clearDefinitionContext}
          >
            {uiText.executionsPage.filter.clearDefinition}
          </button>
        </output>
      )}

      <ExecutionsFilterForm
        status={page.status}
        sortBy={page.sortBy}
        sortOrder={page.sortOrder}
        nameDraft={page.nameDraft}
        definitionDraft={page.definitionDraft}
        loading={page.loading}
        limit={page.limit}
        offset={page.offset}
        currentPage={page.currentPage}
        onSubmit={page.submitFilters}
        onStatusChange={page.changeStatus}
        onNameDraftChange={page.setNameDraft}
        onDefinitionDraftChange={page.setDefinitionDraft}
        onSortByChange={page.changeSortBy}
        onSortOrderChange={page.changeSortOrder}
        onClear={page.clearFilters}
      />

      <Toast toast={page.toast} onClose={page.dismissToast} />

      {page.loading && (
        <PageState state="loading" message={uiText.executionsPage.loading} />
      )}

      {!page.loading && page.items !== null && page.items.length > 0 && (
        <section aria-label={uiText.lists.executions}>
          <div className="mb-2 flex items-center justify-between gap-3">
            <p className="text-xs text-md-on-surface-variant">
              {uiText.executionsPage.listSummary(page.totalCount ?? 0, page.currentPage)}
            </p>
            <ListPagination {...paginationProps} />
          </div>
          <ExecutionsList items={page.items} onOpen={page.openExecution} />
          <div className="mt-2 flex justify-end">
            <ListPagination {...paginationProps} />
          </div>
        </section>
      )}

      {!page.loading && page.items !== null && page.items.length === 0 && (
        <PageState state="empty" message={uiText.executionsPage.empty} />
      )}

      {!page.loading && !page.toast && page.items === null && (
        <PageState
          state="error"
          message={uiText.executionsPage.error}
          onRetry={() => void page.reload()}
        />
      )}
    </PageShell>
  );
}

/**
 * ページング付き実行一覧（検索パラメータ用に `useSearchParams` 利用箇所を `Suspense` で包む）。
 */
export function ExecutionsPageClient() {
  const uiText = useUiText();
  return (
    <Suspense
      fallback={
        <div className="p-6 text-sm text-md-on-surface-variant" aria-live="polite">
          {uiText.actions.loading}
        </div>
      }
    >
      <ExecutionsPageClientInner />
    </Suspense>
  );
}
