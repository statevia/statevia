import type { ReactNode } from "react";

type CardProps = Readonly<{
  /** 見出し。省略すると見出しの帯を出さない。 */
  title?: string;
  /** 見出しの下の説明。 */
  description?: ReactNode;
  /** 見出しの右に置く操作。カードの中の操作は `standard` か `outline` にし、`lead` はページの見出しに 1 つだけ置く。 */
  actions?: ReactNode;
  /** 本文。 */
  children?: ReactNode;
  /** 本文の余白。表やリストをカードの端まで広げるときは `none`。 */
  padding?: "default" | "none";
  /** 外枠に足すクラス。 */
  className?: string;
}>;

/**
 * 見出し・説明・右上の操作・本文を持つ枠。画面ごとに `<section>` の枠を書かず、これを使う。
 * 見出しは h2（16px）。ページの見出し（h1）は `PageShell` が持つ。
 */
export function Card({ title, description, actions, children, padding = "default", className }: CardProps) {
  const hasHeader = Boolean(title ?? actions);
  return (
    <section className={["overflow-hidden rounded-card border border-md-outline bg-md-surface", className].filter(Boolean).join(" ")}>
      {hasHeader ? (
        <div className="flex flex-wrap items-start justify-between gap-3 border-b border-md-outline px-5 py-3.5">
          <div className="flex min-w-0 flex-col gap-0.5">
            {title ? <h2 className="text-base font-semibold text-md-on-surface">{title}</h2> : null}
            {description ? <p className="text-[13px] text-md-on-surface-variant">{description}</p> : null}
          </div>
          {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
        </div>
      ) : null}
      {children ? <div className={padding === "none" ? "" : "px-5 py-4"}>{children}</div> : null}
    </section>
  );
}
