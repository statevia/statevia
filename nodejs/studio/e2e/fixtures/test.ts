import { test as base, expect } from "@playwright/test";
import { ExecutionDetailPage } from "../pages/ExecutionDetailPage";
import { ExecutionsListPage } from "../pages/ExecutionsListPage";
import {
  mockExecutionApi,
  recordUnmockedCoreApiCalls,
  type MockExecutionApiOptions,
  type RecordedCommand
} from "./mockStudioApi";
import { loginServiceApi, type ServiceApiSession } from "./serviceApi";

/** モックの API を組み立てる口。 */
export type MockApi = {
  /**
   * 実行一覧・実行詳細の API と `/api/auth/me` をモックする。
   * @param options 応答の差し替え。省略すると閲覧だけの利用者と既定の実行。
   * @returns 届いたコマンド（POST）の記録。
   */
  setup(options?: MockExecutionApiOptions): Promise<RecordedCommand[]>;
};

/** mock と real-api の両方で使う Page Object。 */
type PageFixtures = {
  /** 実行一覧の Page Object。 */
  executionsListPage: ExecutionsListPage;
  /** 実行詳細の Page Object。 */
  executionDetailPage: ExecutionDetailPage;
};

/** mock project だけで使う fixture。 */
type MockFixtures = {
  /** モックの API。宣言しなくても有効で、モックしていない `/api/core/**` の呼び出しをシナリオの失敗にする。 */
  mockApi: MockApi;
};

/** real-api project だけで使う worker 範囲の fixture。 */
type RealApiWorkerFixtures = {
  /** ログイン済みの Service API の呼び出し口（定義の作成と実行の開始に使う）。 */
  serviceApi: ServiceApiSession;
};

const pageTest = base.extend<PageFixtures>({
  executionsListPage: async ({ page }, use) => {
    await use(new ExecutionsListPage(page));
  },
  executionDetailPage: async ({ page }, use) => {
    await use(new ExecutionDetailPage(page));
  }
});

/**
 * mock project（`e2e/specs/mock`）の `test`。
 * @remarks `mockApi` は auto fixture。受け皿の route を先に登録し、シナリオの終わりに、
 * 個別のモックが扱わなかった呼び出しがあれば一覧を出して失敗にする。
 */
export const test = pageTest.extend<MockFixtures>({
  mockApi: [
    async ({ page }, use) => {
      const unmocked = await recordUnmockedCoreApiCalls(page);
      await use({ setup: (options) => mockExecutionApi(page, options) });
      expect(unmocked, "モックしていない /api/core/** の呼び出しがある。mockExecutionApi に足す").toEqual([]);
    },
    { auto: true }
  ]
});

/**
 * real-api project（`e2e/specs/real-api`）の `test`。モックの fixture を含まない。
 * @remarks ブラウザのログインは `e2e/setup/auth.setup.ts` が保存した Cookie（`storageState`）で済んでいる。
 */
export const realTest = pageTest.extend<object, RealApiWorkerFixtures>({
  serviceApi: [
    async ({ playwright }, use) => {
      const request = await playwright.request.newContext();
      const session = await loginServiceApi(request);
      await use(session);
      await request.dispose();
    },
    { scope: "worker" }
  ]
});

export { expect } from "@playwright/test";
