import { defineConfig, devices } from "@playwright/test";

const coreApiE2E = process.env.SERVICE_API_E2E_URL;
const coreApiInternalBase = process.env.SERVICE_API_INTERNAL_BASE ?? coreApiE2E;
/** Playwright 起動の Next が route ハンドラで落ちないよう、未設定時はダミー URL を渡す（mock E2E はブラウザ側 route で吸収）。 */
const coreApiInternalForWebServer =
  (coreApiE2E && coreApiInternalBase ? coreApiInternalBase : process.env.SERVICE_API_INTERNAL_BASE) ??
  "http://localhost:8080";

/**
 * E2E 用に起動する Next のポート。
 * 既定の 3200 は、ローカルの Docker Compose が使うポート（Studio 3000・Grafana 3001・Loki 3100）と重ならない値として選んだ。
 * 3000 のままだと `reuseExistingServer` が Compose の Studio（イメージのビルド時点のコード）を再利用し、作業中のコードを試さない。
 */
const e2ePort = Number(process.env.E2E_PORT ?? "3200");
const baseURL = `http://localhost:${e2ePort}`;

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : process.env.SERVICE_API_E2E_URL ? 2 : undefined,
  reporter: "html",
  use: {
    baseURL,
    locale: "ja-JP",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: `npx next dev --port ${e2ePort}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      ...process.env,
      SERVICE_API_INTERNAL_BASE: coreApiInternalForWebServer.replace(/\/$/, ""),
      // モック E2E はログインしない。開発時の認証バイパス（middleware と /api/auth/session）で画面へ入る。
      // 値は Service API へ届いても通らないダミー。実 API E2E はログインの Cookie が優先されるので影響しない。
      SERVICE_API_AUTH_TOKEN: process.env.SERVICE_API_AUTH_TOKEN ?? "e2e-dev-auth-bypass",
    },
  },
});
