import type { Locator, Page } from "@playwright/test";

/**
 * 画面右下に出るトースト。
 * @remarks トーストも待機のお知らせ（Notice）も `<output>`（role=status）なので、文言で絞る。
 */
export class Toasts {
  /**
   * @param page 対象のページ。
   */
  constructor(private readonly page: Page) {}

  /**
   * 文言を含むトースト。
   * @param text トーストの文言。
   * @returns トースト。
   */
  withText(text: string | RegExp): Locator {
    return this.page.getByRole("status").filter({ hasText: text });
  }
}
