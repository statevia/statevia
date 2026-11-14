"use client";

import { X } from "lucide-react";
import { useUiText } from "@/shared/i18n/uiTextContext";

/** かけている絞り込みの条件 1 つ。 */
export type FilterChip = {
  /** 条件の識別子。React の key に使う。 */
  id: string;
  /** チップに出す文言（例: 「名前: order」）。 */
  label: string;
  /** この条件だけを外す。 */
  onRemove: () => void;
};

type FilterChipsProps = Readonly<{
  /** かけている条件。空なら何も描かない。 */
  chips: readonly FilterChip[];
  /** 全部の条件を外す。条件が 2 つ以上のときだけ「すべて外す」を出す。 */
  onClearAll: () => void;
}>;

/**
 * 一覧にかけている絞り込みの条件をチップで出す（要件 8.4）。
 * 各チップの × で 1 つずつ外し、2 つ以上かけているときは「すべて外す」で全部を外す。条件の URL への反映は呼び出し側が行う。
 */
export function FilterChips({ chips, onClearAll }: FilterChipsProps) {
  const text = useUiText().filterChips;
  if (chips.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <ul className="flex flex-wrap items-center gap-2" aria-label={text.ariaLabel}>
        {chips.map((chip) => (
          <li
            key={chip.id}
            className="inline-flex h-7 items-center gap-1 rounded-pill border border-md-outline-variant bg-md-surface-container pl-3 pr-1 text-xs text-md-on-surface"
          >
            {chip.label}
            <button
              type="button"
              className="inline-flex h-5 w-5 items-center justify-center rounded-pill text-md-on-surface-variant hover:bg-md-surface-container-high hover:text-md-on-surface"
              aria-label={text.remove(chip.label)}
              onClick={chip.onRemove}
            >
              <X aria-hidden="true" className="h-3.5 w-3.5" />
            </button>
          </li>
        ))}
      </ul>
      {chips.length > 1 ? (
        <button type="button" className="text-xs text-md-primary hover:underline" onClick={onClearAll}>
          {text.clearAll}
        </button>
      ) : null}
    </div>
  );
}
