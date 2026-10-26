"use client";

import Image from "next/image";
import Link from "next/link";
import { Menu } from "lucide-react";
import type { RefObject } from "react";
import type { Locale } from "@/shared/i18n/i18n";
import type { Theme } from "@/shared/lib/theme";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { LanguageToggle } from "./LanguageToggle";
import { ThemeToggle } from "./ThemeToggle";
import { UserMenu } from "./UserMenu";

type AppHeaderProps = Readonly<{
  theme: Theme;
  locale: Locale;
  /** ログイン済み画面か。false（ログイン画面）ではメニューボタンとユーザーメニューを出さない。 */
  showNavigation: boolean;
  /** ドロワーが開いているか（メニューボタンの `aria-expanded`）。 */
  isNavigationOpen?: boolean;
  /** `lg` 未満でメニューボタンを押したとき。 */
  onOpenNavigation?: () => void;
  /** ドロワーを閉じたときにフォーカスを戻すため、メニューボタンの参照を親へ渡す。 */
  menuButtonRef?: RefObject<HTMLButtonElement>;
}>;

/**
 * 共通ヘッダー。画面移動はサイドメニューに任せ、ブランド・テーマ・言語・ユーザーメニューだけを持つ。
 */
export function AppHeader({ theme, locale, showNavigation, isNavigationOpen = false, onOpenNavigation, menuButtonRef }: AppHeaderProps) {
  const uiText = useUiText();
  const brandHref = showNavigation ? "/dashboard" : "/login";

  return (
    <header className="border-b border-md-outline bg-brand-header-bg text-brand-header-fg">
      <div className="flex items-center justify-between gap-3 px-4 py-3">
        <div className="flex items-center gap-2">
          {showNavigation ? (
            <button
              ref={menuButtonRef}
              type="button"
              aria-label={uiText.navigation.openMenu}
              aria-expanded={isNavigationOpen}
              onClick={onOpenNavigation}
              className="rounded-md p-1.5 text-brand-header-fg-muted hover:bg-white/10 hover:text-brand-header-fg lg:hidden"
            >
              <Menu aria-hidden="true" className="h-6 w-6" />
            </button>
          ) : null}
          <Link href={brandHref} className="inline-flex items-center gap-2">
            <Image
              src="/brand/icon-mark.png"
              alt="statevia"
              width={32}
              height={32}
              className="h-8 w-8 rounded-md border border-white/20 object-cover"
              priority
            />
            <span className="hidden text-[1.75rem] font-semibold leading-none tracking-wide sm:inline">
              <span>state</span>
              <span className="text-emerald-400">via</span>
            </span>
          </Link>
        </div>
        <div className="flex items-center gap-2 sm:gap-3">
          <ThemeToggle theme={theme} />
          <LanguageToggle locale={locale} />
          {showNavigation ? <UserMenu /> : null}
        </div>
      </div>
    </header>
  );
}
