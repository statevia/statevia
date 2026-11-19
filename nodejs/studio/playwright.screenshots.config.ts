import { defineConfig, devices } from "@playwright/test";
import { screenshotUser } from "./e2e/screenshots/manifest";
import {
  SCREENSHOT_STUB_BASE,
  SCREENSHOT_STUB_PORT,
  SCREENSHOT_STUDIO_BASE,
  SCREENSHOT_STUDIO_PORT
} from "./e2e/screenshots/support/servers";

/**
 * 文書のスクリーンショットを撮る Playwright の設定（`npm run docs:screenshots`）。
 *
 * - 撮る画面と応答は `e2e/screenshots/manifest.ts`。全サービスは起動しない。
 * - 通常の E2E（`playwright.config.ts`）とは別の設定で、`npm run test:e2e` には含まれない。
 * - Next は開発用の認証バイパスで画面へ入る。サーバー側が呼ぶ `GET /v1/auth/me` だけは、
 *   `e2e/screenshots/server/authMeStub.mjs` が manifest の利用者で答える。
 */
export default defineConfig({
  testDir: "./e2e/screenshots",
  testMatch: /.*\.spec\.ts/,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [["list"]],
  // next dev は初回の表示でコンパイルするので、web-first assertion の待ちを延ばす。
  expect: { timeout: 20_000 },
  use: {
    ...devices["Desktop Chrome"],
    baseURL: SCREENSHOT_STUDIO_BASE,
    viewport: { width: 1280, height: 800 },
    deviceScaleFactor: 1,
    colorScheme: "light",
    locale: "ja-JP",
    timezoneId: "Asia/Tokyo",
    trace: "retain-on-failure"
  },
  webServer: [
    {
      command: "node e2e/screenshots/server/authMeStub.mjs",
      url: `${SCREENSHOT_STUB_BASE}/__health`,
      reuseExistingServer: !process.env.CI,
      env: {
        ...process.env,
        SCREENSHOT_STUB_PORT: String(SCREENSHOT_STUB_PORT),
        SCREENSHOT_AUTH_ME: JSON.stringify(screenshotUser)
      }
    },
    {
      command: `npx next dev --port ${SCREENSHOT_STUDIO_PORT}`,
      url: SCREENSHOT_STUDIO_BASE,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ...process.env,
        SERVICE_API_INTERNAL_BASE: SCREENSHOT_STUB_BASE,
        // 開発時の認証バイパス（middleware と /api/auth/session）。値はスタブにしか届かないダミー。
        SERVICE_API_AUTH_TOKEN: "docs-screenshots-dev-auth-bypass"
      }
    }
  ]
});
