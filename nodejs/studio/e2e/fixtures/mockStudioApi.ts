import type { Page, Route } from "@playwright/test";
import type { AuthMeResponse } from "@/shared/auth/authMe";
import type {
  ExecutionDTO,
  ExecutionEventWithSeq,
  ExecutionGraphDTO,
  ExecutionView
} from "@/features/executions/types";
import { MOCK_EXECUTION_ID, mockExecution, mockExecutionDto, mockExecutionGraph } from "./mockExecution";

/** モックする実行詳細の API の応答。 */
export type MockExecutionApiOptions = {
  /** ログイン中の利用者の権限キー。既定は閲覧だけ（`executions.write` を持たない）。 */
  permissions?: string[];
  /** `GET /v1/executions/{id}` の応答。 */
  execution?: ExecutionDTO;
  /** `GET /v1/executions/{id}/graph` の応答。 */
  graph?: ExecutionGraphDTO;
  /**
   * `GET /v1/executions/{id}/events` の応答。
   * @param afterSeq クエリの afterSeq。無ければ 0。
   */
  events?: (afterSeq: number) => { events: ExecutionEventWithSeq[]; hasMore: boolean };
  /**
   * `GET /v1/executions/{id}/state?atSeq=` の応答（リプレイ）。
   * @param atSeq クエリの atSeq。
   */
  stateAtSeq?: (atSeq: number) => ExecutionView;
};

/** モックした API に届いたコマンド（POST）の記録。 */
export type RecordedCommand = {
  /** `/api/core` を除いたパス。 */
  path: string;
  /** JSON の本文。 */
  body: unknown;
};

const defaultEvents = () => ({
  events: [
    { seq: 1, type: "GraphUpdated", executionId: MOCK_EXECUTION_ID, patch: { nodes: [] }, at: "2026-01-01T00:00:00Z" },
    { seq: 2, type: "ExecutionStatusChanged", executionId: MOCK_EXECUTION_ID, to: "ACTIVE", at: "2026-01-01T00:00:01Z" }
  ] satisfies ExecutionEventWithSeq[],
  hasMore: false
});

const json = (route: Route, body: unknown, status = 200) =>
  route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });

/**
 * Service API を使わずに Studio の画面を開くため、ブラウザ側で `/api/core/**` と `/api/auth/me` をモックする。
 * @remarks 個別にモックしないパスは 404 を返す。401 を返すとログイン画面へ飛ぶため、既定は 404 にしている。
 * Next 側の認証は `playwright.config.ts` の開発バイパスで通す。
 * @param page 対象のページ。
 * @param options 応答の差し替え。
 * @returns 届いたコマンド（POST）の記録。テストで届け先と本文を確かめる。
 */
export async function mockExecutionApi(page: Page, options: MockExecutionApiOptions = {}): Promise<RecordedCommand[]> {
  const commands: RecordedCommand[] = [];
  const me: AuthMeResponse = {
    tenantId: "tenant-1",
    tenantKey: "default",
    principalId: "principal-1",
    username: "e2e-user",
    email: null,
    isTenantAdmin: false,
    permissions: options.permissions ?? ["executions.read"]
  };

  await page.route("**/api/auth/me", (route) => json(route, me));

  await page.route("**/api/core/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/api\/core/, "");
    const executionPrefix = `/executions/${MOCK_EXECUTION_ID}`;

    if (request.method() === "POST") {
      commands.push({ path, body: request.postDataJSON() as unknown });
      return json(route, { commandId: `cmd-${commands.length}`, status: "Accepted" }, 202);
    }

    switch (path) {
      case executionPrefix:
        return json(route, options.execution ?? mockExecutionDto);
      case `${executionPrefix}/graph`:
        return json(route, options.graph ?? mockExecutionGraph);
      case `${executionPrefix}/events`: {
        const afterSeq = Number(url.searchParams.get("afterSeq") ?? "0");
        return json(route, (options.events ?? defaultEvents)(afterSeq));
      }
      case `${executionPrefix}/state`: {
        const atSeq = Number(url.searchParams.get("atSeq") ?? "0");
        return json(route, options.stateAtSeq ? options.stateAtSeq(atSeq) : mockExecution);
      }
      case `${executionPrefix}/stream`:
        return route.fulfill({ status: 200, contentType: "text/event-stream", body: "" });
      default:
        return json(route, { error: { code: "NOT_FOUND", message: "not mocked in e2e" } }, 404);
    }
  });

  return commands;
}
