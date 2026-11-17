import { test } from "@playwright/test";

/**
 * Page Object の操作を、利用者の語のステップ名で HTML レポートとトレースに出す。
 * @remarks `box: true` で、失敗の位置を Page Object の中ではなく spec の呼び出し行に出す。
 * TypeScript のデコレーターは使わない（Playwright の変換と tsconfig の設定に依存させないため）。
 * @param title ステップ名（例: 「実行をキャンセルする」）。
 * @param body 操作。
 * @returns 操作の戻り値。
 */
export function step<T>(title: string, body: () => Promise<T>): Promise<T> {
  return test.step(title, body, { box: true });
}
