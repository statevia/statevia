import { test as base } from "@playwright/test";
import { ExecutionDetailPage } from "../pages/ExecutionDetailPage";

type E2EFixtures = {
  /** 実行詳細の Page Object。 */
  executionDetailPage: ExecutionDetailPage;
};

export const test = base.extend<E2EFixtures>({
  executionDetailPage: async ({ page }, use) => {
    await use(new ExecutionDetailPage(page));
  },
});

export { expect } from "@playwright/test";
