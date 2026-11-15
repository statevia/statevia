import { randomUUID } from "node:crypto";
import { test, expect } from "@playwright/test";
import { e2eWaitWorkflowYaml } from "./fixtures/e2eWaitWorkflowYaml";
import {
  createDefinition,
  loginServiceApi,
  serviceApiBase,
  serviceApiE2EEnabled,
  serviceApiE2ESkipReason,
  startExecutionFromYaml,
  uniqueDefinitionName,
  waitForExecutionStatus,
  type ServiceApiSession
} from "./fixtures/serviceApi";

/**
 * Service API を直接叩くオプション E2E。
 * 前提と実行例は `e2e/README.md`（SERVICE_API_E2E_URL とログインの資格情報が要る）。
 */
test.describe("Service API (direct)", () => {
  test.describe.configure({ mode: "serial" });
  // Reason: 実体の Service API とログインの資格情報が無いときは、環境ゲートでスイートをスキップする。
  test.skip(!serviceApiE2EEnabled, serviceApiE2ESkipReason);

  let session: ServiceApiSession;

  test.beforeAll(async ({ request }) => {
    session = await loginServiceApi(request);
  });

  test("GET /v1/health は ok を返す", async ({ request }) => {
    // Act
    const res = await request.get(`${serviceApiBase}/v1/health`);

    // Assert
    expect(res.ok()).toBeTruthy();
    const json = (await res.json()) as { status?: string };
    expect(json.status).toBe("ok");
  });

  test("GET /v1/executions?limit=1 はログイン済みなら 200", async ({ request }) => {
    // Act
    const res = await request.get(`${serviceApiBase}/v1/executions?limit=1&offset=0`, { headers: session.headers });

    // Assert
    expect(res.status()).toBe(200);
  });

  test("実行開始 → キャンセル → CANCELED", async ({ request }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_wait");
    const executionId = await startExecutionFromYaml(request, session, name, e2eWaitWorkflowYaml(name));

    // Act
    const cancel = await request.post(`${serviceApiBase}/v1/executions/${encodeURIComponent(executionId)}/cancel`, {
      headers: { ...session.headers, "X-Idempotency-Key": randomUUID() },
      data: { reason: "e2e" }
    });

    // Assert
    expect(cancel.ok(), await cancel.text()).toBeTruthy();
    await waitForExecutionStatus(request, session, executionId, "CANCELED", 15_000);
  });

  test("同じ X-Idempotency-Key と同じ本文の再送は同じ実行を返す", async ({ request }) => {
    // Arrange
    const name = uniqueDefinitionName("e2e_idem");
    const definitionId = await createDefinition(request, session, name, e2eWaitWorkflowYaml(name));
    const headers = { ...session.headers, "X-Idempotency-Key": `idem-${randomUUID()}` };
    const body = { definitionId, input: { k: 1 } };

    // Act
    const start1 = await request.post(`${serviceApiBase}/v1/executions`, { headers, data: body });
    const start2 = await request.post(`${serviceApiBase}/v1/executions`, { headers, data: body });

    // Assert
    expect(start1.status(), await start1.text()).toBe(201);
    expect(start2.status(), await start2.text()).toBe(201);
    const execution1 = (await start1.json()) as { displayId: string; resourceId: string };
    const execution2 = (await start2.json()) as { displayId: string; resourceId: string };
    expect(execution2.displayId).toBe(execution1.displayId);
    expect(execution2.resourceId).toBe(execution1.resourceId);
  });

  test("同じ X-Idempotency-Key で本文が違うと 409", async ({ request }) => {
    // Arrange
    const nameA = uniqueDefinitionName("e2e_idem_a");
    const nameB = uniqueDefinitionName("e2e_idem_b");
    const definitionA = await createDefinition(request, session, nameA, e2eWaitWorkflowYaml(nameA));
    const definitionB = await createDefinition(request, session, nameB, e2eWaitWorkflowYaml(nameB));
    const headers = { ...session.headers, "X-Idempotency-Key": `idem-conflict-${randomUUID()}` };
    const start1 = await request.post(`${serviceApiBase}/v1/executions`, {
      headers,
      data: { definitionId: definitionA, input: {} }
    });
    expect(start1.status(), await start1.text()).toBe(201);

    // Act
    const start2 = await request.post(`${serviceApiBase}/v1/executions`, {
      headers,
      data: { definitionId: definitionB, input: {} }
    });

    // Assert
    expect(start2.status()).toBe(409);
    const error = (await start2.json()) as { error?: { code?: string } };
    expect(error.error?.code).toBe("IDEMPOTENCY_KEY_CONFLICT");
  });
});
