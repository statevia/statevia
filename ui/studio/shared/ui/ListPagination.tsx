"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";

type ListPaginationProps = {
  currentPageLabel: string;
  hasPrev: boolean;
  hasNext: boolean;
  onPrev: () => void;
  onNext: () => void;
  ariaLabel: string;
  prevLabel?: string;
  nextLabel?: string;
  className?: string;
};

/**
 * 一覧画面で共通利用するページング操作。
 */
export function ListPagination({
  currentPageLabel,
  hasPrev,
  hasNext,
  onPrev,
  onNext,
  ariaLabel,
  prevLabel,
  nextLabel,
  className
}: Readonly<ListPaginationProps>) {
  const uiText = useUiText();
  const effectivePrevLabel = prevLabel ?? uiText.pagination.prev;
  const effectiveNextLabel = nextLabel ?? uiText.pagination.next;
  const navClassName = [
    "flex items-center gap-2 text-sm",
    className
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <nav className={navClassName} aria-label={ariaLabel}>
      <button
        type="button"
        className="rounded border border-md-outline-variant bg-md-surface-container px-3 py-1.5 text-md-on-surface hover:bg-md-surface-container-high disabled:cursor-not-allowed disabled:opacity-50"
        onClick={onPrev}
        disabled={!hasPrev}
      >
        {effectivePrevLabel}
      </button>
      <span className="text-md-on-surface-variant">{currentPageLabel}</span>
      <button
        type="button"
        className="rounded border border-md-outline-variant bg-md-surface-container px-3 py-1.5 text-md-on-surface hover:bg-md-surface-container-high disabled:cursor-not-allowed disabled:opacity-50"
        onClick={onNext}
        disabled={!hasNext}
      >
        {effectiveNextLabel}
      </button>
    </nav>
  );
}
