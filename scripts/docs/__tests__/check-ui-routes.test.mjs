// 画面一覧の照合（check-ui-routes.mjs）の単体テスト。`node --test "scripts/docs/__tests__/*.test.mjs"` で動かす。
import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { after, describe, test } from "node:test";

import { collectAppRoutes, compareRoutes, normalizeRoute, readDocumentedRoutes, readExcludedRoutes } from "../check-ui-routes.mjs";

/**
 * 「画面の一覧」の節を持つ Markdown を組み立てる。
 * @param {string[]} paths 表の「パス」列に入れる値
 * @returns {string} Markdown
 */
function buildGuide(paths) {
  return [
    "# ガイド",
    "",
    "## 画面の一覧",
    "",
    "| 画面 | パス | 使える利用者 |",
    "| --- | --- | --- |",
    ...paths.map((value) => `| 画面 | \`${value}\` | 全員 |`),
    "",
    "## 次の節",
    "",
    "| 画面 | パス |",
    "| --- | --- |",
    "| 別 | `/not-counted` |",
  ].join("\n");
}

describe("normalizeRoute", () => {
  test("動的な部分は名前を無視して {} にそろえる", () => {
    // Arrange
    const fromApp = "/definitions/[definitionId]/edit";
    const fromDocument = "/definitions/{id}/edit";

    // Act
    const normalized = [normalizeRoute(fromApp), normalizeRoute(fromDocument)];

    // Assert
    assert.deepEqual(normalized, ["/definitions/{}/edit", "/definitions/{}/edit"]);
  });

  test("末尾の / を除き、ルートは / のままにする", () => {
    // Arrange
    const routes = ["/admin/", "/"];

    // Act
    const normalized = routes.map(normalizeRoute);

    // Assert
    assert.deepEqual(normalized, ["/admin", "/"]);
  });
});

describe("collectAppRoutes", () => {
  const root = mkdtempSync(path.join(tmpdir(), "check-ui-routes-"));
  after(() => rmSync(root, { recursive: true, force: true }));

  test("page.tsx を持つディレクトリだけを集め、ルートグループは URL から除く", () => {
    // Arrange
    const files = ["page.tsx", "login/page.tsx", "definitions/[definitionId]/page.tsx", "(auth)/account/page.tsx", "api/route.ts"];
    files.forEach((file) => {
      mkdirSync(path.join(root, path.dirname(file)), { recursive: true });
      writeFileSync(path.join(root, file), "");
    });

    // Act
    const routes = collectAppRoutes(root);

    // Assert
    assert.deepEqual(routes, ["/", "/account", "/definitions/{}", "/login"]);
  });
});

describe("readDocumentedRoutes", () => {
  test("画面の一覧の節の表だけを読み、バッククォートを除いた値と行番号を返す", () => {
    // Arrange
    const markdown = buildGuide(["/login", "/definitions/{definitionId}"]);

    // Act
    const routes = readDocumentedRoutes(markdown);

    // Assert
    assert.deepEqual(routes, [
      { route: "/login", raw: "/login", line: 7 },
      { route: "/definitions/{}", raw: "/definitions/{definitionId}", line: 8 },
    ]);
  });

  test("節が無ければ一致とみなさず失敗する", () => {
    // Arrange
    const markdown = "# ガイド\n\n## 主な画面\n\n| 画面 | パス |\n| --- | --- |\n| a | `/a` |";

    // Act & Assert
    assert.throws(() => readDocumentedRoutes(markdown), /見出し「## 画面の一覧」が見つかりません/);
  });

  test("パスの列を持つ表が無ければ失敗する", () => {
    // Arrange
    const markdown = "## 画面の一覧\n\n| 画面 | URL |\n| --- | --- |\n| a | `/a` |";

    // Act & Assert
    assert.throws(() => readDocumentedRoutes(markdown), /「パス」列を持つ表が見つかりません/);
  });

  test("表に行が無ければ失敗する", () => {
    // Arrange
    const markdown = buildGuide([]);

    // Act & Assert
    assert.throws(() => readDocumentedRoutes(markdown), /表に行がありません/);
  });
});

describe("readExcludedRoutes", () => {
  test("理由つきの項目を照合用のパスにそろえて返す", () => {
    // Arrange
    const json = JSON.stringify({ uiRoutes: [{ value: "/admin/", reason: "転送だけの画面" }] });

    // Act
    const excluded = readExcludedRoutes(json);

    // Assert
    assert.deepEqual([...excluded], ["/admin"]);
  });

  test("理由が空の項目があれば失敗する", () => {
    // Arrange
    const json = JSON.stringify({ uiRoutes: [{ value: "/admin", reason: " " }] });

    // Act & Assert
    assert.throws(() => readExcludedRoutes(json), /value か reason が空/);
  });
});

describe("compareRoutes", () => {
  test("実装と文書が一致すれば問題を返さない", () => {
    // Arrange
    const documented = readDocumentedRoutes(buildGuide(["/", "/definitions/{definitionId}"]));

    // Act
    const problems = compareRoutes(["/", "/definitions/{}"], documented, new Set());

    // Assert
    assert.deepEqual(problems, []);
  });

  test("文書に無い画面、実装に無い画面、重複、不要な除外をそれぞれ返す", () => {
    // Arrange
    const documented = readDocumentedRoutes(buildGuide(["/", "/removed", "/", "/kept"]));
    const excluded = new Set(["/kept", "/gone"]);

    // Act
    const problems = compareRoutes(["/", "/kept", "/added"], documented, excluded);

    // Assert
    assert.equal(problems.length, 5);
    assert.match(problems[0], /画面 \/added が/);
    assert.match(problems[1], /\/removed は Studio に無い画面です/);
    assert.match(problems[2], /\/ が重複しています/);
    assert.match(problems[3], /uiRoutes の \/kept は/);
    assert.match(problems[4], /uiRoutes の \/gone は/);
  });

  test("除外一覧にある画面は、文書に無くても問題にしない", () => {
    // Arrange
    const documented = readDocumentedRoutes(buildGuide(["/"]));

    // Act
    const problems = compareRoutes(["/", "/internal"], documented, new Set(["/internal"]));

    // Assert
    assert.deepEqual(problems, []);
  });
});
