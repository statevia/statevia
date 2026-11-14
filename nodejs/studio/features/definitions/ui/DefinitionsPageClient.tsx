"use client";

import { Plus, Search } from "lucide-react";
import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { DefinitionsListQuery, SortOrder } from "@/features/definitions/api";
import { useDefinitionCatalogActions } from "@/features/definitions/hooks/useDefinitionCatalogActions";
import { useDefinitionsList } from "@/features/definitions/hooks/useDefinitionsList";
import { useDefinitionsListQuery } from "@/features/definitions/hooks/useDefinitionsListQuery";
import { isDeletedDefinition, type DefinitionDTO } from "@/features/definitions/types";
import { useProject } from "@/shared/project/projectContext";
import { canPublish } from "@/shared/project/roles";
import { DEFINITIONS_WRITE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { matchesPattern } from "@/shared/lib/validation/primitives";
import { SEARCH_NAME_PATTERN } from "@/shared/lib/validation/searchRules";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button, buttonClassName } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { DataList, type DataListColumn } from "@/shared/ui/DataList";
import { FilterChips, type FilterChip } from "@/shared/ui/FilterChips";
import { inputClassName } from "@/shared/ui/FormField";
import { ListPagination } from "@/shared/ui/ListPagination";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";

/** 並び順の選択肢。項目と順序の組を 1 つの選択欄で選ぶ。 */
const SORT_OPTIONS: ReadonlyArray<{ sortBy: "createdAt" | "name"; sortOrder: SortOrder }> = [
  { sortBy: "createdAt", sortOrder: "desc" },
  { sortBy: "createdAt", sortOrder: "asc" },
  { sortBy: "name", sortOrder: "asc" },
  { sortBy: "name", sortOrder: "desc" },
];

/** 検索欄のエラーの要素 ID（`aria-describedby` 用）。 */
const SEARCH_ERROR_ID = "definitions-search-error";

type DefinitionRowActionsProps = Readonly<{
  /** 行の定義。 */
  definition: DefinitionDTO;
  /** 作成・削除・復元ができるか（公開以上のロールと definitions.write）。 */
  canManageCatalog: boolean;
  /** 削除か復元を送っている最中か。 */
  busy: boolean;
  /** 削除中の定義の表示用 ID。 */
  deletingId: string | null;
  /** 復元中の定義の表示用 ID。 */
  restoringId: string | null;
  /** 削除の確認を開く。 */
  onRequestDelete: (displayId: string) => void;
  /** 復元の確認を開く。 */
  onRequestRestore: (displayId: string) => void;
}>;

/**
 * 行の操作。削除済みでない定義は詳細と削除、削除済みの定義は復元を出す。
 * @param props 行の定義と操作。
 * @returns 操作ボタン群。
 */
function DefinitionRowActions({
  definition,
  canManageCatalog,
  busy,
  deletingId,
  restoringId,
  onRequestDelete,
  onRequestRestore,
}: DefinitionRowActionsProps) {
  const actionsText = useI18n().uiText.definitionsPage.actions;
  if (isDeletedDefinition(definition)) {
    return canManageCatalog ? (
      <Button variant="standard" size="sm" disabled={busy} onClick={() => onRequestRestore(definition.displayId)}>
        {restoringId === definition.displayId ? actionsText.restoring : actionsText.restore}
      </Button>
    ) : null;
  }
  return (
    <div className="flex flex-nowrap gap-2 whitespace-nowrap">
      <Link href={`/definitions/${encodeURIComponent(definition.displayId)}`} className={buttonClassName("outline", "sm")}>
        {actionsText.openDetail}
      </Link>
      {canManageCatalog ? (
        <Button variant="danger" size="sm" disabled={busy} onClick={() => onRequestDelete(definition.displayId)}>
          {deletingId === definition.displayId ? actionsText.deleting : actionsText.delete}
        </Button>
      ) : null}
    </div>
  );
}

/**
 * 並び順の選択肢の文言。
 * @param option 項目と順序。
 * @param text 定義一覧の辞書。
 * @returns 例: 「作成日時（降順）」。
 */
function sortOptionLabel(
  option: (typeof SORT_OPTIONS)[number],
  text: ReturnType<typeof useI18n>["uiText"]["definitionsPage"],
): string {
  const by = option.sortBy === "name" ? text.sortByName : text.sortByCreatedAt;
  const order = option.sortOrder === "asc" ? text.sortOrderAsc : text.sortOrderDesc;
  return `${by}（${order}）`;
}

/**
 * Definition 一覧（検索・並び順・ページング・catalog 論理削除/復元）を表示する。
 * 条件は URL に反映し（`useDefinitionsListQuery`）、かけている条件はチップで出す（要件 8）。
 * 削除は定義名の入力を求める確認ダイアログ（U6）、復元は確認ダイアログを経てから送る。
 */
export function DefinitionsPageClient() {
  const { uiText, locale } = useI18n();
  const text = uiText.definitionsPage;
  const dateTimeLocale = getDateTimeLocale(locale);
  const [searchInput, setSearchInput] = useState("");
  const [searchError, setSearchError] = useState<string | null>(null);
  const {
    listQuery,
    currentPage,
    effectiveSortBy,
    effectiveSortOrder,
    includeDeleted,
    goTo,
  } = useDefinitionsListQuery();
  const { selectedProject } = useProject();
  const permissions = usePermissions();
  // 作成・削除・復元は、選択中 Project の公開以上とグループ権限 definitions.write の両方を API が要求する。
  const canManageCatalog =
    canPublish(selectedProject?.role) && permissions.has(DEFINITIONS_WRITE_PERMISSION);
  const scopedQuery = useMemo(
    () => ({ ...listQuery, projectId: selectedProject?.projectId }),
    [listQuery, selectedProject],
  );
  const {
    items,
    totalCount,
    loading,
    toast,
    setToast,
    loadDefinitions,
    hasPrev,
    hasNext,
    empty,
  } = useDefinitionsList(scopedQuery);
  const {
    pendingConfirm,
    setPendingConfirm,
    deletingId,
    restoringId,
    handleDeleteClick,
    handleRestoreClick,
  } = useDefinitionCatalogActions({
    reload: loadDefinitions,
    setToast,
    deletedMessage: text.toasts.deleted,
    restoredMessage: text.toasts.restored,
  });

  useEffect(() => {
    setSearchInput(listQuery.name ?? "");
  }, [listQuery.name]);

  /**
   * 条件を変えて 1 ページ目から読み直す。並び順を変えたときも 1 ページ目に戻す（要件 8.3）。
   * @param next 変える条件。
   */
  const goToFirstPage = useCallback(
    (next: Partial<DefinitionsListQuery>) =>
      goTo({ ...listQuery, ...next, pagination: { ...listQuery.pagination, offset: 0 } }),
    [goTo, listQuery],
  );

  const handleSubmitSearch = useCallback(
    (event: React.FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      const trimmedKeyword = searchInput.trim();
      if (!matchesPattern(trimmedKeyword, SEARCH_NAME_PATTERN)) {
        setSearchError(text.search.invalidName);
        return;
      }
      setSearchError(null);
      goToFirstPage({ name: trimmedKeyword || undefined });
    },
    [goToFirstPage, searchInput, text.search.invalidName],
  );

  const nameChips: FilterChip[] = listQuery.name
    ? [
        {
          id: "name",
          label: text.nameChip(listQuery.name),
          onRemove: () => {
            setSearchInput("");
            goToFirstPage({ name: undefined });
          },
        },
      ]
    : [];
  const includeDeletedChips: FilterChip[] = includeDeleted
    ? [
        {
          id: "includeDeleted",
          label: text.includeDeleted.label,
          onRemove: () => goToFirstPage({ includeDeleted: undefined }),
        },
      ]
    : [];

  const paginationNav = (
    <ListPagination
      ariaLabel={text.pagination.ariaLabel}
      currentPageLabel={text.pagination.currentPage(currentPage)}
      hasPrev={hasPrev}
      hasNext={hasNext}
      onPrev={() =>
        goTo({
          ...listQuery,
          pagination: {
            ...listQuery.pagination,
            offset: Math.max(0, listQuery.pagination.offset - listQuery.pagination.limit),
          },
        })
      }
      onNext={() =>
        goTo({
          ...listQuery,
          pagination: {
            ...listQuery.pagination,
            offset: listQuery.pagination.offset + listQuery.pagination.limit,
          },
        })
      }
    />
  );

  const busy = deletingId !== null || restoringId !== null;
  const pendingDefinition =
    pendingConfirm === null
      ? undefined
      : items?.find((definition) => definition.displayId === pendingConfirm.displayId);

  const columns: DataListColumn<DefinitionDTO>[] = [
    {
      id: "name",
      header: text.columns.name,
      width: "minmax(0,1fr)",
      hideLabelOnMobile: true,
      cell: (definition) => (
        <>
          <p className="flex min-w-0 items-center gap-2 font-medium text-md-on-surface" title={definition.name}>
            {isDeletedDefinition(definition) ? (
              <span className="truncate">{definition.name}</span>
            ) : (
              <Link
                href={`/definitions/${encodeURIComponent(definition.displayId)}`}
                className="truncate hover:text-md-primary hover:underline"
              >
                {definition.name}
              </Link>
            )}
            {isDeletedDefinition(definition) ? (
              <span className="inline-flex h-5 shrink-0 items-center rounded-pill bg-md-surface-container-high px-2 text-[11px] font-semibold text-md-on-surface-variant">
                {text.deletedBadge}
              </span>
            ) : null}
          </p>
          <p className="mt-0.5 truncate font-mono text-xs text-md-on-surface-variant">{definition.displayId}</p>
        </>
      ),
    },
    {
      id: "createdAt",
      header: text.columns.createdAt,
      width: "11rem",
      cell: (definition) => (
        <>
          <p className="whitespace-nowrap text-md-on-surface">
            {formatDateTimeLocalized(definition.createdAt, dateTimeLocale)}
          </p>
          {isDeletedDefinition(definition) && definition.deletedAt ? (
            <p className="mt-0.5 whitespace-nowrap text-xs text-md-on-surface-variant">
              {text.deletedAt(formatDateTimeLocalized(definition.deletedAt, dateTimeLocale))}
            </p>
          ) : null}
        </>
      ),
    },
    {
      id: "actions",
      header: text.columns.actions,
      width: "12rem",
      align: "end",
      hideLabelOnMobile: true,
      cell: (definition) => (
        <DefinitionRowActions
          definition={definition}
          canManageCatalog={canManageCatalog}
          busy={busy}
          deletingId={deletingId}
          restoringId={restoringId}
          onRequestDelete={(displayId) => setPendingConfirm({ kind: "delete", displayId })}
          onRequestRestore={(displayId) => setPendingConfirm({ kind: "restore", displayId })}
        />
      ),
    },
  ];

  return (
    <PageShell
      title={uiText.lists.definitions}
      description={text.description}
      breadcrumbs={
        <Breadcrumbs items={[{ label: uiText.navigation.groups.workflow }, { label: uiText.lists.definitions }]} />
      }
      primaryActions={
        canManageCatalog ? (
          <Link href="/definitions/new" className={buttonClassName("lead")}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            {text.actions.createNew}
          </Link>
        ) : null
      }
    >
      <Toast toast={toast} onClose={() => setToast(null)} />

      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-start gap-3">
          <form className="flex min-w-[260px] flex-1 items-start gap-2" onSubmit={handleSubmitSearch}>
            <div className="relative flex-1">
              <Search
                aria-hidden="true"
                className="pointer-events-none absolute left-3 top-4 h-4 w-4 text-md-on-surface-variant"
              />
              <input
                type="search"
                className={inputClassName({ error: searchError !== null, paddingX: "pl-9 pr-3", className: "h-9" })}
                value={searchInput}
                onChange={(event) => {
                  setSearchInput(event.target.value);
                  setSearchError(null);
                }}
                placeholder={text.search.placeholder}
                aria-label={text.search.label}
                aria-invalid={searchError ? true : undefined}
                aria-describedby={searchError ? SEARCH_ERROR_ID : undefined}
              />
              {searchError ? (
                <span id={SEARCH_ERROR_ID} role="alert" className="mt-1 block text-xs text-md-error">
                  {searchError}
                </span>
              ) : null}
            </div>
            <Button variant="standard" type="submit" className="mt-1.5" disabled={loading}>
              {text.search.submit}
            </Button>
          </form>
          <label className="mt-1.5 flex h-9 items-center gap-2 text-sm text-md-on-surface-variant">
            {text.sortLabel}
            <select
              className="h-9 rounded-control border border-md-outline-variant bg-md-surface-container px-3 text-sm text-md-on-surface"
              value={`${effectiveSortBy}:${effectiveSortOrder}`}
              onChange={(event) => {
                const selected = SORT_OPTIONS.find(
                  (option) => `${option.sortBy}:${option.sortOrder}` === event.target.value,
                );
                if (selected) goToFirstPage({ sort: { sortBy: selected.sortBy, sortOrder: selected.sortOrder } });
              }}
            >
              {SORT_OPTIONS.map((option) => (
                <option key={`${option.sortBy}:${option.sortOrder}`} value={`${option.sortBy}:${option.sortOrder}`}>
                  {sortOptionLabel(option, text)}
                </option>
              ))}
            </select>
          </label>
          <label className="mt-1.5 flex h-9 items-center gap-2 text-sm text-md-on-surface">
            <input
              type="checkbox"
              className="h-4 w-4 accent-brand-lead-bg"
              checked={includeDeleted}
              onChange={(event) => goToFirstPage({ includeDeleted: event.target.checked || undefined })}
              disabled={loading}
            />
            {text.includeDeleted.label}
          </label>
        </div>
        <FilterChips
          chips={[...nameChips, ...includeDeletedChips]}
          onClearAll={() => {
            setSearchInput("");
            goToFirstPage({ name: undefined, includeDeleted: undefined });
          }}
        />
      </div>

      {loading && <PageState state="loading" message={text.loading} />}

      {empty && <PageState state="empty" message={text.emptyNoMatch} />}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label={uiText.lists.definitions} className="flex flex-col gap-3">
          <DataList
            ariaLabel={uiText.lists.definitions}
            columns={columns}
            items={items}
            getRowKey={(definition) => definition.displayId}
          />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-xs text-md-on-surface-variant">{text.listSummary(totalCount ?? 0, currentPage)}</p>
            {paginationNav}
          </div>
        </section>
      )}

      {!loading && items === null && !toast && (
        <PageState state="error" message={text.error} onRetry={() => void loadDefinitions()} />
      )}

      <ConfirmDialog
        open={pendingConfirm?.kind === "delete" && pendingDefinition !== undefined}
        title={text.deleteConfirm.title(pendingDefinition?.name ?? "")}
        message={text.deleteConfirm.message}
        confirmLabel={text.actions.confirmDelete}
        cancelLabel={text.actions.cancelConfirm}
        requireTypedName={pendingDefinition?.name}
        loading={deletingId !== null}
        onConfirm={() => {
          if (pendingDefinition) handleDeleteClick(pendingDefinition.displayId);
        }}
        onCancel={() => setPendingConfirm(null)}
      />
      <ConfirmDialog
        open={pendingConfirm?.kind === "restore" && pendingDefinition !== undefined}
        title={text.restoreConfirm.title(pendingDefinition?.name ?? "")}
        message={text.restoreConfirm.message}
        confirmLabel={text.actions.confirmRestore}
        cancelLabel={text.actions.cancelConfirm}
        confirmVariant="standard"
        loading={restoringId !== null}
        onConfirm={() => {
          if (pendingDefinition) handleRestoreClick(pendingDefinition.displayId);
        }}
        onCancel={() => setPendingConfirm(null)}
      />
    </PageShell>
  );
}
