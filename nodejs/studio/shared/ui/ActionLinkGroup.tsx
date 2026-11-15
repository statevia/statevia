"use client";

import Link from "next/link";
import { buttonClassName } from "@/shared/ui/Button";
import { useUiText } from "@/shared/i18n/uiTextContext";

type ActionLinkPriority = "primary" | "secondary";

/** ActionLinkItem の型定義。 */
export type ActionLinkItem = {
  /** リンクの表示名。 */
  label: string;
  /** 遷移先のパス。 */
  href: string;
  /** 見た目の強さ。`primary` は紺のボタン、`secondary` は枠線のボタン。主導線の緑にはしない（戻り・関連の導線のため）。省略すると `secondary`。 */
  priority?: ActionLinkPriority;
};

type ActionLinkGroupProps = {
  links: ActionLinkItem[];
  className?: string;
};

const ACTION_LINK_CLASS_MAP: Record<ActionLinkPriority, string> = {
  primary: buttonClassName("standard"),
  secondary: buttonClassName("outline")
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
