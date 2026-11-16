import type { Page, Route } from "@playwright/test";
import type { AuthMeResponse } from "@/shared/auth/authMe";
import type {
  ExecutionDTO,
  ExecutionEventWithSeq,
  ExecutionGraphDTO,
  ExecutionView,
  PagedExecutions
} from "@/features/executions/types";
import type { ExecutionCounts } from "@/features/executions/api";
import type { ProjectSummary } from "@/shared/project/types";
import { MOCK_EXECUTION_ID, mockExecution, mockExecutionDto, mockExecutionGraph } from "./mockExecution";

/** モックする Studio の API の応答。 */
export type MockExecutionApiOptions = {
  /** ログイン中の利用者の権限キー。既定は閲覧だけ（`executions.write` を持たない）。 */
  permissions?: string[];
  /**
   * `GET /v1/projects` の応答（所属 Project）。既定は空。
   * @remarks 実行一覧は先頭の Project を選んでから一覧を取得する。空のままだと一覧の API を呼ばずに空と表示する。
   */
  projects?: ProjectSummary[];
  /** `GET /v1/executions` の応答（実行一覧）。既定は空の一覧。 */
  executions?: PagedExecutions;
  /** `GET /v1/executions/counts` の応答（状態ごとの件数）。既定はすべて 0。 */
  executionCounts?: ExecutionCounts;
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

const emptyExecutions: PagedExecutions = { items: [], totalCount: 0, offset: 0, limit: 20, hasMore: false };

const emptyExecutionCounts: ExecutionCounts = {
  total: 0,
  byStatus: { ACTIVE: 0, COMPLETED: 0, FAILED: 0, CANCELED: 0, UNKNOWN: 0 },
  waiting: 0,
  needsAttention: 0
};

const json = (route: Route, body: unknown, status = 200) =>
  route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });

/**
 * Service API を使わずに Studio の実行一覧と実行詳細を開くため、ブラウザ側で `/api/core/**` と `/api/auth/me` をモックする。
 * @remarks 扱わないパスは `route.fallback()` で、`fixtures/test.ts` の `mockApi` が先に登録した受け皿へ渡す。
 * 受け皿は呼び出しを記録して 404 を返し（401 だとログイン画面へ飛ぶため）、シナリオの終わりに失敗にする。
 * Next 側の認証は `playwright.config.ts` の開発バイパスで通す。
 * spec からは直接呼ばず、`mockApi.setup(options)` を使う。
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
    const graphId = (options.execution ?? mockExecutionDto).graphId;

    if (request.method() === "POST") {
      commands.push({ path, body: request.postDataJSON() as unknown });
      return json(route, { commandId: `cmd-${commands.length}`, status: "Accepted" }, 202);
    }

    switch (path) {
      case "/projects":
        // 画面の外枠が所属 Project を引く。既定は所属なしにして、Project の切り替えを出さない。
        return json(route, options.projects ?? ([] satisfies ProjectSummary[]));
      case `/graphs/${graphId}`:
        // 定義のグラフは無いことにして、画面を手元の registry の定義へ倒す（useGraphDefinition の既定の経路）。
        return json(route, { error: { code: "NOT_FOUND", message: "graph definition is not mocked" } }, 404);
      case "/executions":
        // 既定は空。パンくずなどで実行一覧へ移るだけのシナリオは中身を検証しない。
        return json(route, options.executions ?? emptyExecutions);
      case "/executions/counts":
        return json(route, options.executionCounts ?? emptyExecutionCounts);
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
        return route.fallback();
    }
  });

  return commands;
}

/** モックしていない `/api/core/**` の呼び出し（`メソッド パス`）。 */
export type UnmockedCalls = readonly string[];

/**
 * `/api/core/**` の受け皿を登録する。個別のモックが扱わない呼び出しを記録し、404 を返す。
 * @remarks Playwright の route は後から登録したものが先に当たるので、`mockExecutionApi` より先に呼ぶ。
 * @param page 対象のページ。
 * @returns 記録した呼び出し。シナリオの終わりに空であることを確かめる。
 */
export async function recordUnmockedCoreApiCalls(page: Page): Promise<UnmockedCalls> {
  const unmocked: string[] = [];
  await page.route("**/api/core/**", (route) => {
    const request = route.request();
    const url = new URL(request.url());
    unmocked.push(`${request.method()} ${url.pathname}${url.search}`);
    return json(route, { error: { code: "NOT_FOUND", message: "not mocked in e2e" } }, 404);
  });
  return unmocked;
}
