import type { Locator, Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";
import { ExecutionTimelinePanel } from "../components/ExecutionTimelinePanel";
import { SendEventDialog } from "../components/SendEventDialog";
import { Toasts } from "../components/Toasts";
import { step } from "../support/step";

const sendEventText = uiText.executionDashboard.sendEvent;

/**
 * 実行詳細（`/executions/[executionId]`）の Page Object。
 * @remarks 見出しは実行 ID、操作は見出しの右（キャンセル・比較・イベントを送信）。実行 ID の入力欄は無い。
 * 文言は i18n 辞書（日本語）から引く。検証（`expect`）は持たず、spec に書く。
 * タイムライン・イベント送信ダイアログ・トーストはコンポーネントオブジェクトとして合成で持つ。
 */
export class ExecutionDetailPage {
  /** パンくずの「実行一覧」へのリンク。 */
  readonly executionsBreadcrumbLink: Locator;
  /** 見出しの「キャンセル」。 */
  readonly cancelButton: Locator;
  /** 見出しの下の「キャンセル要求あり」（開始日時と同じ行に並ぶ）。 */
  readonly cancelRequestedNotice: Locator;
  /** 見出しの「比較」。 */
  readonly compareButton: Locator;
  /** 見出しの「イベントを送信」。 */
  readonly sendEventButton: Locator;
  /** ノード詳細の「このノードへ送る」。 */
  readonly sendToNodeButton: Locator;
  /** 「リスト」表示の切り替え。 */
  readonly listViewButton: Locator;
  /** 「グラフ」表示の切り替え。 */
  readonly graphViewButton: Locator;
  /** グラフ表示のときだけ出る「全画面表示」。 */
  readonly fullscreenButton: Locator;
  /** イベントタイムライン。 */
  readonly timeline: ExecutionTimelinePanel;
  /** イベント送信ダイアログ。 */
  readonly sendEventDialog: SendEventDialog;
  /** トースト。 */
  readonly toasts: Toasts;
  /** 実行 ID・状態・操作を並べた見出しの領域。 */
  private readonly header: Locator;

  /**
   * @param page 対象のページ。
   */
  constructor(private readonly page: Page) {
    // Reason: 見出しの領域は名前を持たない <header> なので、h1 を含むもので絞る。
    this.header = page.locator("header").filter({ has: page.getByRole("heading", { level: 1 }) });
    this.executionsBreadcrumbLink = page
      .getByRole("navigation", { name: uiText.breadcrumbs.aria })
      .getByRole("link", { name: uiText.lists.executions });
    this.cancelButton = this.header.getByRole("button", { name: uiText.actions.cancel, exact: true });
    this.cancelRequestedNotice = this.header.getByText(uiText.executionHeader.cancelRequested);
    this.compareButton = this.header.getByRole("button", { name: uiText.executionHeader.compareLabel });
    this.sendEventButton = this.header.getByRole("button", { name: sendEventText.open });
    this.sendToNodeButton = page.getByRole("button", { name: sendEventText.sendToNode });
    this.listViewButton = page.getByRole("button", { name: uiText.actions.viewList, exact: true });
    this.graphViewButton = page.getByRole("button", { name: uiText.actions.viewGraph, exact: true });
    this.fullscreenButton = page.getByRole("button", { name: uiText.executionDashboard.graph.fullscreenEnter });
    this.timeline = new ExecutionTimelinePanel(page);
    this.sendEventDialog = new SendEventDialog(page);
    this.toasts = new Toasts(page);
  }

  /**
   * 実行 ID の見出し（h1）。読み込みが終わったことの目印にする。
   * @param executionId 実行の表示用 ID。
   * @returns 見出し。
   */
  heading(executionId: string): Locator {
    return this.page.getByRole("heading", { level: 1, name: executionId });
  }

  /**
   * 見出しの状態のピル。
   * @param status 実行の状態（例: `ACTIVE` / `CANCELED`）。
   * @returns 指定の状態を表示したピル。
   */
  statusBadge(status: string): Locator {
    return this.header.getByText(status, { exact: true });
  }

  /**
   * ノードの一覧の、ノード ID または状態名のセル。
   * @param text ノード ID か状態名。
   * @returns セル。
   */
  nodeListCell(text: string): Locator {
    return this.page.getByRole("cell", { name: text, exact: true });
  }

  /**
   * 実行詳細を開く。
   * @param executionId 実行の表示用 ID。
   */
  async goto(executionId: string): Promise<void> {
    await step(`実行詳細 ${executionId} を開く`, async () => {
      await this.page.goto(`/executions/${encodeURIComponent(executionId)}`);
    });
  }

  /** パンくずから実行一覧へ戻る。 */
  async backToExecutions(): Promise<void> {
    await step("パンくずから実行一覧へ戻る", () => this.executionsBreadcrumbLink.click());
  }

  /** グラフ表示に切り替える。 */
  async showGraph(): Promise<void> {
    await step("グラフ表示に切り替える", () => this.graphViewButton.click());
  }

  /** 実行をキャンセルする（見出しの「キャンセル」を押す）。 */
  async cancel(): Promise<void> {
    await step("実行をキャンセルする", () => this.cancelButton.click());
  }

  /**
   * ノードの一覧で状態名のノードを選び、ノード詳細に出す。
   * @param nodeName 状態名。
   */
  async selectNode(nodeName: string): Promise<void> {
    await step(`ノード ${nodeName} を選ぶ`, () => this.nodeListCell(nodeName).first().click());
  }

  /** ノード詳細の「このノードへ送る」で、イベント送信ダイアログを開く。 */
  async openSendEventForSelectedNode(): Promise<void> {
    await step("選んだノードへのイベント送信を開く", () => this.sendToNodeButton.click());
  }

  /** 見出しの「イベントを送信」で、イベント送信ダイアログを開く。 */
  async openSendEvent(): Promise<void> {
    await step("イベント送信を開く", () => this.sendEventButton.click());
  }
}
