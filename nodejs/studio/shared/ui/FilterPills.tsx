"use client";

/** 絞り込みのピル 1 つ。 */
export type FilterPillOption<TValue extends string> = {
  /** 選んだときに渡す値。「すべて」は空文字にする。 */
  value: TValue;
  /** ピルの文言。 */
  label: string;
  /** 件数。分かるときだけ文言の右に出す。 */
  count?: number;
};

type FilterPillsProps<TValue extends string> = Readonly<{
  /** まとまりの名前。`aria-label` に使う。 */
  ariaLabel: string;
  /** 選択肢。左から順に並べる。 */
  options: readonly FilterPillOption<TValue>[];
  /** 今選んでいる値。 */
  value: TValue;
  /**
   * ピルを押したときに呼ぶ。
   * @param value 押したピルの値。
   */
  onChange: (value: TValue) => void;
}>;

/**
 * 一覧を 1 つの項目（実行の状態など）で絞り込むピル（モック 07）。1 つだけ選べ、選んでいるピルは紺の塗りで示し `aria-pressed` でも伝える。
 * 表示の切り替え（`SegmentedControl`）とは使い分け、こちらは一覧のデータを絞り込むときに使う。
 */
export function FilterPills<TValue extends string>({ ariaLabel, options, value, onChange }: FilterPillsProps<TValue>) {
  return (
    <div aria-label={ariaLabel} className="flex flex-wrap gap-2" role="toolbar">
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={option.value || "all"}
            type="button"
            aria-pressed={selected}
            className={
              selected
                ? "inline-flex h-8 items-center gap-1.5 rounded-pill border border-transparent bg-brand-cta-bg px-3.5 text-xs font-semibold text-brand-cta-fg"
                : "inline-flex h-8 items-center gap-1.5 rounded-pill border border-md-outline-variant bg-md-surface px-3.5 text-xs text-md-on-surface hover:bg-md-surface-container"
            }
            onClick={() => onChange(option.value)}
          >
            {option.label}
            {option.count === undefined ? null : <span className="opacity-70">{option.count}</span>}
          </button>
        );
      })}
    </div>
  );
}
