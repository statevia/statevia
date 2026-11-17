import { fileURLToPath } from "node:url";

/**
 * 実 API E2E の Service API のオリジン（`SERVICE_API_E2E_URL`）。未設定なら空文字。
 * @remarks `playwright.config.ts` と fixture の両方が読むので、`@playwright/test` に依存しないこのファイルに置く。
 */
export const serviceApiBase = process.env.SERVICE_API_E2E_URL?.replace(/\/$/, "") ?? "";

/**
 * 実 API E2E でログインする資格情報（環境変数）。
 * @remarks ローカルでは Service API の `appsettings.Development.json` の `Statevia:Bootstrap:DevAdmin` の値を渡す。
 * 値はコードにもログにも書かない。
 */
export const serviceApiCredentials = {
  tenantKey: process.env.E2E_TENANT_KEY ?? "",
  username: process.env.E2E_USERNAME ?? "",
  password: process.env.E2E_PASSWORD ?? ""
};

/**
 * 実 API の project（`setup-real` / `real-api`）を定義する条件。オリジンと資格情報がそろっているとき true。
 * @remarks false のとき実 API の spec は project ごと存在せず、「スキップ」として数えない。
 */
export const realApiEnabled =
  serviceApiBase.length > 0 &&
  serviceApiCredentials.tenantKey.length > 0 &&
  serviceApiCredentials.username.length > 0 &&
  serviceApiCredentials.password.length > 0;

/**
 * 実 API の setup が保存し、`real-api` project が読み込むログイン済みの状態（Cookie）。
 * @remarks `e2e/.auth/` は `.gitignore` の対象。認証の Cookie を含むのでコミットしない。
 */
export const studioStorageStatePath = fileURLToPath(new URL("../.auth/studio.json", import.meta.url));
