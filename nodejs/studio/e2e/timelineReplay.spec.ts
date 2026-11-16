import type { Page } from "@playwright/test";
import { test, expect } from "./fixtures/test";
import { MOCK_EXECUTION_ID } from "./fixtures/mockExecution";
import { mockExecutionApi } from "./fixtures/mockStudioApi";
import { timelineEventsPage1, timelineEventsPage2, getStateAtSeq } from "./fixtures/mockTimeline";
import type { ExecutionDetailPage } from "./pages/ExecutionDetailPage";
import { uiText } from "@/shared/i18n/uiText";

/** 1 ページ目（seq 1〜3）の続きに 2 ページ目（seq 4〜5）を返すタイムラインをモックし、実行詳細を開く。 */
async function openWithTimeline(page: Page, executionDetailPage: ExecutionDetailPage): Promise<void> {
  await mockExecutionApi(page, {
    events: (afterSeq) =>
      afterSeq >= 3 ? { events: timelineEventsPage2, hasMore: false } : { events: timelineEventsPage1, hasMore: true },
    stateAtSeq: getStateAtSeq
  });
  await executionDetailPage.goto(MOCK_EXECUTION_ID);
  await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);
}

test.describe("イベントタイムライン・リプレイ", () => {
  test("イベントタイムラインの開閉ボタンが出て、最初は閉じている", async ({ executionDetailPage, page }) => {
    // Arrange / Act
    await openWithTimeline(page, executionDetailPage);

    // Assert
    await expect(executionDetailPage.timelineToggle).toBeVisible();
    await expect(executionDetailPage.timelineBody).toBeHidden();
  });

  test("タイムラインを開くとイベント一覧が表示される", async ({ executionDetailPage, page }) => {
    // Arrange
    await openWithTimeline(page, executionDetailPage);

    // Act
    await executionDetailPage.expandTimeline();

    // Assert
    const list = executionDetailPage.timelineEventList;
    await expect(executionDetailPage.timelineEvent(1)).toBeVisible();
    await expect(executionDetailPage.timelineEvent(2)).toBeVisible();
    await expect(executionDetailPage.timelineEvent(3)).toBeVisible();
    await expect(list.getByText(/GraphUpdated/).first()).toBeVisible();
    await expect(list.getByText(/Status →|ExecutionStatusChanged/).first()).toBeVisible();
  });

  test("時点 seq=1 を選ぶとリプレイ表示になり、その時点のノードと「現在に戻る」が出る", async ({ executionDetailPage, page }) => {
    // Arrange
    await openWithTimeline(page, executionDetailPage);
    await executionDetailPage.expandTimeline();

    // Act
    await executionDetailPage.selectTimelineSeq(1);

    // Assert
    await expect(executionDetailPage.backToCurrentButton.first()).toBeVisible({ timeout: 10_000 });
    await expect(executionDetailPage.nodeListCell("start")).toBeVisible();
    await expect(executionDetailPage.nodeListCell("task-a")).toBeVisible();
  });

  test("「現在に戻る」を押すとリプレイが解除される", async ({ executionDetailPage, page }) => {
    // Arrange
    await openWithTimeline(page, executionDetailPage);
    await executionDetailPage.expandTimeline();
    await executionDetailPage.selectTimelineSeq(1);
    await expect(executionDetailPage.backToCurrentButton.first()).toBeVisible({ timeout: 10_000 });

    // Act
    await executionDetailPage.backToCurrentButton.first().click();

    // Assert
    await expect(executionDetailPage.backToCurrentButton).toHaveCount(0);
  });

  test("リプレイ表示中は「過去の時点を表示中」のバナーが出る", async ({ executionDetailPage, page }) => {
    // Arrange
    await openWithTimeline(page, executionDetailPage);
    await executionDetailPage.expandTimeline();

    // Act
    await executionDetailPage.selectTimelineSeq(3);

    // Assert
    await expect(page.getByText(uiText.executionTimeline.replayingPastStateMessage)).toBeVisible({ timeout: 10_000 });
  });

  test("「続きを読み込む」で 2 ページ目のイベントが足される", async ({ executionDetailPage, page }) => {
    // Arrange
    await openWithTimeline(page, executionDetailPage);
    await executionDetailPage.expandTimeline();
    await expect(executionDetailPage.timelineEvent(3)).toBeVisible();
    await expect(executionDetailPage.timelineEvent(4)).toHaveCount(0);

    // Act
    await executionDetailPage.timelineLoadMoreButton.click();

    // Assert
    await expect(executionDetailPage.timelineEvent(4)).toBeVisible({ timeout: 5000 });
    await expect(executionDetailPage.timelineEvent(5)).toBeVisible();
  });

  test("開閉ボタンをもう一度押すとタイムラインが閉じる", async ({ executionDetailPage, page }) => {
    // Arrange
    await openWithTimeline(page, executionDetailPage);
    await executionDetailPage.expandTimeline();

    // Act
    await executionDetailPage.timelineToggle.click();

    // Assert
    await expect(executionDetailPage.timelineBody).toBeHidden();
  });
});
