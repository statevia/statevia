/**
 * 文書のスクリーンショットを撮るときに、Next のサーバー側が呼ぶ Service API の代わりをする。
 *
 * - 管理画面と定期実行の `layout.tsx`、画面の外枠は、サーバー側で `GET /v1/auth/me` を呼んで tenant.admin を確かめる。
 *   ブラウザ側の route のモックは届かないので、ここで `SCREENSHOT_AUTH_ME`（manifest の `screenshotUser` の JSON）を返す。
 * - それ以外のパスは呼ばれない前提なので、404 を返して記録する。撮影の spec は最後に `GET /__unexpected` で記録が空かを確かめる。
 * - Node の標準だけを使う。外部には通信しない。
 *
 * 使い方: `SCREENSHOT_AUTH_ME='{"...":...}' SCREENSHOT_STUB_PORT=3299 node e2e/screenshots/server/authMeStub.mjs`
 */
import { createServer } from "node:http";

const port = Number(process.env.SCREENSHOT_STUB_PORT ?? "3299");
const authMe = process.env.SCREENSHOT_AUTH_ME;
if (!authMe) {
  throw new Error("SCREENSHOT_AUTH_ME is required");
}

/** 呼ばれる前提でなかった要求（`メソッド パス`）。 */
const unexpected = [];

const server = createServer((request, response) => {
  const target = `${request.method} ${request.url}`;
  switch (target) {
    case "GET /v1/auth/me":
      response.writeHead(200, { "Content-Type": "application/json" });
      response.end(authMe);
      return;
    case "GET /__health":
      response.writeHead(200, { "Content-Type": "text/plain" });
      response.end("ok");
      return;
    case "GET /__unexpected":
      response.writeHead(200, { "Content-Type": "application/json" });
      response.end(JSON.stringify(unexpected));
      return;
    default:
      unexpected.push(target);
      response.writeHead(404, { "Content-Type": "application/json" });
      response.end(JSON.stringify({ error: { code: "NOT_FOUND", message: "not stubbed for screenshots" } }));
  }
});

server.listen(port, "127.0.0.1");
