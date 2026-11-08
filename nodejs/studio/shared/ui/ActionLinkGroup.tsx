"use client";

import Link from "next/link";
import { NAVIGATION_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { useUiText } from "@/shared/i18n/uiTextContext";

type ActionLinkPriority = "primary" | "secondary";

/** ActionLinkItem の型定義。 */
export type ActionLinkItem = {
  /** リンクの表示名。 */
  label: string;
  /** 遷移先のパス。 */
  href: string;
  /** 見た目の強さ。`primary` は主操作の CTA、`secondary` はナビゲーションボタン。省略すると `secondary`。 */
  priority?: ActionLinkPriority;
};

type ActionLinkGroupProps = {
  links: ActionLinkItem[];
  className?: string;
};

const ACTION_LINK_CLASS_MAP: Record<ActionLinkPriority, string> = {
  primary:
    "rounded-md border-2 border-brand-cta-border bg-brand-cta-bg px-3 py-1.5 text-sm font-medium text-brand-cta-fg hover:bg-brand-cta-bg-hover",
  secondary: NAVIGATION_BUTTON_CLASS
};

function getLinkClass(priority: ActionLinkPriority): string {
  return ACTION_LINK_CLASS_MAP[priority];
}

/**
 * 一覧遷移・戻り・関連導線をまとめて描画するリンクグループ。
 */
export function ActionLinkGroup({ links, className }: Readonly<ActionLinkGroupProps>) {
  const uiText = useUiText();
  if (links.length === 0) return null;

  return (
    <nav
      aria-label={uiText.actionLinks.aria.navigation}
      className={className ? `flex flex-wrap items-center gap-3 ${className}` : "flex flex-wrap items-center gap-3"}
    >
      {links.map((link) => {
        const priority = link.priority ?? "secondary";
        return (
          <Link
            key={`${link.href}:${link.label}`}
            href={link.href}
            className={getLinkClass(priority)}
          >
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}
