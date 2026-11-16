import { test, expect } from "./fixtures/test";
import { e2eParallelWaitWorkflowYaml, e2eWaitWorkflowYaml } from "./fixtures/e2eWaitWorkflowYaml";
import {
  loginServiceApi,
  loginStudio,
  serviceApiE2EEnabled,
  serviceApiE2ESkipReason,
  startExecutionFromYaml,
  uniqueDefinitionName,
  waitForExecutionStatus,
  waitForWaitingNodes,
  type ServiceApiSession
} from "./fixtures/serviceApi";
import { uiText } from "@/shared/i18n/uiText";

const toasts = uiText.executionDashboard.toasts;
const waitNotice = uiText.executionDashboard.waitNotice;

/**
 * Service API 実体 + Next プロキシ経由の実行詳細の UI E2E。
 * @remarks 定義の作成と実行の開始は API で行い、画面ではキャンセルとイベント送信を操作する。
 * 前提と実行例は `e2e/README.md`（SERVICE_API_E2E_URL とログインの資格情報が要る）。
 */
test.describe("Service API + UI (real)", () => {
  test.describe.configure({ mode: "serial", timeout: 120_000 });
  // Reason: 実体の Service API とログインの資格情報が無いときは、環境ゲートでスイートをスキップする。
  test.skip(!serviceApiE2EEnabled, serviceApiE2ESkipReason);

  let session: ServiceApiSession;

  test.beforeAll(async ({ request }) => {
    session = await loginServiceApi(request);
  });

  test.beforeEach(async ({ page }) => {
    await loginStudio(page);
  });

  test("実行一覧から実行 ID のリンクで実行詳細へ移る", async ({ executionDetailPage, page, request }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_list");
    const executionId = await startExecutionFromYaml(request, session, name, e2eWaitWorkflowYaml(name));
    // 一覧の name は実行 ID の検索（部分一致）。
    await page.goto(`/executions?name=${encodeURIComponent(executionId)}`);

    // Act
    await page.getByRole("link", { name: executionId, exact: true }).filter({ visible: true }).click();

    // Assert
    await expect(page).toHaveURL(new RegExp(`/executions/${executionId}$`));
    await executionDetailPage.expectLoaded(executionId);
  });

  test("キャンセルすると CANCELED になり、成功のトーストが出る", async ({ executionDetailPage, request }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_cancel");
    const executionId = await startExecutionFromYaml(request, session, name, e2eWaitWorkflowYaml(name));
    await executionDetailPage.goto(executionId);
    await executionDetailPage.expectLoaded(executionId);

    // Act
    await executionDetailPage.cancelButton.click();

    // Assert
    await expect(executionDetailPage.toast(toasts.cancelAccepted)).toBeVisible({ timeout: 60_000 });
    await executionDetailPage.expectExecutionStatus("CANCELED", { timeout: 30_000 });
  });

  test("キャンセルが 409 のときトーストに 409 が出る", async ({ executionDetailPage, page, request }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_409");
    const executionId = await startExecutionFromYaml(request, session, name, e2eWaitWorkflowYaml(name));
    await page.route("**/api/core/executions/*/cancel", (route) =>
      route.fulfill({
        status: 409,
        contentType: "application/json",
        body: JSON.stringify({ error: { code: "IDEMPOTENCY_KEY_CONFLICT", message: "conflict for e2e" } })
      })
    );
    await executionDetailPage.goto(executionId);
    await executionDetailPage.expectLoaded(executionId);

    // Act
    await executionDetailPage.cancelButton.click();

    // Assert
    await expect(page.getByText(/409/)).toBeVisible({ timeout: 15_000 });
  });

  test("待機が 1 つの実行で、ノードの「このノードへ送る」から送ると実行が COMPLETED になる", async ({
    executionDetailPage,
    page,
    request
  }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_wait1");
    const executionId = await startExecutionFromYaml(request, session, name, e2eWaitWorkflowYaml(name));
    await waitForWaitingNodes(request, session, executionId, ["WaitNode"]);
    await executionDetailPage.goto(executionId);
    await executionDetailPage.expectLoaded(executionId);
    await expect(page.getByText(waitNotice.singleTitle("WaitNode"))).toBeVisible({ timeout: 15_000 });
    await executionDetailPage.selectNode("WaitNode");

    // Act
    await executionDetailPage.sendToNodeButton.click();
    await expect(executionDetailPage.sendEventNodeOption("WaitNode", "ResumeEvt")).toBeChecked();
    await executionDetailPage.submitSendEvent();

    // Assert
    await expect(executionDetailPage.toast(toasts.resumeAccepted)).toBeVisible({ timeout: 30_000 });
    await waitForExecutionStatus(request, session, executionId, "COMPLETED");
    await executionDetailPage.expectExecutionStatus("COMPLETED", { timeout: 30_000 });
    await expect(executionDetailPage.sendEventButton).toHaveCount(0);
  });

  test("並列分岐で待機が 2 つの実行で、1 つずつ送ると残りの待機が残り、両方送ると COMPLETED になる", async ({
    executionDetailPage,
    page,
    request
  }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_ui_wait2");
    const executionId = await startExecutionFromYaml(request, session, name, e2eParallelWaitWorkflowYaml(name));
    await waitForWaitingNodes(request, session, executionId, ["WaitA", "WaitB"]);
    await executionDetailPage.goto(executionId);
    await executionDetailPage.expectLoaded(executionId);
    await expect(page.getByText(waitNotice.multiTitle(2))).toBeVisible({ timeout: 15_000 });

    // Act: WaitA へノードの「このノードへ送る」から送る
    await executionDetailPage.selectNode("WaitA");
    await executionDetailPage.sendToNodeButton.click();
    await expect(executionDetailPage.sendEventNodeOption("WaitA", "EvtA")).toBeChecked();
    await expect(executionDetailPage.sendEventNodeOption("WaitB", "EvtB")).not.toBeChecked();
    await executionDetailPage.submitSendEvent();

    // Assert: WaitB だけが待機に残る
    await expect(executionDetailPage.toast(toasts.resumeAccepted)).toBeVisible({ timeout: 30_000 });
    await waitForWaitingNodes(request, session, executionId, ["WaitB"]);
    await expect(page.getByText(waitNotice.singleTitle("WaitB"))).toBeVisible({ timeout: 30_000 });
    await executionDetailPage.expectExecutionStatus("ACTIVE");

    // Act: 残った WaitB へ見出しの「イベントを送信」から送る
    await executionDetailPage.sendEventButton.click();
    await expect(executionDetailPage.sendEventNodeOption("WaitB", "EvtB")).toBeChecked();
    await expect(executionDetailPage.sendEventNodeOption("WaitA", "EvtA")).toHaveCount(0);
    await executionDetailPage.submitSendEvent();

    // Assert: 合流して実行が終わる
    await waitForExecutionStatus(request, session, executionId, "COMPLETED");
    await executionDetailPage.expectExecutionStatus("COMPLETED", { timeout: 30_000 });
  });
});
