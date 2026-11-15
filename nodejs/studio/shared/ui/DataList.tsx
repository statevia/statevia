import type { CSSProperties, ReactNode } from "react";

/** `DataList` の 1 列の定義。 */
export type DataListColumn<TItem> = {
  /** 列の識別子。React の key に使う。 */
  id: string;
  /** 列の見出し。狭い幅では各セルの上に小さく出す。 */
  header: string;
  /** デスクトップの幅での列の幅（CSS grid のトラック。例: `minmax(0,2fr)`、`7rem`）。 */
  width: string;
  /** セルの寄せ。操作の列は `end` にする。省略時は `start`。 */
  align?: "start" | "end";
  /** 狭い幅で見出しを出さない（名前のように、値だけで意味が分かる列）。 */
  hideLabelOnMobile?: boolean;
  /**
   * セルの中身を描く。
   * @param item 行のデータ。
   * @returns セルの中身。
   */
  cell: (item: TItem) => ReactNode;
};

type DataListProps<TItem> = Readonly<{
  /** 一覧の名前。`aria-label` に使う。 */
  ariaLabel: string;
  /** 列の定義。左から順に並べる。 */
  columns: readonly DataListColumn<TItem>[];
  /** 表示する行。空のときの表示は呼び出し側（`PageState`）が出す。 */
  items: readonly TItem[];
  /**
   * 行の key。
   * @param item 行のデータ。
   * @returns 一意な文字列。
   */
  getRowKey: (item: TItem) => string;
  /**
   * 行の下に足す要素（行ごとの結果やエラー）。無ければ null を返す。
   * @param item 行のデータ。
   * @returns 行の下に出す要素。
   */
  renderRowFooter?: (item: TItem) => ReactNode;
}>;

/**
 * 一覧の共通部品。デスクトップの幅では列の見出しのある表、スマートフォンの幅では 1 行 1 枚のカードで出す（要件 8.1・8.2）。
 * DOM は 1 つで、CSS grid の切り替えだけで見た目を変える（表とカードを二重に描かない）。列の幅は `width` を並べた grid のトラックで揃える。
 * 並び替えやページングは持たない。呼び出し側が一覧の上下に置く。
 */
export function DataList<TItem>({ ariaLabel, columns, items, getRowKey, renderRowFooter }: DataListProps<TItem>) {
  const gridStyle: CSSProperties & Record<"--data-list-columns", string> = {
    "--data-list-columns": columns.map((column) => column.width).join(" "),
  };
  const rowGridClass = "sm:grid sm:grid-cols-(--data-list-columns) sm:items-center sm:gap-4";

  return (
    <div className="overflow-hidden rounded-card border border-md-outline bg-md-surface" style={gridStyle}>
      <div className={`hidden bg-md-surface-container px-4 py-2.5 text-xs font-medium text-md-on-surface-variant ${rowGridClass}`}>
        {columns.map((column) => (
          <span key={column.id} className={column.align === "end" ? "text-right" : undefined}>
            {column.header}
          </span>
        ))}
      </div>
      <ul aria-label={ariaLabel} className="divide-y divide-md-outline-variant">
        {items.map((item) => {
          const footer = renderRowFooter?.(item);
          return (
            <li key={getRowKey(item)} className="px-4 py-3 text-sm">
              <div className={`flex flex-col gap-2 ${rowGridClass}`}>
                {columns.map((column) => (
                  <div
                    key={column.id}
                    className={column.align === "end" ? "flex min-w-0 sm:justify-end" : "min-w-0"}
                  >
                    {column.hideLabelOnMobile ? null : (
                      <span className="mb-0.5 block text-xs text-md-on-surface-variant sm:hidden">{column.header}</span>
                    )}
                    {column.cell(item)}
                  </div>
                ))}
              </div>
              {footer ? <div className="mt-2">{footer}</div> : null}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
