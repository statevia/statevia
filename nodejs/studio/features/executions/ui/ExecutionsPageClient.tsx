"use client";

import { useState } from "react";
import { EXECUTIONS_WRITE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { FilterChips, type FilterChip } from "@/shared/ui/FilterChips";
import { ListPagination } from "@/shared/ui/ListPagination";
import { Toast } from "@/shared/ui/Toast";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { useExecutionsPage } from "../hooks/useExecutionsPage";
import { ExecutionsFilterForm } from "./ExecutionsFilterForm";
import { ExecutionsList } from "./ExecutionsList";
import { SubscribeIngressDialog } from "./SubscribeIngressDialog";

/**
 * ページング・フィルタ（URL 同期）付きの実行一覧。詳細は <code>/executions/[id]</code> へ遷移する。
 * 実行 ID と定義 ID の条件はチップで出し、1 つずつでも全部でも外せる（要件 8.4）。状態はピルで選ぶ。
 * @remarks `useSearchParams` を使うが `Suspense` で包まない。ルートの layout が `cookies()` を読むので全 route が動的に描画され、
 * 包まなくても静的な事前描画で止まらない。包むと、URL に検索パラメータがあるとき、流し込んだ中身（`<div hidden id="S:0">`）が差し替わらずに DOM に残っていた。
 */
export function ExecutionsPageClient() {
  const { uiText } = useI18n();
  const page = useExecutionsPage();
  const [ingressOpen, setIngressOpen] = useState(false);
  // POST /v1/events は executions.write を要求する。
  const canSendEvents = usePermissions().has(EXECUTIONS_WRITE_PERMISSION);
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

  const filterText = uiText.executionsPage.filter;
  const nameChips: FilterChip[] = page.name
    ? [{ id: "name", label: filterText.nameChip(page.name), onRemove: page.clearName }]
    : [];
  const definitionChips: FilterChip[] = page.definitionId
    ? [{ id: "definitionId", label: filterText.definitionChip(page.definitionId), onRemove: page.clearDefinitionContext }]
    : [];

  return (
    <PageShell
      title={uiText.lists.executions}
      description={uiText.executionsPage.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.workflow }, { label: uiText.lists.executions }]} />}
      primaryActions={
        canSendEvents && (
          <Button variant="standard" onClick={() => setIngressOpen(true)}>
            {uiText.executionsPage.ingress.open}
          </Button>
        )
      }
    >
      <SubscribeIngressDialog
        open={canSendEvents && ingressOpen}
        onClose={() => setIngressOpen(false)}
        onAccepted={() => {
          void page.reload().then((loaded) => {
            if (loaded) {
              page.notifyIngressAccepted();
            }
          });
        }}
      />

      <ExecutionsFilterForm
        status={page.status}
        counts={page.counts}
        sortBy={page.sortBy}
        sortOrder={page.sortOrder}
        nameDraft={page.nameDraft}
        definitionDraft={page.definitionDraft}
        nameError={page.nameError}
        definitionError={page.definitionError}
        loading={page.loading}
        onSubmit={page.submitFilters}
        onStatusChange={page.changeStatus}
        onNameDraftChange={page.setNameDraft}
        onDefinitionDraftChange={page.setDefinitionDraft}
        onSortChange={page.changeSort}
      />
      <FilterChips chips={[...nameChips, ...definitionChips]} onClearAll={page.clearFilters} />

      <Toast toast={page.toast} onClose={page.dismissToast} />

      {page.loading && (
        <PageState state="loading" message={uiText.executionsPage.loading} />
      )}

      {!page.loading && page.items !== null && page.items.length > 0 && (
        <section aria-label={uiText.lists.executions} className="flex flex-col gap-3">
          <ExecutionsList items={page.items} />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-xs text-md-on-surface-variant">
              {uiText.executionsPage.listSummary(page.totalCount ?? 0, page.currentPage)}
            </p>
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
