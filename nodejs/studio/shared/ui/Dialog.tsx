"use client";

import { X } from "lucide-react";
import { useEffect, useId, useRef, type ReactNode } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), select:not([disabled]), input:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** ダイアログの幅。`md` は 480px（確認など）、`lg` は 560px（作成フォームなど）。 */
export type DialogSize = "md" | "lg";

const SIZE_CLASS: Record<DialogSize, string> = {
  md: "max-w-[480px]",
  lg: "max-w-[560px]",
};

type DialogProps = Readonly<{
  /** 開いているか。false のときは何も描かない。 */
  open: boolean;
  /** 閉じる操作（Esc・右上の閉じる・キャンセル）をしたとき。 */
  onClose: () => void;
  /** 見出し。`aria-labelledby` で結ぶ。 */
  title: string;
  /** 見出しの下の説明。 */
  description?: ReactNode;
  /** 本文。 */
  children?: ReactNode;
  /** 下部の操作欄。右寄せで並べる（キャンセル → 決定の順）。 */
  footer?: ReactNode;
  /** 幅。省略すると `md`。 */
  size?: DialogSize;
  /** Esc と右上の閉じるで閉じられるか。送信中は false にして閉じさせない。省略すると true。 */
  dismissible?: boolean;
}>;

/**
 * 画面中央に出すポップアップダイアログ。全画面の作成・確認・送信のダイアログはこれを使う。
 * ネイティブ `<dialog>` を `open` 属性で開き、`showModal()` は使わない（jsdom が実装せず、開閉を React の状態で一元管理するため。`AppShell` のドロワーと同じ方針）。
 * 背景の暗幕・Esc で閉じる・フォーカスの閉じ込め・閉じたあと元の要素へフォーカスを戻す、をこの部品で扱う。
 */
export function Dialog({ open, onClose, title, description, children, footer, size = "md", dismissible = true }: DialogProps) {
  const uiText = useUiText();
  const titleId = useId();
  const descriptionId = useId();
  const panelRef = useRef<HTMLDialogElement>(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;
  const dismissibleRef = useRef(dismissible);
  dismissibleRef.current = dismissible;

  useEffect(() => {
    if (!open) return;
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const panel = panelRef.current;
    panel?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        if (dismissibleRef.current) onCloseRef.current();
        return;
      }
      if (event.key !== "Tab" || !panel) return;
      const focusables = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR));
      const first = focusables.at(0);
      const last = focusables.at(-1);
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      previouslyFocused?.focus();
    };
  }, [open]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4">
      <dialog
        ref={panelRef}
        open
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        className={`relative m-0 flex max-h-full w-full flex-col overflow-hidden rounded-card border border-md-outline bg-md-surface p-0 text-md-on-surface shadow-xl ${SIZE_CLASS[size]}`}
      >
        <div className="flex items-start justify-between gap-3 border-b border-md-outline px-6 py-5">
          <div className="flex min-w-0 flex-col gap-1">
            <h2 id={titleId} className="text-lg font-semibold">
              {title}
            </h2>
            {description ? (
              <div id={descriptionId} className="text-sm text-md-on-surface-variant">
                {description}
              </div>
            ) : null}
          </div>
          {dismissible ? (
            <button
              type="button"
              aria-label={uiText.dialog.close}
              onClick={onClose}
              className="rounded-control p-1.5 text-md-on-surface-variant hover:bg-md-surface-container-high"
            >
              <X aria-hidden="true" className="h-5 w-5" />
            </button>
          ) : null}
        </div>
        {children ? <div className="min-h-0 overflow-y-auto px-6 py-5">{children}</div> : null}
        {footer ? (
          <div className="flex flex-wrap justify-end gap-2 border-t border-md-outline bg-md-surface-container px-6 py-3.5">{footer}</div>
        ) : null}
      </dialog>
    </div>
  );
}
