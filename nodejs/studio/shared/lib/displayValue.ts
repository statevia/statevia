/**
 * API の値（実行やノードの状態、ノードのタイプ）を画面に出す表記にする。
 * 訳さずに大文字へそろえる（例: `Task` → `TASK`）。実行とノードの状態は API が既に大文字で、`CANCELED` の綴りもそろっている。
 * 表示の直前だけで使い、色の判定・比較・フィルタの送信値には API の値をそのまま使う。
 * @param value API の値。
 * @returns 画面に出す表記。
 */
export function toDisplayValue(value: string): string {
  return value.trim().toUpperCase();
}
