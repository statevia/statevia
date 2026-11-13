"use client";

import Link from "next/link";
import { useUiText } from "@/shared/i18n/uiTextContext";

/** パンくずリストの 1 段。`href` が無い段はリンクにしない（グループ名や今の画面）。 */
export type BreadcrumbItem = Readonly<{
  /** 表示名。 */
  label: string;
  /** 遷移先。今の画面とグループ名には付けない。 */
  href?: string;
}>;

type BreadcrumbsProps = Readonly<{
  /** 上の階層から順に並べる。最後の段が今の画面。 */
  items: readonly BreadcrumbItem[];
}>;

/**
 * パンくずの 1 段。今の画面は `aria-current`、`href` のある段はリンク、それ以外は文字だけにする。
 * @param props 段と、今の画面かどうか。
 * @returns 1 段の要素。
 */
function Crumb({ item, isCurrent }: Readonly<{ item: BreadcrumbItem; isCurrent: boolean }>) {
  if (isCurrent) {
    return (
      <span aria-current="page" className="text-md-on-surface">
        {item.label}
      </span>
    );
  }
  if (item.href) {
    return (
      <Link href={item.href} className="text-md-primary hover:underline">
        {item.label}
      </Link>
    );
  }
  return <span>{item.label}</span>;
}

/**
 * 見出しの上に置くパンくずリスト。最後の段に `aria-current="page"` を付ける。
 */
export function Breadcrumbs({ items }: BreadcrumbsProps) {
  const uiText = useUiText();
  if (items.length === 0) return null;
  const lastIndex = items.length - 1;
  return (
    <nav aria-label={uiText.breadcrumbs.aria} className="text-[13px] text-md-on-surface-variant">
      <ol className="flex flex-wrap items-center gap-1.5">
        {items.map((item, index) => (
          <li key={`${index}:${item.label}`} className="flex items-center gap-1.5">
            {index > 0 ? <span aria-hidden="true">/</span> : null}
            <Crumb item={item} isCurrent={index === lastIndex} />
          </li>
        ))}
      </ol>
    </nav>
  );
}
