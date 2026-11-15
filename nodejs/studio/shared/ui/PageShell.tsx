import type { ReactNode } from "react";

type PageShellProps = {
  /** ページの主見出し。 */
  title: string;
  /** 見出し直下の補足文。 */
  description?: string;
  /** 見出しの上に置くパンくずリスト（`Breadcrumbs`）。深い階層の画面で上へ戻る導線にする。 */
  breadcrumbs?: ReactNode;
  /** 見出しの右隣に置く要素（状態バッジ・版など）。 */
  titleAddon?: ReactNode;
  /** 見出しの右に置く操作。主導線（`lead` の緑）は 1 画面に 1 つまでにし、ここかフォーム下部の操作バーに置く。 */
  primaryActions?: ReactNode;
  /** 本文領域。 */
  children: ReactNode;
  /** 戻り導線や補助リンク。 */
  secondaryActions?: ReactNode;
  /** 画面下部に固定する操作バー（フォームの保存・キャンセルと未保存の表示）。 */
  stickyFooter?: ReactNode;
  /** ページ外枠に追加するクラス名。 */
  className?: string;
};

/**
 * 全画面共通のページ骨格。
 * パンくず → 見出し（と右の操作）→ 本文 → 補助の導線 → 固定の操作バー、の順に並べる。
 * 見出しは枠で囲わず背景の上に直接置き、箱の入れ子を減らす。
 */
export function PageShell({
  title,
  description,
  breadcrumbs,
  titleAddon,
  primaryActions,
  children,
  secondaryActions,
  stickyFooter,
  className
}: Readonly<PageShellProps>) {
  const shellClassName = [
    "mx-auto flex w-full max-w-5xl flex-col gap-5 p-4 sm:p-6",
    className
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={shellClassName}>
      {breadcrumbs}
      <header className="flex flex-col items-start gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-2xl font-bold text-md-on-surface">{title}</h1>
            {titleAddon}
          </div>
          {description ? <p className="text-sm text-md-on-surface-variant">{description}</p> : null}
        </div>
        {primaryActions ? <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto sm:justify-end">{primaryActions}</div> : null}
      </header>

      <section className="flex flex-col gap-4">{children}</section>

      {secondaryActions ? (
        <footer className="flex flex-col items-start gap-2 border-t border-md-outline pt-3 text-sm text-md-on-surface-variant sm:flex-row sm:items-center sm:gap-3">
          {secondaryActions}
        </footer>
      ) : null}

      {stickyFooter ? (
        <div className="sticky bottom-0 -mx-4 border-t border-md-outline bg-md-surface px-4 py-3 sm:-mx-6 sm:px-6">
          <div className="flex flex-wrap items-center justify-between gap-3">{stickyFooter}</div>
        </div>
      ) : null}
    </div>
  );
}
