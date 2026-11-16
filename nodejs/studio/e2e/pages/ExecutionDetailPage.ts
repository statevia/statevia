import { expect, type Locator, type Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";

const sendEventText = uiText.executionDashboard.sendEvent;

/**
 * 実行詳細（`/executions/[executionId]`）の Page Object。
 * @remarks 見出しは実行 ID、操作は見出しの右（キャンセル・比較・イベントを送信）。実行 ID の入力欄は無い。
 * 文言は i18n 辞書（日本語）から引く。
 */
export class ExecutionDetailPage {
  constructor(private readonly page: Page) {}

  /**
   * 実行詳細を開く。
   * @param executionId 実行の表示用 ID。
   */
  async goto(executionId: string): Promise<void> {
    await this.page.goto(`/executions/${encodeURIComponent(executionId)}`);
  }

  /**
   * 実行 ID の見出し（h1）。
   * @param executionId 実行の表示用 ID。
   */
  heading(executionId: string): Locator {
    return this.page.getByRole("heading", { level: 1, name: executionId });
  }

  /** パンくずの「実行一覧」へのリンク。 */
  get executionsBreadcrumbLink(): Locator {
    return this.page
      .getByRole("navigation", { name: uiText.breadcrumbs.aria })
      .getByRole("link", { name: uiText.lists.executions });
  }

  /** 見出しの「キャンセル」。 */
  get cancelButton(): Locator {
    return this.header.getByRole("button", { name: uiText.actions.cancel, exact: true });
  }

  /** 見出しの「比較」。 */
  get compareButton(): Locator {
    return this.header.getByRole("button", { name: uiText.executionHeader.compareLabel });
  }

  /** 見出しの「イベントを送信」。 */
  get sendEventButton(): Locator {
    return this.header.getByRole("button", { name: sendEventText.open });
  }

  /** ノード詳細の「このノードへ送る」。 */
  get sendToNodeButton(): Locator {
    return this.page.getByRole("button", { name: sendEventText.sendToNode });
  }

  /** イベント送信ダイアログ。 */
  get sendEventDialog(): Locator {
    return this.page.getByRole("dialog", { name: sendEventText.title });
  }

  /**
   * 文言を含むトースト。
   * @remarks トーストも待機のお知らせ（Notice）も `<output>`（status）なので、文言で絞る。
   * @param text トーストの文言。
   */
  toast(text: string | RegExp): Locator {
    return this.page.getByRole("status").filter({ hasText: text });
  }

  /** 「リスト」表示の切り替え。 */
  get listViewButton(): Locator {
    return this.page.getByRole("button", { name: uiText.actions.viewList, exact: true });
  }

  /** 「グラフ」表示の切り替え。 */
  get graphViewButton(): Locator {
    return this.page.getByRole("button", { name: uiText.actions.viewGraph, exact: true });
  }

  /** グラフ表示のときだけ出る「全画面表示」。 */
  get fullscreenButton(): Locator {
    return this.page.getByRole("button", { name: uiText.executionDashboard.graph.fullscreenEnter });
  }

  /** イベントタイムラインの開閉ボタン（見出しを中に持つ。ボタンの子は読み上げで見出しにならないので、ボタンで探す）。 */
  get timelineToggle(): Locator {
    return this.page.getByRole("button", { name: uiText.executionTimeline.title });
  }

  /** タイムライン本体（開いたときのイベント一覧のコンテナ）。 */
  get timelineBody(): Locator {
    return this.page.locator("#execution-timeline-body");
  }

  /** タイムラインのイベント一覧。 */
  get timelineEventList(): Locator {
    return this.timelineBody.locator("ul");
  }

  /** タイムラインの「続きを読み込む」。 */
  get timelineLoadMoreButton(): Locator {
    return this.timelineBody.getByRole("button", { name: uiText.executionTimeline.loadMore });
  }

  /** 「現在に戻る」（タイムラインの見出しの横とリプレイのバナーの 2 か所に出る）。 */
  get backToCurrentButton(): Locator {
    return this.page.getByRole("button", { name: uiText.executionTimeline.backToCurrent });
  }

  /** 実行 ID・状態・操作を並べた見出しの領域。 */
  private get header(): Locator {
    return this.page.locator("header").filter({ has: this.page.getByRole("heading", { level: 1 }) });
  }

  /**
   * 見出しの状態のピルに、指定の状態が出るまで待つ。
   * @param status 実行の状態（例: `ACTIVE` / `CANCELED`）。
   * @param options 待つ上限。
   */
  async expectExecutionStatus(status: string, options?: { timeout?: number }): Promise<void> {
    await expect(this.header.getByText(status, { exact: true })).toBeVisible({ timeout: options?.timeout ?? 15_000 });
  }

  /**
   * 実行が読み込まれ、見出しと状態が出るまで待つ。
   * @param executionId 実行の表示用 ID。
   * @param status 期待する状態。既定は `ACTIVE`。
   */
  async expectLoaded(executionId: string, status = "ACTIVE"): Promise<void> {
    await expect(this.heading(executionId)).toBeVisible({ timeout: 15_000 });
    await this.expectExecutionStatus(status);
  }

  /**
   * ノードの一覧の、ノード ID または状態名のセル。
   * @param text ノード ID か状態名。
   */
  nodeListCell(text: string): Locator {
    return this.page.getByRole("cell", { name: text, exact: true });
  }

  /**
   * ノードの一覧で、状態名の行を選ぶ（ノード詳細に出す）。
   * @param nodeName 状態名。
   */
  async selectNode(nodeName: string): Promise<void> {
    await this.nodeListCell(nodeName).first().click();
  }

  /** タイムラインを開く（閉じている場合のみ）。 */
  async expandTimeline(): Promise<void> {
    if ((await this.timelineToggle.getAttribute("aria-expanded")) !== "true") {
      await this.timelineToggle.click();
    }
    await expect(this.timelineEventList).toBeVisible({ timeout: 5000 });
  }

  /**
   * タイムラインで seq のイベントを選ぶ（リプレイ）。
   * @param seq イベントの seq。
   */
  async selectTimelineSeq(seq: number): Promise<void> {
    await this.timelineEvent(seq).click();
  }

  /**
   * タイムラインの seq のイベントのボタン。
   * @remarks ボタンの名前は `#seq` と日時が空白なしでつながる（例: `#12/28 …`）ため、名前の正規表現ではなく `#seq` の文字で絞る。
   * @param seq イベントの seq。
   */
  timelineEvent(seq: number): Locator {
    return this.timelineEventList
      .getByRole("button")
      .filter({ has: this.page.getByText(`#${seq}`, { exact: true }) });
  }

  /**
   * イベント送信ダイアログの、ノードへ届ける届け先のラジオ。
   * @param nodeName 状態名。
   * @param eventName 1 つだけ待っているときのイベント名。
   */
  sendEventNodeOption(nodeName: string, eventName: string): Locator {
    return this.sendEventDialog.getByRole("radio", { name: sendEventText.nodeOptionLabel(nodeName, eventName) });
  }

  /** イベント送信ダイアログの「送信」を押す。 */
  async submitSendEvent(): Promise<void> {
    await this.sendEventDialog.getByRole("button", { name: sendEventText.submit, exact: true }).click();
  }
}
