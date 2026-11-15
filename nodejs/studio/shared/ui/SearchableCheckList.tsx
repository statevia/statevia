"use client";

import { Search } from "lucide-react";
import { useId, useState } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { inputClassName } from "@/shared/ui/FormField";

/** 選べる項目 1 つ。 */
export type SearchableCheckListItem = {
  /** 選択の値（ユーザー ID・権限キーなど）。 */
  value: string;
  /** 主な表示名。検索の対象。 */
  label: string;
  /** 補足（キー・状態など）。検索の対象。 */
  description?: string;
};

type SearchableCheckListProps = Readonly<{
  /** 一覧の aria-label（例: 「メンバー」）。検索欄のラベルにも使う。 */
  ariaLabel: string;
  /** 選べる項目。 */
  items: readonly SearchableCheckListItem[];
  /** 今の選択。 */
  selected: ReadonlySet<string>;
  /** 保存済みの選択。今の選択と違う行に「追加」「解除」の印を出す。 */
  initial: ReadonlySet<string>;
  /**
   * 項目の選択を切り替える。
   * @param value 項目の値。
   */
  onToggle: (value: string) => void;
  /** 操作できないか（保存中など）。 */
  disabled?: boolean;
}>;

/**
 * 検索で絞り込める複数選択のリスト（要件 9.5）。権限の設定のように誤操作のコストが高い選択で使う。
 * @remarks 検索は表示名と補足に対する大文字小文字を区別しない部分一致で、選択は検索で隠れても残る。
 * 保存済みの選択と違う行には「追加」「解除」の印を出し、保存前に何が変わるかを行の上で分かるようにする。保存と確認は呼び出し側が持つ。
 */
export function SearchableCheckList({ ariaLabel, items, selected, initial, onToggle, disabled = false }: SearchableCheckListProps) {
  const text = useUiText().searchableCheckList;
  const baseId = useId();
  const [query, setQuery] = useState("");
  const normalizedQuery = query.trim().toLowerCase();
  const visibleItems =
    normalizedQuery.length === 0
      ? items
      : items.filter((item) =>
          [item.label, item.description ?? ""].some((field) => field.toLowerCase().includes(normalizedQuery))
        );

  return (
    <div className="flex flex-col gap-2.5">
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-[200px] flex-1">
          <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-md-on-surface-variant" />
          <input
            type="search"
            aria-label={text.searchLabel(ariaLabel)}
            placeholder={text.searchPlaceholder}
            className={inputClassName({ paddingX: "pl-9 pr-3", className: "h-9" })}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            autoComplete="off"
          />
        </div>
        <span className="text-xs text-md-on-surface-variant">{text.selectedCount(selected.size, items.length)}</span>
      </div>
      {visibleItems.length === 0 ? (
        <p className="py-3 text-sm text-md-on-surface-variant">{text.noMatch}</p>
      ) : (
        <ul aria-label={ariaLabel} className="max-h-72 divide-y divide-md-outline overflow-y-auto rounded-control border border-md-outline">
          {visibleItems.map((item) => {
            const inputId = `${baseId}-${item.value.replaceAll(/[^a-zA-Z0-9_-]/g, "-")}`;
            const checked = selected.has(item.value);
            const wasChecked = initial.has(item.value);
            let change: string | null = null;
            if (checked && !wasChecked) change = text.added;
            else if (!checked && wasChecked) change = text.removed;
            return (
              <li key={item.value} className={`flex items-center gap-3 px-3 py-2 text-sm ${change ? "bg-md-primary/5" : ""}`}>
                <input
                  id={inputId}
                  type="checkbox"
                  checked={checked}
                  disabled={disabled}
                  onChange={() => onToggle(item.value)}
                  className="h-4 w-4 accent-[var(--md-sys-color-primary)]"
                />
                <label htmlFor={inputId} className="flex min-w-0 flex-1 cursor-pointer flex-col">
                  <span className="truncate text-md-on-surface">{item.label}</span>
                  {item.description ? (
                    <span className="truncate text-xs text-md-on-surface-variant">{item.description}</span>
                  ) : null}
                </label>
                {change ? (
                  <span
                    className={`shrink-0 rounded-pill px-2 py-0.5 text-[11px] font-semibold ${
                      checked ? "bg-md-success-container text-md-on-success-container" : "bg-md-error-container text-md-on-error-container"
                    }`}
                  >
                    {change}
                  </span>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
