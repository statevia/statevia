import type { Locator, Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";
import { step } from "../support/step";

const sendEventText = uiText.executionDashboard.sendEvent;

/**
 * 実行詳細の「イベントを送信」ダイアログ（届け先のノードを選んで再開を送る）。
 * @remarks 届け先の既定の選択や件数は spec で `expect` する。
 */
export class SendEventDialog {
  /** ダイアログ。 */
  readonly root: Locator;
  /** 「送信」。 */
  readonly submitButton: Locator;

  /**
   * @param page 実行詳細のページ。
   */
  constructor(page: Page) {
    this.root = page.getByRole("dialog", { name: sendEventText.title });
    this.submitButton = this.root.getByRole("button", { name: sendEventText.submit, exact: true });
  }

  /**
   * ノードへ届ける届け先のラジオ。
   * @param nodeName 状態名。
   * @param eventName 1 つだけ待っているときのイベント名。
   * @returns 届け先のラジオ。
   */
  nodeOption(nodeName: string, eventName: string): Locator {
    return this.root.getByRole("radio", { name: sendEventText.nodeOptionLabel(nodeName, eventName) });
  }

  /** 選んだ届け先へ送信する。 */
  async submit(): Promise<void> {
    await step("イベントを送信する", () => this.submitButton.click());
  }
}
