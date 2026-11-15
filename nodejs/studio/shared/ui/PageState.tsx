"use client";

import type { ReactNode } from "react";
import { useDelayedVisibility } from "@/shared/lib/useDelayedVisibility";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "./Button";
import { EmptyState } from "./EmptyState";
import { Notice } from "./Notice";
import { TableSkeleton } from "./TableSkeleton";

type PageStateKind = "loading" | "empty" | "error";

type PageStateProps = {
  /** 表示する状態種別。 */
  state: PageStateKind;
  /** 補助メッセージ。 */
  message?: string;
  /** 再試行ボタン押下時の処理。error 時のみ有効。 */
  onRetry?: () => void;
  /** 再試行ボタンの文言。 */
  retryLabel?: string;
  /** loading 表示を出すまでの遅延（ミリ秒）。0 で即時。 */
  loadingDelayMs?: number;
  /** empty のときに添える次の操作（作成・条件のクリアなど）。 */
  emptyAction?: ReactNode;
};

/**
 * Loading / Empty / Error の共通表示ブロック。色はトークンから取り、ダークでも明るい枠のままにしない。
 * - loading: 遅延のあと、表の骨組みと「読み込み中」を出す
 * - empty: 次の操作を 1 つ添えた空の表示
 * - error: エラーのお知らせと再試行
 */
export function PageState({
  state,
  message,
  onRetry,
  retryLabel,
  loadingDelayMs,
  emptyAction
}: Readonly<PageStateProps>) {
  const uiText = useUiText();
  const showLoading = useDelayedVisibility(state === "loading", loadingDelayMs);

  if (state === "loading") {
    if (!showLoading) return null;
    return (
      <output className="flex flex-col gap-2" aria-live="polite" aria-busy="true">
        <div className="flex flex-wrap items-baseline gap-2 text-sm text-md-on-surface-variant">
          <span className="font-medium">{uiText.pageState.loading}</span>
          {message ? <p>{message}</p> : null}
        </div>
        <TableSkeleton rows={3} />
      </output>
    );
  }

  if (state === "error") {
    const showRetryButton = typeof onRetry === "function";
    return (
      <Notice
        tone="error"
        title={uiText.pageState.error}
        actions={
          showRetryButton ? (
            <Button variant="outline" size="sm" onClick={onRetry}>
              {retryLabel ?? uiText.actions.retry}
            </Button>
          ) : undefined
        }
      >
        {message ? <p>{message}</p> : null}
      </Notice>
    );
  }

  return (
    <output className="block" aria-live="polite">
      <EmptyState title={uiText.pageState.empty} description={message} action={emptyAction} />
    </output>
  );
}
