"use client";

import { X } from "lucide-react";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import type { Locale } from "@/shared/i18n/i18n";
import type { Theme } from "@/shared/lib/theme";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { AppHeader } from "./AppHeader";
import { NAV_GROUPS } from "./navigation/navItems";
import { resolveActiveItemId, selectVisibleGroups } from "./navigation/navSelection";
import { SideNav } from "./navigation/SideNav";
import { storeSideNavCollapsed } from "./navigation/sideNavState";

/** ドロワー内でフォーカストラップの対象にする要素。 */
const FOCUSABLE_SELECTOR = 'a[href], button:not([disabled]), select:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])';

type AppShellProps = Readonly<{
  theme: Theme;
  locale: Locale;
  /** テナント管理者か。サーバー側の `auth/me` で確定させ、失敗時は false を渡す。 */
  isTenantAdmin: boolean;
  /** 選択中 Project で `admin` か。 */
  isProjectAdmin: boolean;
  /** cookie `ui-sidenav` から解決した初期の畳み状態。 */
  initialCollapsed: boolean;
  /** サイドメニュー先頭の Project 切替。`shared` から `features` を参照しないよう slot で受け取る。 */
  projectSlot?: ReactNode;
  children: ReactNode;
}>;

/**
 * ヘッダー・サイドメニュー・本文を配置するアプリシェル。
 * `lg` 以上は固定列（展開 / レール）、`lg` 未満はヘッダーのボタンで開くドロワーにする。
 * ログイン画面ではサイドメニューを出さない。表示制御は見た目だけで、認可は各ルートと Service API が担う。
 */
export function AppShell({ theme, locale, isTenantAdmin, isProjectAdmin, initialCollapsed, projectSlot, children }: AppShellProps) {
  const pathname = usePathname();
  const uiText = useUiText();
  const showNavigation = pathname !== "/login";
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const menuButtonRef = useRef<HTMLButtonElement>(null);
  const drawerRef = useRef<HTMLDialogElement>(null);

  const groups = useMemo(() => selectVisibleGroups(NAV_GROUPS, { isTenantAdmin, isProjectAdmin }), [isTenantAdmin, isProjectAdmin]);
  const activeItemId = resolveActiveItemId(groups, pathname);

  const closeDrawer = () => {
    setDrawerOpen(false);
    menuButtonRef.current?.focus();
  };

  const toggleCollapsed = () => {
    const next = !collapsed;
    storeSideNavCollapsed(next);
    setCollapsed(next);
  };

  // ルートが変わったらドロワーを閉じる（ブラウザの戻る等、項目クリック以外の遷移も含む）。
  useEffect(() => {
    setDrawerOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!drawerOpen) return;
    const drawer = drawerRef.current;
    drawer?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setDrawerOpen(false);
        menuButtonRef.current?.focus();
        return;
      }
      if (event.key !== "Tab" || !drawer) return;
      const focusables = Array.from(drawer.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR));
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
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [drawerOpen]);

  return (
    <div className="flex h-screen flex-col">
      <AppHeader
        theme={theme}
        locale={locale}
        showNavigation={showNavigation}
        isNavigationOpen={drawerOpen}
        onOpenNavigation={() => setDrawerOpen(true)}
        menuButtonRef={menuButtonRef}
      />
      <div className="flex min-h-0 flex-1">
        {showNavigation ? (
          <aside className={`hidden shrink-0 border-r border-md-outline bg-md-surface lg:block ${collapsed ? "w-14" : "w-60"}`}>
            <SideNav
              mode={collapsed ? "rail" : "expanded"}
              groups={groups}
              activeItemId={activeItemId}
              projectSlot={projectSlot}
              onToggleCollapse={toggleCollapsed}
            />
          </aside>
        ) : null}
        <main className="min-w-0 flex-1 overflow-y-auto">
          <div className="mx-auto max-w-[min(1400px,calc(100%-2rem))] px-4 py-6">{children}</div>
        </main>
      </div>

      {showNavigation && drawerOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button
            type="button"
            tabIndex={-1}
            aria-hidden="true"
            onClick={closeDrawer}
            className="absolute inset-0 h-full w-full cursor-default bg-black/40"
          />
          {/*
            ネイティブ <dialog> を `open` 属性で開く。`showModal()` は使わず、背景・Escape・フォーカストラップは
            この部品で扱う（jsdom が `showModal()` を実装せず、開閉を React の状態で一元管理するため）。
            UA 既定の margin / border / 高さ制限 / 文字色を打ち消して左端のドロワーとして配置する。
          */}
          <dialog
            ref={drawerRef}
            open
            aria-modal="true"
            aria-label={uiText.navigation.ariaSideNav}
            className="absolute inset-y-0 left-0 right-auto m-0 flex h-full max-h-none w-72 max-w-[85vw] flex-col border-0 bg-md-surface p-0 text-md-on-surface shadow-xl"
          >
            <div className="flex justify-end px-3 pt-3">
              <button
                type="button"
                aria-label={uiText.navigation.closeMenu}
                onClick={closeDrawer}
                className="rounded-md p-2 text-md-on-surface-variant hover:bg-md-surface-container"
              >
                <X aria-hidden="true" className="h-5 w-5" />
              </button>
            </div>
            <div className="min-h-0 flex-1">
              <SideNav mode="drawer" groups={groups} activeItemId={activeItemId} projectSlot={projectSlot} onNavigate={closeDrawer} />
            </div>
          </dialog>
        </div>
      ) : null}
    </div>
  );
}
