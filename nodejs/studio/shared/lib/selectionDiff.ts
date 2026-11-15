/** 選択の変更（保存前に見せる追加と解除）。 */
export type SelectionDiff = {
  /** 元に無く、今は選んでいる値。元の並びではなく `selected` の並び順。 */
  added: string[];
  /** 元にあって、今は外した値。`initial` の並び順。 */
  removed: string[];
};

/**
 * 元の選択と今の選択から、追加と解除を求める。
 * @param initial 保存済みの選択。
 * @param selected 今の選択。
 * @returns 追加と解除。どちらも空なら変更は無い。
 */
export function diffSelection(initial: ReadonlySet<string>, selected: ReadonlySet<string>): SelectionDiff {
  return {
    added: [...selected].filter((value) => !initial.has(value)),
    removed: [...initial].filter((value) => !selected.has(value))
  };
}

/**
 * 選択に変更があるか。
 * @param diff 追加と解除。
 * @returns 1 件でも追加か解除があれば true。
 */
export function hasSelectionChanges(diff: SelectionDiff): boolean {
  return diff.added.length > 0 || diff.removed.length > 0;
}
