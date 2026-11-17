import type { Locator, Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";
import { step } from "../support/step";

const timelineText = uiText.executionTimeline;

/**
 * 実行詳細のイベントタイムライン（開閉・続きの読み込み・時点の選択によるリプレイ）。
 * @remarks 検証は持たない。開いたか・何件出たかは spec で `expect` する。
 */
export class ExecutionTimelinePanel {
  /** 開閉ボタン（見出しを中に持つ。ボタンの子は読み上げで見出しにならないので、ボタンで探す）。 */
  readonly toggle: Locator;
  /** タイムライン本体（開いたときのイベント一覧のコンテナ）。 */
  readonly body: Locator;
  /** イベントの一覧。 */
  readonly eventList: Locator;
  /** 「続きを読み込む」。 */
  readonly loadMoreButton: Locator;
  /** 「現在に戻る」（タイムラインの見出しの横とリプレイのバナーの 2 か所に出る）。 */
  readonly backToCurrentButtons: Locator;
  /** リプレイ中に出る「過去の時点を表示中」のバナーの文言。 */
  readonly replayingBanner: Locator;

  /**
   * @param page 実行詳細のページ。
   */
  constructor(private readonly page: Page) {
    this.toggle = page.getByRole("button", { name: timelineText.title });
    // Reason: 本体は見出しも名前も持たない領域なので、開閉ボタンの aria-controls が指す ID で特定する。
    this.body = page.locator("#execution-timeline-body");
    this.eventList = this.body.getByRole("list");
    this.loadMoreButton = this.body.getByRole("button", { name: timelineText.loadMore });
    this.backToCurrentButtons = page.getByRole("button", { name: timelineText.backToCurrent });
    this.replayingBanner = page.getByText(timelineText.replayingPastStateMessage);
  }

  /**
   * seq のイベントのボタン。
   * @remarks ボタンの名前は `#seq` と日時が空白なしでつながる（例: `#12/28 …`）ため、名前の正規表現ではなく `#seq` の文字で絞る。
   * @param seq イベントの seq。
   * @returns イベントのボタン。
   */
  event(seq: number): Locator {
    return this.eventList.getByRole("button").filter({ has: this.page.getByText(`#${seq}`, { exact: true }) });
  }

  /** タイムラインを開く。開いていれば何もしない。 */
  async expand(): Promise<void> {
    await step("イベントタイムラインを開く", async () => {
      if ((await this.toggle.getAttribute("aria-expanded")) !== "true") {
        await this.toggle.click();
      }
    });
  }

  /** タイムラインを閉じる（開閉ボタンを押す）。 */
  async collapse(): Promise<void> {
    await step("イベントタイムラインを閉じる", () => this.toggle.click());
  }

  /**
   * seq の時点を選び、その時点の状態をリプレイ表示する。
   * @param seq イベントの seq。
   */
  async replayAt(seq: number): Promise<void> {
    await step(`時点 #${seq} をリプレイ表示する`, () => this.event(seq).click());
  }

  /** 「現在に戻る」を押してリプレイを解除する。 */
  async backToCurrent(): Promise<void> {
    await step("リプレイを解除して現在に戻る", () => this.backToCurrentButtons.first().click());
  }

  /** 「続きを読み込む」で次のページのイベントを足す。 */
  async loadMore(): Promise<void> {
    await step("タイムラインの続きを読み込む", () => this.loadMoreButton.click());
  }
}
