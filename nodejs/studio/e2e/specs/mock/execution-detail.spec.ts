import { test, expect } from "../../fixtures/test";
import { MOCK_EXECUTION_ID, mockExecutionDto, mockSingleWaitGraph } from "../../fixtures/mockExecution";
import { uiText } from "@/shared/i18n/uiText";

const EXECUTIONS_WRITE = "executions.write";

test.describe("実行詳細（モック）", () => {
  test("パンくずの「実行一覧」から一覧へ戻れる", async ({ executionDetailPage, mockApi, page }) => {
    // Arrange
    await mockApi.setup();
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await expect(executionDetailPage.heading(MOCK_EXECUTION_ID)).toBeVisible();

    // Act
    await executionDetailPage.backToExecutions();

    // Assert
    await expect(page).toHaveURL(/\/executions(\?.*)?$/);
  });

  test("リスト表示でノードの一覧が出る", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    await mockApi.setup();

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();
    await expect(executionDetailPage.nodeListCell("start")).toBeVisible();
    await expect(executionDetailPage.nodeListCell("task-a")).toBeVisible();
  });

  test("グラフ表示に切り替えられる", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    await mockApi.setup();
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await expect(executionDetailPage.heading(MOCK_EXECUTION_ID)).toBeVisible();

    // Act
    await executionDetailPage.showGraph();

    // Assert
    await expect(executionDetailPage.fullscreenButton).toBeVisible();
  });

  test("executions.write を持たない利用者には見出しの操作を出さない", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    await mockApi.setup({ graph: mockSingleWaitGraph, execution: { ...mockExecutionDto, waiting: true } });

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();
    await expect(executionDetailPage.compareButton).toBeVisible();
    await expect(executionDetailPage.cancelButton).toHaveCount(0);
    await expect(executionDetailPage.sendEventButton).toHaveCount(0);
  });

  test("待機が無い実行では「イベントを送信」を出さず、キャンセルと比較だけを出す", async ({ executionDetailPage, mockApi }) => {
    // Arrange
    await mockApi.setup({ permissions: [EXECUTIONS_WRITE] });

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();
    await expect(executionDetailPage.cancelButton).toBeVisible();
    await expect(executionDetailPage.compareButton).toBeVisible();
    await expect(executionDetailPage.sendEventButton).toHaveCount(0);
  });

  test("待機中のノードの「このノードへ送る」から、そのノードへ再開を送る", async ({ executionDetailPage, mockApi, page }) => {
    // Arrange
    const commands = await mockApi.setup({
      permissions: [EXECUTIONS_WRITE],
      execution: { ...mockExecutionDto, waiting: true },
      graph: mockSingleWaitGraph
    });
    const dialog = executionDetailPage.sendEventDialog;
    await executionDetailPage.goto(MOCK_EXECUTION_ID);
    await expect(page.getByText(uiText.executionDashboard.waitNotice.singleTitle("WaitApproval"))).toBeVisible();
    await executionDetailPage.selectNode("WaitApproval");
    await executionDetailPage.openSendEventForSelectedNode();
    await expect(dialog.nodeOption("WaitApproval", "Approved")).toBeChecked();

    // Act
    await dialog.submit();

    // Assert
    await expect(executionDetailPage.toasts.withText(uiText.executionDashboard.toasts.resumeAccepted)).toBeVisible();
    await expect(dialog.root).toHaveCount(0);
    expect(commands).toEqual([
      { path: `/executions/${MOCK_EXECUTION_ID}/nodes/wait-approval/resume`, body: expect.objectContaining({ resumeKey: "Approved" }) as unknown }
    ]);
  });
});
