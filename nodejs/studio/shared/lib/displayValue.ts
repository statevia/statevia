/**
 * 表示の綴りをそろえる対応。キーは大文字にした API の値。
 * 実行の状態は `Cancelled`、ノードの状態は `CANCELED` と綴りが違うので、HTTP の契約の綴り `CANCELED` にそろえる。
 */
const DISPLAY_SPELLING: Readonly<Record<string, string>> = {
  CANCELLED: "CANCELED"
};

/**
 * API の値（実行やノードの状態、ノードのタイプ）を画面に出す表記にする。
 * 訳さずに大文字へそろえ、綴りの揺れも 1 つにする（例: `Running` → `RUNNING`、`Cancelled` → `CANCELED`、`Task` → `TASK`）。
 * 表示の直前だけで使い、色の判定・比較・フィルタの送信値には API の値をそのまま使う。
 * @param value API の値。
 * @returns 画面に出す表記。
 */
export function toDisplayValue(value: string): string {
  const upper = value.trim().toUpperCase();
  return Object.hasOwn(DISPLAY_SPELLING, upper) ? DISPLAY_SPELLING[upper] : upper;
}
