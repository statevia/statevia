"use client";

import type { FormEvent } from "react";
import { useI18n } from "@/shared/i18n/uiTextContext";
import type { SortOrder } from "@/features/executions/api";
import type { ExecutionSortBy, ExecutionStatusFilter } from "../hooks/useExecutionsPage";

/** 実行一覧フィルタ欄の入力。 */
export type ExecutionsFilterFormProps = {
  /** 状態セレクトの値。空文字はすべて。 */
  status: ExecutionStatusFilter;
  /** ソートキー。 */
  sortBy: ExecutionSortBy;
  /** ソート順。 */
  sortOrder: SortOrder;
  /** 名前の入力中の値。 */
  nameDraft: string;
  /** 定義 ID の入力中の値。 */
  definitionDraft: string;
  /** 検索ボタンを無効にするか。一覧取得中。 */
  loading: boolean;
  /** 1 ページの件数。 */
  limit: number;
  /** 現在ページの先頭オフセット。 */
  offset: number;
  /** 1 始まりのページ番号。 */
  currentPage: number;
  /**
   * フォーム送信。
   * @param event フォームの submit。
   */
  onSubmit: (event: FormEvent) => void;
  /**
   * 状態セレクトの変更。
   * @param value セレクトの値。
   */
  onStatusChange: (value: string) => void;
  /**
   * 名前入力の変更。
   * @param value 入力値。
   */
  onNameDraftChange: (value: string) => void;
  /**
   * 定義 ID 入力の変更。
   * @param value 入力値。
   */
  onDefinitionDraftChange: (value: string) => void;
  /**
   * ソートキーの変更。
   * @param value セレクトの値。
   */
  onSortByChange: (value: string) => void;
  /**
   * ソート順の変更。
   * @param value セレクトの値。
   */
  onSortOrderChange: (value: string) => void;
  /** フィルタを消す。 */
  onClear: () => void;
};

/**
 * 実行一覧のフィルタ欄。検証と URL 更新は呼び出し側のコマンドに渡す。
 * @param props 入力値とコマンド。
 * @returns フィルタフォーム。
 */
export function ExecutionsFilterForm({
  status,
  sortBy,
  sortOrder,
  nameDraft,
  definitionDraft,
  loading,
  limit,
  offset,
  currentPage,
  onSubmit,
  onStatusChange,
  onNameDraftChange,
  onDefinitionDraftChange,
  onSortByChange,
  onSortOrderChange,
  onClear
}: Readonly<ExecutionsFilterFormProps>) {
  const { uiText } = useI18n();
  const filterText = uiText.executionsPage.filter;

  return (
    <form onSubmit={onSubmit} className="space-y-3 rounded-lg border border-md-outline bg-md-surface p-4 shadow-sm">
      <h2 className="text-sm font-medium text-md-on-surface">{filterText.title}</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block text-sm text-md-on-surface">
          <span className="text-md-on-surface-variant">{uiText.labels.status}</span>
          <select
            className="mt-1 w-full rounded border border-md-outline-variant bg-md-surface-container px-2 py-1.5 text-sm text-md-on-surface"
            value={status}
            onChange={(event) => onStatusChange(event.target.value)}
          >
            <option value="">{filterText.all}</option>
            <option value="Running">{filterText.statusRunning}</option>
            <option value="Completed">{filterText.statusCompleted}</option>
            <option value="Cancelled">{filterText.statusCancelled}</option>
            <option value="Failed">{filterText.statusFailed}</option>
          </select>
        </label>
        <label className="block text-sm text-md-on-surface">
          <span className="text-md-on-surface-variant">{filterText.definitionLabelWithHint(uiText.labels.definitionId)}</span>
          <input
            className="mt-1 w-full rounded border border-md-outline-variant bg-md-surface-container px-2 py-1.5 font-mono text-sm text-md-on-surface"
            value={definitionDraft}
            onChange={(event) => onDefinitionDraftChange(event.target.value)}
            placeholder={filterText.definitionPlaceholder}
            autoComplete="off"
          />
        </label>
      </div>
      <div className="flex flex-wrap items-end gap-3">
        <label className="min-w-[260px] flex-1 text-sm text-md-on-surface">
          <span className="text-md-on-surface-variant">{filterText.nameInputHint}</span>
          <input
            className="mt-1 w-full rounded border border-md-outline-variant bg-md-surface-container px-2 py-1.5 font-mono text-sm text-md-on-surface"
            value={nameDraft}
            onChange={(event) => onNameDraftChange(event.target.value)}
            autoComplete="off"
          />
        </label>
        <button
          type="submit"
          className="rounded border-2 border-brand-cta-border bg-brand-cta-bg px-4 py-2 text-sm font-medium text-brand-cta-fg hover:bg-brand-cta-bg-hover"
          disabled={loading}
        >
          {filterText.search}
        </button>
        <label className="text-sm text-md-on-surface">
          <span className="text-md-on-surface-variant">{filterText.sortByLabel}</span>
          <select
            className="mt-1 rounded border border-md-outline-variant bg-md-surface-container px-2 py-2 text-sm text-md-on-surface"
            value={sortBy}
            onChange={(event) => onSortByChange(event.target.value)}
          >
            <option value="updatedAt">{filterText.sortByUpdatedAt}</option>
            <option value="displayId">{filterText.sortByDisplayId}</option>
          </select>
        </label>
        <label className="text-sm text-md-on-surface">
          <span className="text-md-on-surface-variant">{filterText.sortOrderLabel}</span>
          <select
            className="mt-1 rounded border border-md-outline-variant bg-md-surface-container px-2 py-2 text-sm text-md-on-surface"
            value={sortOrder}
            onChange={(event) => onSortOrderChange(event.target.value)}
          >
            <option value="desc">{filterText.sortOrderDesc}</option>
            <option value="asc">{filterText.sortOrderAsc}</option>
          </select>
        </label>
        <button
          type="button"
          className="rounded border border-md-outline-variant bg-md-surface-container px-4 py-2 text-sm text-md-on-surface hover:bg-md-surface-container-high"
          onClick={onClear}
          disabled={loading && !status && !nameDraft && !definitionDraft}
        >
          {filterText.clear}
        </button>
      </div>
      <p className="text-xs text-md-on-surface-variant">
        {filterText.pageInfo(limit, offset, currentPage)}
      </p>
    </form>
  );
}
