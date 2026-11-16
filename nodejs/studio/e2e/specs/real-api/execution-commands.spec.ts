import { realTest as test, expect } from "../../fixtures/test";
import { e2eParallelWaitWorkflowYaml, e2eWaitWorkflowYaml } from "../../fixtures/e2eWaitWorkflowYaml";
import {
  startExecutionFromYaml,
  uniqueDefinitionName,
  waitForExecutionStatus,
  waitForWaitingNodes
} from "../../fixtures/serviceApi";
import { uiText } from "@/shared/i18n/uiText";

const toasts = uiText.executionDashboard.toasts;
const waitNotice = uiText.executionDashboard.waitNotice;

/**
 * Service API 実体 + Next プロキシ経由の実行詳細の UI E2E（real-api project）。
 * @remarks 定義の作成と実行の開始は API で行い、画面ではキャンセルとイベント送信を操作する。
 * ブラウザのログインは setup-real が保存した Cookie で済んでいる。前提と実行例は `e2e/README.md`。
 */
test.describe("Service API + UI (real)", () => {
  test.describe.configure({ mode: "serial", timeout: 120_000 });

  test("実行一覧から実行 ID のリンクで実行詳細へ移る", async ({ executionDetailPage, page, request, serviceApi }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_list");
    const executionId = await startExecutionFromYaml(request, serviceApi, name, e2eWaitWorkflowYaml(name));
    // 一覧の name は実行 ID の検索（部分一致）。
    await page.goto(`/executions?name=${encodeURIComponent(executionId)}`);

    // Act
    await page.getByRole("link", { name: executionId, exact: true }).filter({ visible: true }).click();

    // Assert
    await expect(page).toHaveURL(new RegExp(`/executions/${executionId}$`));
    await expect(executionDetailPage.heading(executionId)).toBeVisible();
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();
  });

  test("キャンセルすると CANCELED になり、成功のトーストが出る", async ({ executionDetailPage, request, serviceApi }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_cancel");
    const executionId = await startExecutionFromYaml(request, serviceApi, name, e2eWaitWorkflowYaml(name));
    await executionDetailPage.goto(executionId);
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();

    // Act
    await executionDetailPage.cancel();

    // Assert
    await expect(executionDetailPage.toasts.withText(toasts.cancelAccepted)).toBeVisible({ timeout: 60_000 });
    await expect(executionDetailPage.statusBadge("CANCELED")).toBeVisible({ timeout: 30_000 });
  });

  test("キャンセルが 409 のときトーストに 409 が出る", async ({ executionDetailPage, page, request, serviceApi }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_409");
    const executionId = await startExecutionFromYaml(request, serviceApi, name, e2eWaitWorkflowYaml(name));
    await page.route("**/api/core/executions/*/cancel", (route) =>
      route.fulfill({
        status: 409,
        contentType: "application/json",
        body: JSON.stringify({ error: { code: "IDEMPOTENCY_KEY_CONFLICT", message: "conflict for e2e" } })
      })
    );
    await executionDetailPage.goto(executionId);
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();

    // Act
    await executionDetailPage.cancel();

    // Assert
    await expect(page.getByText(/409/)).toBeVisible({ timeout: 15_000 });
  });

  test("待機が 1 つの実行で、ノードの「このノードへ送る」から送ると実行が COMPLETED になる", async ({
    executionDetailPage,
    page,
    request,
    serviceApi
  }) => {
    // Arrange
    const dialog = executionDetailPage.sendEventDialog;
    const name = uniqueDefinitionName("e2e_ui_wait1");
    const executionId = await startExecutionFromYaml(request, serviceApi, name, e2eWaitWorkflowYaml(name));
    await waitForWaitingNodes(request, serviceApi, executionId, ["WaitNode"]);
    await executionDetailPage.goto(executionId);
    await expect(page.getByText(waitNotice.singleTitle("WaitNode"))).toBeVisible({ timeout: 15_000 });
    await executionDetailPage.selectNode("WaitNode");
    await executionDetailPage.openSendEventForSelectedNode();
    await expect(dialog.nodeOption("WaitNode", "ResumeEvt")).toBeChecked();

    // Act
    await dialog.submit();

    // Assert
    await expect(executionDetailPage.toasts.withText(toasts.resumeAccepted)).toBeVisible({ timeout: 30_000 });
    await waitForExecutionStatus(request, serviceApi, executionId, "COMPLETED");
    await expect(executionDetailPage.statusBadge("COMPLETED")).toBeVisible({ timeout: 30_000 });
    await expect(executionDetailPage.sendEventButton).toHaveCount(0);
  });

  test("並列分岐で待機が 2 つの実行で、1 つずつ送ると残りの待機が残り、両方送ると COMPLETED になる", async ({
    executionDetailPage,
    page,
    request,
    serviceApi
  }) => {
    // Arrange
    const dialog = executionDetailPage.sendEventDialog;
    const name = uniqueDefinitionName("e2e_ui_wait2");
    const executionId = await startExecutionFromYaml(request, serviceApi, name, e2eParallelWaitWorkflowYaml(name));
    await waitForWaitingNodes(request, serviceApi, executionId, ["WaitA", "WaitB"]);
    await executionDetailPage.goto(executionId);
    await expect(page.getByText(waitNotice.multiTitle(2))).toBeVisible({ timeout: 15_000 });

    // Act: WaitA へノードの「このノードへ送る」から送る
    await executionDetailPage.selectNode("WaitA");
    await executionDetailPage.openSendEventForSelectedNode();
    await expect(dialog.nodeOption("WaitA", "EvtA")).toBeChecked();
    await expect(dialog.nodeOption("WaitB", "EvtB")).not.toBeChecked();
    await dialog.submit();

    // Assert: WaitB だけが待機に残る
    await expect(executionDetailPage.toasts.withText(toasts.resumeAccepted)).toBeVisible({ timeout: 30_000 });
    await waitForWaitingNodes(request, serviceApi, executionId, ["WaitB"]);
    await expect(page.getByText(waitNotice.singleTitle("WaitB"))).toBeVisible({ timeout: 30_000 });
    await expect(executionDetailPage.statusBadge("ACTIVE")).toBeVisible();

    // Act: 残った WaitB へ見出しの「イベントを送信」から送る
    await executionDetailPage.openSendEvent();
    await expect(dialog.nodeOption("WaitB", "EvtB")).toBeChecked();
    await expect(dialog.nodeOption("WaitA", "EvtA")).toHaveCount(0);
    await dialog.submit();

    // Assert: 合流して実行が終わる
    await waitForExecutionStatus(request, serviceApi, executionId, "COMPLETED");
    await expect(executionDetailPage.statusBadge("COMPLETED")).toBeVisible({ timeout: 30_000 });
  });
});
