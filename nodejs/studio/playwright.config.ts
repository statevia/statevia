import { defineConfig, devices, type Project } from "@playwright/test";
import { realApiEnabled, serviceApiBase, studioStorageStatePath } from "./e2e/support/env";

const coreApiInternalBase = process.env.SERVICE_API_INTERNAL_BASE ?? (serviceApiBase || undefined);
/** Playwright 起動の Next が route ハンドラで落ちないよう、未設定時はダミー URL を渡す（mock E2E はブラウザ側 route で吸収）。 */
const coreApiInternalForWebServer = coreApiInternalBase ?? "http://localhost:8080";

/**
 * E2E 用に起動する Next のポート。
 * 既定の 3200 は、ローカルの Docker Compose が使うポート（Studio 3000・Grafana 3001・Loki 3100）と重ならない値として選んだ。
 * 3000 のままだと `reuseExistingServer` が Compose の Studio（イメージのビルド時点のコード）を再利用し、作業中のコードを試さない。
 */
const e2ePort = Number(process.env.E2E_PORT ?? "3200");
const baseURL = `http://localhost:${e2ePort}`;
const desktopChrome = devices["Desktop Chrome"];

/**
 * worker 数。CI は 1。実 API を含むときは Service API と Worker の負荷を抑えて 2。それ以外は Playwright の既定。
 */
function resolveWorkers(): number | undefined {
  if (process.env.CI) return 1;
  return realApiEnabled ? 2 : undefined;
}

/**
 * 実 API の project。オリジンと資格情報がそろうときだけ定義する（`e2e/support/env.ts` の `realApiEnabled`）。
 * `setup-real` が 1 回だけログインして Cookie を保存し、`real-api` の各シナリオはそれを読み込む。
 */
const realApiProjects: Project[] = realApiEnabled
  ? [
      { name: "setup-real", testDir: "./e2e/setup", testMatch: /.*\.setup\.ts/, use: { ...desktopChrome } },
      {
        name: "real-api",
        testDir: "./e2e/specs/real-api",
        dependencies: ["setup-real"],
        use: { ...desktopChrome, storageState: studioStorageStatePath },
      },
    ]
  : [];

export default defineConfig({
  testDir: "./e2e",
  // Reason: gitignore 済みの古いコンパイル出力（e2e/dist の .js）を spec として読み込まないようにする。
  testIgnore: ["**/dist/**"],
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: resolveWorkers(),
  reporter: [["list"], ["html", { open: "never" }]],
  // next dev は初回のページ表示でコンパイルするので、web-first assertion の既定の待ち（5 秒）を延ばす。
  expect: { timeout: 10_000 },
  use: {
    baseURL,
    locale: "ja-JP",
    timezoneId: "Asia/Tokyo",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "mock", testDir: "./e2e/specs/mock", use: { ...desktopChrome } }, ...realApiProjects],
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
