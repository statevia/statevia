import { test, expect } from "../../fixtures/test";
import { MOCK_EXECUTION_ID } from "../../fixtures/mockExecution";

test.describe("smoke", () => {
  test("ルートを開くとダッシュボードへ移る", async ({ mockApi, page }) => {
    // Arrange
    await mockApi.setup();

    // Act
    await page.goto("/");

    // Assert
    await expect(page).toHaveURL(/\/dashboard$/);
  });

  test("実行詳細は実行 ID を見出しに出し、実行 ID の入力欄と「読み込む」を持たない", async ({
    executionDetailPage,
    mockApi,
    page
  }) => {
    // Arrange
    await mockApi.setup();

    // Act
    await executionDetailPage.goto(MOCK_EXECUTION_ID);

    // Assert
    await expect(executionDetailPage.heading(MOCK_EXECUTION_ID)).toBeVisible();
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();
    await expect(page.getByRole("textbox", { name: /Execution ID|実行 ID/ })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "読み込む", exact: true })).toHaveCount(0);
  });
});
