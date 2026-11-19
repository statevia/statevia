import { expect, test } from "../fixtures/test";
import { SCREENSHOT_NOW } from "./data/common";
import { screenshotManifest } from "./manifest";
import { screenshotScenes, sceneFor } from "./scenes";
import { captureWebp, prepareScreenshot, readUnexpectedServerCalls, SCREENSHOT_MAX_BYTES } from "./support/capture";

// 文書に載せる画面を撮り、docs/images/screenshots/<id>.webp に書く（npm run docs:screenshots）。
// mock の test を使うので、manifest に無い /api/core/** が呼ばれるとシナリオが失敗する。

test("manifest のすべての画面に撮影の場面がある", () => {
  // Arrange
  const manifestIds = screenshotManifest.map((entry) => entry.id);

  // Act
  const sceneIds = Object.keys(screenshotScenes);

  // Assert
  expect([...sceneIds].sort()).toEqual([...manifestIds].sort());
});

for (const entry of screenshotManifest) {
  test(`${entry.id} を架空データで撮ると、200KB 以下の WebP ができる`, async ({ page, context, request }) => {
    // Arrange
    await page.clock.setFixedTime(new Date(SCREENSHOT_NOW));
    await prepareScreenshot(page, context, entry);
    await page.goto(entry.route);
    await sceneFor(entry.id)(page);

    // Act
    const bytes = await captureWebp(page, entry.id);

    // Assert
    expect(bytes, `${entry.id}.webp のサイズ`).toBeLessThanOrEqual(SCREENSHOT_MAX_BYTES);
    expect(await readUnexpectedServerCalls(request), "サーバー側から auth/me 以外が呼ばれた").toEqual([]);
  });
}
