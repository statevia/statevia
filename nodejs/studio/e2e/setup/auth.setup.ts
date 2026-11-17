import { test as setup } from "@playwright/test";
import { loginStudio } from "../fixtures/serviceApi";
import { studioStorageStatePath } from "../support/env";

/**
 * 実 API の project の前に 1 回だけ Studio にログインし、Cookie を保存する。
 * @remarks `real-api` project は `storageState` でこの Cookie を読み込むので、シナリオごとにログインしない。
 * 資格情報は環境変数から読む（`e2e/support/env.ts`）。値はログにもレポートにも出さない。
 */
setup("Studio にログインして Cookie を保存する", async ({ page }) => {
  await loginStudio(page);
  await page.context().storageState({ path: studioStorageStatePath });
});
