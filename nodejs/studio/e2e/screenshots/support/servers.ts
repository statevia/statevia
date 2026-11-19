/**
 * 撮影で起動するサーバーのポートと URL。設定（`playwright.screenshots.config.ts`）と spec の両方が読む。
 * @remarks 既定のポートは、通常の E2E（3200）とローカルの Docker Compose（3000・3001・3100）と重ならない値にした。
 */

/** 撮影で起動する Studio（next dev）のポート。`SCREENSHOT_PORT` で変えられる。 */
export const SCREENSHOT_STUDIO_PORT = Number(process.env.SCREENSHOT_PORT ?? "3210");

/** サーバー側の `GET /v1/auth/me` に答えるスタブのポート。`SCREENSHOT_STUB_PORT` で変えられる。 */
export const SCREENSHOT_STUB_PORT = Number(process.env.SCREENSHOT_STUB_PORT ?? "3299");

/** スタブの URL。Next の `SERVICE_API_INTERNAL_BASE` に渡す。IPv6 で引かれて繋がらないのを避けるため 127.0.0.1 にする。 */
export const SCREENSHOT_STUB_BASE = `http://127.0.0.1:${SCREENSHOT_STUB_PORT}`;

/** 撮影で開く Studio の URL。 */
export const SCREENSHOT_STUDIO_BASE = `http://localhost:${SCREENSHOT_STUDIO_PORT}`;
