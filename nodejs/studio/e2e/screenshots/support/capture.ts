import { mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import type { APIRequestContext, BrowserContext, Page } from "@playwright/test";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "@/shared/auth/authSession";
import { commonResponses, screenshotUser, type ScreenshotEntry, type ScreenshotLocale } from "../manifest";
import { SCREENSHOT_STUB_BASE, SCREENSHOT_STUDIO_BASE } from "./servers";

/** 画像の置き場所（`docs/images/screenshots/`）。 */
export const SCREENSHOT_OUTPUT_DIRECTORY = fileURLToPath(new URL("../../../../../docs/images/screenshots/", import.meta.url));

/**
 * 1 枚の上限（200KB）。
 * @remarks README と UI ガイドを開いたときの読み込みを抑えるため。執筆標準（`docs/DOCUMENTATION-STANDARD.md` の「画像」）と同じ値。
 */
export const SCREENSHOT_MAX_BYTES = 200 * 1024;

/**
 * WebP の品質（0〜1）。
 * @remarks 1280×800 の画面で 200KB に収まり、文字がにじまない値として選んだ。
 */
const WEBP_QUALITY = 0.85;

/** Studio の表示言語の Cookie（`app/layout.tsx` が読む）。 */
const UI_LANGUAGE_COOKIE = "ui-lang";

const json = (body: unknown, status = 200) => ({ status, contentType: "application/json", body: JSON.stringify(body) });

/**
 * 撮影の前に、サインイン済みの状態と API の応答を用意する。
 * @remarks
 * - Cookie のアクセストークンはダミー。サーバー側の `auth/me` はスタブが答え、値を検証しない。
 * - `/api/core/**` のうち manifest に無いものは `route.fallback()` で `fixtures/test.ts` の受け皿へ渡し、シナリオを失敗にする。
 * - 実行の SSE（`.../stream`）は空のストリームで答える（画面は取得済みの値で描く）。
 * @param page 対象のページ。
 * @param context ページのブラウザコンテキスト。
 * @param entry 撮る画面。
 * @param locale 撮る言語。Studio の表示言語の Cookie（`ui-lang`）に置く。
 */
export async function prepareScreenshot(
  page: Page,
  context: BrowserContext,
  entry: ScreenshotEntry,
  locale: ScreenshotLocale
): Promise<void> {
  await context.addCookies([
    { name: AUTH_COOKIE_ACCESS, value: "docs-screenshots-dummy-token", url: SCREENSHOT_STUDIO_BASE },
    { name: AUTH_COOKIE_TENANT_KEY, value: screenshotUser.tenantKey, url: SCREENSHOT_STUDIO_BASE },
    { name: UI_LANGUAGE_COOKIE, value: locale, url: SCREENSHOT_STUDIO_BASE }
  ]);
  const responses = { ...commonResponses, ...entry.responses };
  await page.route("**/api/auth/me", (route) => route.fulfill(json(screenshotUser)));
  await page.route("**/api/core/**", (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/api\/core/, "");
    if (path.endsWith("/stream")) {
      return route.fulfill({ status: 200, contentType: "text/event-stream", body: "" });
    }
    const responder = request.method() === "GET" ? responses[path] : undefined;
    return responder ? route.fulfill(json(responder(url.searchParams))) : route.fallback();
  });
}

/**
 * 画面を撮り、WebP にして `docs/images/screenshots/<fileName>.webp` に書く。
 * @remarks
 * - 撮る前に next dev の開発表示（`nextjs-portal`）を隠し、Web フォントの読み込みを待つ。アニメーションとカーソルは止める。
 * - WebP への変換はページの canvas（Chromium の符号化）で行い、画像処理の依存を足さない。
 * @param page 撮るページ。準備（`scenes.ts`）が済んでいること。
 * @param fileName 画像のファイル名（拡張子なし。manifest の `screenshotFileName`）。
 * @returns 書いた WebP のバイト数。
 */
export async function captureWebp(page: Page, fileName: string): Promise<number> {
  await page.addStyleTag({ content: "nextjs-portal { display: none !important; }" });
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  const png = await page.screenshot({ animations: "disabled", caret: "hide" });
  const dataUrl = await page.evaluate(
    async ({ base64, quality }) => {
      const image = new Image();
      image.src = `data:image/png;base64,${base64}`;
      await image.decode();
      const canvas = document.createElement("canvas");
      canvas.width = image.naturalWidth;
      canvas.height = image.naturalHeight;
      canvas.getContext("2d")?.drawImage(image, 0, 0);
      return canvas.toDataURL("image/webp", quality);
    },
    { base64: png.toString("base64"), quality: WEBP_QUALITY }
  );
  const webpPrefix = "data:image/webp;base64,";
  if (!dataUrl.startsWith(webpPrefix)) {
    // canvas は符号化できない形式を黙って PNG にするので、WebP でなければ止める。
    throw new Error(`${fileName}: ブラウザが WebP に符号化しなかった（${dataUrl.slice(0, 30)}）`);
  }
  const webp = Buffer.from(dataUrl.slice(webpPrefix.length), "base64");
  mkdirSync(SCREENSHOT_OUTPUT_DIRECTORY, { recursive: true });
  writeFileSync(`${SCREENSHOT_OUTPUT_DIRECTORY}${fileName}.webp`, webp);
  return webp.length;
}

/**
 * サーバー側（Next）がスタブへ送った、`auth/me` 以外の要求を返す。
 * @param request Playwright の API 要求の口。
 * @returns 想定外の要求（`メソッド パス`）。空なら問題なし。
 */
export async function readUnexpectedServerCalls(request: APIRequestContext): Promise<string[]> {
  const response = await request.get(`${SCREENSHOT_STUB_BASE}/__unexpected`);
  return (await response.json()) as string[];
}
