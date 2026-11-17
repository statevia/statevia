import { test, expect, type MockApi } from "../../fixtures/test";
import { MOCK_EXECUTION_ID } from "../../fixtures/mockExecution";
import { timelineEventsPage1, timelineEventsPage2, getStateAtSeq } from "../../fixtures/mockTimeline";
import type { ExecutionDetailPage } from "../../pages/ExecutionDetailPage";

/** 1 ページ目（seq 1〜3）の続きに 2 ページ目（seq 4〜5）を返すタイムラインをモックし、実行詳細を開く。 */
async function openWithTimeline(mockApi: MockApi, executionDetailPage: ExecutionDetailPage): Promise<void> {
  await mockApi.setup({
    events: (afterSeq) =>
      afterSeq >= 3 ? { events: timelineEventsPage2, hasMore: false } : { events: timelineEventsPage1, hasMore: true },
    stateAtSeq: getStateAtSeq
  });
  await executionDetailPage.goto(MOCK_EXECUTION_ID);
  await expect(executionDetailPage.heading(MOCK_EXECUTION_ID)).toBeVisible();
}

test.describe("イベントタイムライン・リプレイ", () => {
  test("イベントタイムラインの開閉ボタンが出て、最初は閉じている", async ({ executionDetailPage, mockApi }) => {
    // Arrange / Act
    await openWithTimeline(mockApi, executionDetailPage);

    // Assert
    await expect(executionDetailPage.timeline.toggle).toBeVisible();
    await expect(executionDetailPage.timeline.body).toBeHidden();
  });

  test("タイムラインを開くとイベント一覧が表示される", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    const timeline = executionDetailPage.timeline;
    await openWithTimeline(mockApi, executionDetailPage);

    // Act
    await timeline.expand();

    // Assert
    await expect(timeline.event(1)).toBeVisible();
    await expect(timeline.event(2)).toBeVisible();
    await expect(timeline.event(3)).toBeVisible();
    await expect(timeline.eventList.getByText(/GraphUpdated/).first()).toBeVisible();
    await expect(timeline.eventList.getByText(/Status →|ExecutionStatusChanged/).first()).toBeVisible();
  });

  test("時点 seq=1 を選ぶとリプレイ表示になり、その時点のノードと「現在に戻る」が出る", async ({
    executionDetailPage,
    mockApi
  }) => {
    // Arrange
    const timeline = executionDetailPage.timeline;
    await openWithTimeline(mockApi, executionDetailPage);
    await timeline.expand();

    // Act
    await timeline.replayAt(1);

    // Assert
    await expect(timeline.backToCurrentButtons.first()).toBeVisible();
    await expect(executionDetailPage.nodeListCell("start")).toBeVisible();
    await expect(executionDetailPage.nodeListCell("task-a")).toBeVisible();
  });

  test("「現在に戻る」を押すとリプレイが解除される", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    const timeline = executionDetailPage.timeline;
    await openWithTimeline(mockApi, executionDetailPage);
    await timeline.expand();
    await timeline.replayAt(1);
    await expect(timeline.backToCurrentButtons.first()).toBeVisible();

    // Act
    await timeline.backToCurrent();

    // Assert
    await expect(timeline.backToCurrentButtons).toHaveCount(0);
  });

  test("リプレイ表示中は「過去の時点を表示中」のバナーが出る", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    const timeline = executionDetailPage.timeline;
    await openWithTimeline(mockApi, executionDetailPage);
    await timeline.expand();

    // Act
    await timeline.replayAt(3);

    // Assert
    await expect(timeline.replayingBanner).toBeVisible();
  });

  test("「続きを読み込む」で 2 ページ目のイベントが足される", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    const timeline = executionDetailPage.timeline;
    await openWithTimeline(mockApi, executionDetailPage);
    await timeline.expand();
    await expect(timeline.event(3)).toBeVisible();
    await expect(timeline.event(4)).toHaveCount(0);

    // Act
    await timeline.loadMore();

    // Assert
    await expect(timeline.event(4)).toBeVisible();
    await expect(timeline.event(5)).toBeVisible();
  });

  test("開閉ボタンをもう一度押すとタイムラインが閉じる", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    const timeline = executionDetailPage.timeline;
    await openWithTimeline(mockApi, executionDetailPage);
    await timeline.expand();
    await expect(timeline.eventList).toBeVisible();

    // Act
    await timeline.collapse();

    // Assert
    await expect(timeline.body).toBeHidden();
  });
});
