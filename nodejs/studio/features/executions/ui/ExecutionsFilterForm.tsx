"use client";

import { Search } from "lucide-react";
import type { FormEvent } from "react";
import { useI18n } from "@/shared/i18n/uiTextContext";
import type { ExecutionCounts, SortOrder } from "@/features/executions/api";
import { Button } from "@/shared/ui/Button";
import { FilterPills, type FilterPillOption } from "@/shared/ui/FilterPills";
import { inputClassName } from "@/shared/ui/FormField";
import type { ExecutionSortBy, ExecutionStatusFilter } from "../hooks/useExecutionsPage";
import { toDisplayValue } from "@/shared/lib/displayValue";

/**
 * ステータスの絞り込みで選べる値。API の契約の値をそのまま送り、そのまま出す（訳さない）。
 */
const EXECUTION_STATUS_FILTER_VALUES: readonly Exclude<ExecutionStatusFilter, "" | "NEEDS_ATTENTION">[] = [
  "ACTIVE",
  "COMPLETED",
  "FAILED",
  "CANCELED"
];

/** 並び順の選択肢。項目と順序の組を 1 つの選択欄で選ぶ。 */
const SORT_OPTIONS: ReadonlyArray<{ sortBy: ExecutionSortBy; sortOrder: SortOrder }> = [
  { sortBy: "updatedAt", sortOrder: "desc" },
  { sortBy: "updatedAt", sortOrder: "asc" },
  { sortBy: "startedAt", sortOrder: "desc" },
  { sortBy: "startedAt", sortOrder: "asc" },
  { sortBy: "displayId", sortOrder: "asc" },
  { sortBy: "displayId", sortOrder: "desc" }
];

/** 実行 ID の検索欄のエラーの要素 ID。 */
const NAME_ERROR_ID = "executions-name-error";

/** 定義 ID の欄のエラーの要素 ID。 */
const DEFINITION_ERROR_ID = "executions-definition-error";

/** 実行一覧フィルタ欄の入力。 */
export type ExecutionsFilterFormProps = {
  /** 状態の絞り込み。空文字はすべて。 */
  status: ExecutionStatusFilter;
  /** ピルに出す件数。未取得なら件数を出さない。 */
  counts?: ExecutionCounts | null;
  /** ソートキー。 */
  sortBy: ExecutionSortBy;
  /** ソート順。 */
  sortOrder: SortOrder;
  /** 名前の入力中の値。 */
  nameDraft: string;
  /** 定義 ID の入力中の値。 */
  definitionDraft: string;
  /** 実行 ID の検索語の検証エラー。無ければ null。 */
  nameError: string | null;
  /** 定義 ID の検証エラー。無ければ null。 */
  definitionError: string | null;
  /** 検索ボタンを無効にするか。一覧取得中。 */
  loading: boolean;
  /**
   * フォーム送信。
   * @param event フォームの submit。
   */
  onSubmit: (event: FormEvent) => void;
  /**
   * 状態の絞り込みの変更。
   * @param value 状態の値。空文字はすべて。
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
   * 並び順の変更。
   * @param sortBy 並びの項目。
   * @param sortOrder 並びの順序。
   */
  onSortChange: (sortBy: ExecutionSortBy, sortOrder: SortOrder) => void;
};

/**
 * 実行一覧のフィルタ欄（モック 07）。状態はピルで選んですぐ反映し、実行 ID と定義 ID は検索ボタンで反映する。
 * 無効な値は各入力欄の下にエラーを出す（要件 8.5）。検証と URL 更新は呼び出し側のコマンドに渡す。
 * @param props 入力値とコマンド。
 * @returns フィルタフォーム。
 */
export function ExecutionsFilterForm({
  status,
  counts = null,
  sortBy,
  sortOrder,
  nameDraft,
  definitionDraft,
  nameError,
  definitionError,
  loading,
  onSubmit,
  onStatusChange,
  onNameDraftChange,
  onDefinitionDraftChange,
  onSortChange
}: Readonly<ExecutionsFilterFormProps>) {
  const { uiText } = useI18n();
  const filterText = uiText.executionsPage.filter;
  const statusOptions: FilterPillOption<ExecutionStatusFilter>[] = [
    { value: "", label: filterText.all, count: counts?.total },
    { value: "NEEDS_ATTENTION", label: filterText.needsAttention, count: counts?.needsAttention },
    ...EXECUTION_STATUS_FILTER_VALUES.map((value) => ({
      value,
      label: toDisplayValue(value),
      count: counts?.byStatus[value]
    }))
  ];
  const sortByLabel: Record<ExecutionSortBy, string> = {
    updatedAt: filterText.sortByUpdatedAt,
    startedAt: filterText.sortByStartedAt,
    displayId: filterText.sortByDisplayId
  };

  return (
    <div className="flex flex-col gap-3">
      <FilterPills ariaLabel={uiText.labels.status} options={statusOptions} value={status} onChange={onStatusChange} />
      <form onSubmit={onSubmit} className="flex flex-wrap items-start gap-3">
        <div className="relative min-w-[240px] flex-1">
          <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-4 h-4 w-4 text-md-on-surface-variant" />
          <input
            type="search"
            className={inputClassName({ error: nameError !== null, paddingX: "pl-9 pr-3", className: "h-9 font-mono" })}
            value={nameDraft}
            onChange={(event) => onNameDraftChange(event.target.value)}
            aria-label={filterText.nameInputHint}
            placeholder={filterText.nameInputHint}
            aria-invalid={nameError ? true : undefined}
            aria-describedby={nameError ? NAME_ERROR_ID : undefined}
            autoComplete="off"
          />
          {nameError ? (
            <span id={NAME_ERROR_ID} role="alert" className="mt-1 block text-xs text-md-error">
              {nameError}
            </span>
          ) : null}
        </div>
        <div className="w-full sm:w-56">
          <input
            className={inputClassName({ error: definitionError !== null, className: "h-9 font-mono" })}
            value={definitionDraft}
            onChange={(event) => onDefinitionDraftChange(event.target.value)}
            aria-label={filterText.definitionLabelWithHint(uiText.labels.definitionId)}
            placeholder={filterText.definitionInputHint}
            aria-invalid={definitionError ? true : undefined}
            aria-describedby={definitionError ? DEFINITION_ERROR_ID : undefined}
            autoComplete="off"
          />
          {definitionError ? (
            <span id={DEFINITION_ERROR_ID} role="alert" className="mt-1 block text-xs text-md-error">
              {definitionError}
            </span>
          ) : null}
        </div>
        <Button variant="standard" type="submit" className="mt-1.5" disabled={loading}>
          {filterText.search}
        </Button>
        <label className="mt-1.5 flex h-9 items-center gap-2 text-sm text-md-on-surface-variant">
          {filterText.sortLabel}
          <select
            className="h-9 rounded-control border border-md-outline-variant bg-md-surface-container px-3 text-sm text-md-on-surface"
            value={`${sortBy}:${sortOrder}`}
            onChange={(event) => {
              const selected = SORT_OPTIONS.find((option) => `${option.sortBy}:${option.sortOrder}` === event.target.value);
              if (selected) onSortChange(selected.sortBy, selected.sortOrder);
            }}
          >
            {SORT_OPTIONS.map((option) => (
              <option key={`${option.sortBy}:${option.sortOrder}`} value={`${option.sortBy}:${option.sortOrder}`}>
                {`${sortByLabel[option.sortBy]}（${option.sortOrder === "asc" ? filterText.sortOrderAsc : filterText.sortOrderDesc}）`}
              </option>
            ))}
          </select>
        </label>
      </form>
    </div>
  );
}
