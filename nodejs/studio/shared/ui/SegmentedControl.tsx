"use client";

/** セグメントの選択肢 1 つ。 */
export type SegmentedOption<TValue extends string> = {
  /** 選んだときに渡す値。 */
  value: TValue;
  /** ボタンの文言。 */
  label: string;
};

type SegmentedControlProps<TValue extends string> = Readonly<{
  /** まとまりの名前。`aria-label` に使う。 */
  ariaLabel: string;
  /** 選択肢。左から順に並べる。2〜4 個を想定する。 */
  options: readonly SegmentedOption<TValue>[];
  /** 今選んでいる値。 */
  value: TValue;
  /**
   * 選択肢を押したときに呼ぶ。今選んでいる値を押しても呼ぶ。
   * @param value 押した選択肢の値。
   */
  onChange: (value: TValue) => void;
}>;

/**
 * 同じ内容の表示の仕方を切り替えるセグメント（例: YAML とグラフ）。モック 04 のスケジュールの切り替えと同じ見た目。
 * 選んでいる項目は面の色を上げて示し、`aria-pressed` でも伝える。ページの移動やデータの絞り込みには使わない。
 */
export function SegmentedControl<TValue extends string>({ ariaLabel, options, value, onChange }: SegmentedControlProps<TValue>) {
  return (
    <div aria-label={ariaLabel} className="inline-flex rounded-control bg-md-surface-container-high p-0.5" role="toolbar">
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={selected}
            className={
              selected
                ? "h-7 rounded-[5px] bg-md-surface px-3 text-xs font-semibold text-md-on-surface shadow-xs"
                : "h-7 rounded-[5px] px-3 text-xs text-md-on-surface-variant hover:text-md-on-surface"
            }
            onClick={() => onChange(option.value)}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
