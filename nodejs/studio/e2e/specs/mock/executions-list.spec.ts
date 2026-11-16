import { test, expect, type MockApi } from "../../fixtures/test";
import {
  MOCK_EXECUTION_ID,
  mockCompletedExecutionDto,
  mockExecutionCounts,
  mockExecutionsPage,
  mockProjects
} from "../../fixtures/mockExecution";

/**
 * 所属 Project が 1 件あり、その Project に実行が 2 件（ACTIVE の ex-1 と COMPLETED の ex-2）ある状態をモックする。
 * @param mockApi モックの API。
 * @param permissions ログイン中の利用者の権限キー。
 */
async function mockTwoExecutions(mockApi: MockApi, permissions: string[]): Promise<void> {
  await mockApi.setup({
    permissions,
    projects: mockProjects,
    executions: mockExecutionsPage,
    executionCounts: mockExecutionCounts
  });
}

test.describe("実行一覧", () => {
  test("開くと、一覧の API が返した実行が状態と実行 ID で並び、合計件数が出る", async ({
    executionsListPage,
    mockApi
  }) => {
    // Arrange
    await mockTwoExecutions(mockApi, ["executions.read"]);

    // Act
    await executionsListPage.goto();

    // Assert
    await expect(executionsListPage.heading).toBeVisible();
    await expect(executionsListPage.list.getByRole("listitem")).toHaveCount(2);
    await expect(executionsListPage.rowStatus(MOCK_EXECUTION_ID, "ACTIVE")).toBeVisible();
    await expect(executionsListPage.rowStatus(mockCompletedExecutionDto.displayId, "COMPLETED")).toBeVisible();
    await expect(executionsListPage.summary(2, 1)).toBeVisible();
  });

  test("所属 Project が無いと、実行が無いことを表示し、一覧を出さない", async ({
    executionsListPage,
    mockApi
  }) => {
    // Arrange
    await mockApi.setup();

    // Act
    await executionsListPage.goto();

    // Assert
    await expect(executionsListPage.emptyState).toBeVisible();
    await expect(executionsListPage.list).toHaveCount(0);
  });

  test("実行 ID のリンクを押すと、その実行の詳細へ移る", async ({ executionDetailPage, executionsListPage, mockApi, page }) => {
    // Arrange
    await mockTwoExecutions(mockApi, ["executions.read"]);
    await executionsListPage.goto();
    await expect(executionsListPage.row(MOCK_EXECUTION_ID)).toBeVisible();

    // Act
    await executionsListPage.openExecution(MOCK_EXECUTION_ID);

    // Assert
    await expect(page).toHaveURL(new RegExp(`/executions/${MOCK_EXECUTION_ID}$`));
    await expect(executionDetailPage.heading(MOCK_EXECUTION_ID)).toBeVisible();
  });

  test("executions.write を持たない利用者には、見出しの「イベントを送信」が出ない", async ({
    executionsListPage,
    mockApi
  }) => {
    // Arrange
    await mockTwoExecutions(mockApi, ["executions.read"]);

    // Act
    await executionsListPage.goto();

    // Assert
    await expect(executionsListPage.row(MOCK_EXECUTION_ID)).toBeVisible();
    await expect(executionsListPage.sendEventButton).toHaveCount(0);
  });

  test("executions.write を持つ利用者には、見出しの「イベントを送信」が出る", async ({
    executionsListPage,
    mockApi
  }) => {
    // Arrange
    await mockTwoExecutions(mockApi, ["executions.read", "executions.write"]);

    // Act
    await executionsListPage.goto();

    // Assert
    await expect(executionsListPage.row(MOCK_EXECUTION_ID)).toBeVisible();
    await expect(executionsListPage.sendEventButton).toBeVisible();
  });
});
