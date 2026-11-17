import type { Locator, Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";
import { step } from "../support/step";

const listText = uiText.executionsPage;

/**
 * 実行一覧（`/executions`）の Page Object。
 * @remarks 一覧は共通の `DataList`（アクセシブルネーム「実行一覧」の list）で、実行 1 件が 1 つの listitem。
 * 実行 ID のリンクが実行詳細への導線を兼ねる。見出しの右の「イベントを送信」は `executions.write` を持つときだけ出る。
 * 文言は i18n 辞書（日本語）から引く。検証（`expect`）は持たず、spec に書く。
 */
export class ExecutionsListPage {
  /** 見出し（h1）の「実行一覧」。読み込みが終わったことの目印にする。 */
  readonly heading: Locator;
  /** 実行の一覧。 */
  readonly list: Locator;
  /** 見出しの右の「イベントを送信」（購読しているイベントを送る）。 */
  readonly sendEventButton: Locator;
  /** 条件に合う実行が無いときの表示。 */
  readonly emptyState: Locator;

  /**
   * @param page 対象のページ。
   */
  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { level: 1, name: uiText.lists.executions });
    this.list = page.getByRole("list", { name: uiText.lists.executions });
    this.sendEventButton = page.getByRole("button", { name: listText.ingress.open, exact: true });
    this.emptyState = page.getByText(listText.empty, { exact: true });
  }

  /**
   * 実行 1 件の行。
   * @param executionId 実行の表示用 ID。
   * @returns 実行 ID のリンクを持つ listitem。
   */
  row(executionId: string): Locator {
    return this.list.getByRole("listitem").filter({ has: this.executionLink(executionId) });
  }

  /**
   * 実行 ID のリンク（実行詳細へ移る）。
   * @param executionId 実行の表示用 ID。
   * @returns リンク。
   */
  executionLink(executionId: string): Locator {
    return this.page.getByRole("link", { name: executionId, exact: true });
  }

  /**
   * 行の状態のピル。
   * @param executionId 実行の表示用 ID。
   * @param status 実行の状態（例: `ACTIVE` / `COMPLETED`）。
   * @returns 指定の状態を表示したピル。
   */
  rowStatus(executionId: string, status: string): Locator {
    return this.row(executionId).getByText(status, { exact: true });
  }

  /**
   * 一覧の下の件数の要約。
   * @param totalCount 合計件数。
   * @param pageNumber ページ番号（1 始まり）。
   * @returns 要約の文。
   */
  summary(totalCount: number, pageNumber: number): Locator {
    return this.page.getByText(listText.listSummary(totalCount, pageNumber), { exact: true });
  }

  /** 実行一覧を開く。 */
  async goto(): Promise<void> {
    await step("実行一覧を開く", async () => {
      await this.page.goto("/executions");
    });
  }

  /**
   * 実行 ID のリンクから実行詳細へ移る。
   * @param executionId 実行の表示用 ID。
   */
  async openExecution(executionId: string): Promise<void> {
    await step(`実行 ${executionId} の詳細を開く`, () => this.executionLink(executionId).click());
  }
}
