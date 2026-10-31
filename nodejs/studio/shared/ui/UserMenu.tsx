"use client";

import Link from "next/link";
import { CircleUser } from "lucide-react";
import { useEffect, useId, useRef, useState } from "react";
import { clearSessionAndRedirectToLogin } from "@/shared/auth/authRedirect";
import { useUiText } from "@/shared/i18n/uiTextContext";

/**
 * ヘッダー右端のユーザーメニュー（アカウント・ログアウト）。
 * 矢印キー操作を伴う `role="menu"` ではなく、開閉ボタン＋リンク一覧のディスクロージャーとして実装する。
 * 外側クリックと Escape で閉じ、Escape ではボタンへフォーカスを戻す。
 */
export function UserMenu() {
  const uiText = useUiText();
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const panelId = useId();

  useEffect(() => {
    if (!isOpen) return;
    const handlePointerDown = (event: MouseEvent) => {
      if (event.target instanceof Node && !containerRef.current?.contains(event.target)) {
        setIsOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  return (
    <div ref={containerRef} className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-label={uiText.navigation.userMenu}
        aria-expanded={isOpen}
        aria-controls={panelId}
        onClick={() => setIsOpen((current) => !current)}
        className="rounded-md p-1.5 text-brand-header-fg-muted hover:bg-white/10 hover:text-brand-header-fg"
      >
        <CircleUser aria-hidden="true" className="h-6 w-6" />
      </button>
      {isOpen ? (
        <ul
          id={panelId}
          className="absolute right-0 z-40 mt-2 min-w-40 rounded-md border border-md-outline bg-md-surface py-1 text-sm text-md-on-surface shadow-lg"
        >
          <li>
            <Link href="/account" onClick={() => setIsOpen(false)} className="block px-3 py-2 hover:bg-md-surface-container">
              {uiText.navigation.account}
            </Link>
          </li>
          <li>
            <button
              type="button"
              onClick={() => {
                setIsOpen(false);
                void clearSessionAndRedirectToLogin({ includeFrom: false });
              }}
              className="block w-full px-3 py-2 text-left hover:bg-md-surface-container"
            >
              {uiText.navigation.logout}
            </button>
          </li>
        </ul>
      ) : null}
    </div>
  );
}
