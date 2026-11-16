import { test, expect } from "./fixtures/test";
import { MOCK_EXECUTION_ID, mockExecutionDto, mockSingleWaitGraph } from "./fixtures/mockExecution";
import { mockExecutionApi } from "./fixtures/mockStudioApi";
import { uiText } from "@/shared/i18n/uiText";

const EXECUTIONS_WRITE = "executions.write";

test.describe("実行詳細（モック）", () => {
  test("パンくずの「実行一覧」から一覧へ戻れる", async ({ executionDetailPage, page }) => {
    // Arrange
    await mockExecutionApi(page);
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);

    // Act
    await executionDetailPage.executionsBreadcrumbLink.click();

    // Assert
    await expect(page).toHaveURL(/\/executions(\?.*)?$/);
  });

  test("リスト表示でノードの一覧が出る", async ({ executionDetailPage, page }) => {
    // Arrange
    await mockExecutionApi(page);

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.nodeListCell("start")).toBeVisible();
    await expect(executionDetailPage.nodeListCell("task-a")).toBeVisible();
  });

  test("グラフ表示に切り替えられる", async ({ executionDetailPage, page }) => {
    // Arrange
    await mockExecutionApi(page);
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);

    // Act
    await executionDetailPage.graphViewButton.click();

    // Assert
    await expect(executionDetailPage.fullscreenButton).toBeVisible();
  });

  test("executions.write を持たない利用者には見出しの操作を出さない", async ({ executionDetailPage, page }) => {
    // Arrange
    await mockExecutionApi(page, { graph: mockSingleWaitGraph, execution: { ...mockExecutionDto, waiting: true } });

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.compareButton).toBeVisible();
    await expect(executionDetailPage.cancelButton).toHaveCount(0);
    await expect(executionDetailPage.sendEventButton).toHaveCount(0);
  });

  test("待機が無い実行では「イベントを送信」を出さず、キャンセルと比較だけを出す", async ({ executionDetailPage, page }) => {
    // Arrange
    await mockExecutionApi(page, { permissions: [EXECUTIONS_WRITE] });

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.cancelButton).toBeVisible();
    await expect(executionDetailPage.compareButton).toBeVisible();
    await expect(executionDetailPage.sendEventButton).toHaveCount(0);
  });

  test("待機中のノードの「このノードへ送る」から、そのノードへ再開を送る", async ({ executionDetailPage, page }) => {
    // Arrange
    const commands = await mockExecutionApi(page, {
      permissions: [EXECUTIONS_WRITE],
      execution: { ...mockExecutionDto, waiting: true },
      graph: mockSingleWaitGraph
    });
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await executionDetailPage.expectLoaded(MOCK_EXECUTION_ID);
    await expect(page.getByText(uiText.executionDashboard.waitNotice.singleTitle("WaitApproval"))).toBeVisible();
    await executionDetailPage.selectNode("WaitApproval");

    // Act
    await executionDetailPage.sendToNodeButton.click();
    await expect(executionDetailPage.sendEventNodeOption("WaitApproval", "Approved")).toBeChecked();
    await executionDetailPage.submitSendEvent();

    // Assert
    await expect(executionDetailPage.toast(uiText.executionDashboard.toasts.resumeAccepted)).toBeVisible();
    await expect(executionDetailPage.sendEventDialog).toHaveCount(0);
    expect(commands).toEqual([
      { path: `/executions/${MOCK_EXECUTION_ID}/nodes/wait-approval/resume`, body: expect.objectContaining({ resumeKey: "Approved" }) as unknown }
    ]);
  });
});
