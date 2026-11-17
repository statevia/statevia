// メタブロック検査（check-doc-meta.mjs）の単体テスト。`node --test "scripts/docs/__tests__/*.test.mjs"` で動かす。
import assert from "node:assert/strict";
import { describe, test } from "node:test";

import { checkDocument, isValidDate } from "../check-doc-meta.mjs";

/**
 * 検査用の Markdown を組み立てる。
 * @param {string[]} metaRows メタブロックの本体の行（`| 種別 | Guide |` など）
 * @param {string[]} [bodyLines] 区切り線の後の本文
 * @returns {string} Markdown
 */
function buildDocument(metaRows, bodyLines = []) {
  return ["# タイトル", "", "| 項目 | 値 |", "| --- | --- |", ...metaRows, "", "---", "", ...bodyLines].join("\n");
}

const VALID_ROWS = ["| 種別 | Guide |", "| Version | 1.2 |", "| 更新日 | 2026-11-17 |"];

describe("checkDocument", () => {
  test("正しいメタブロックと一致する変更履歴なら問題を返さない", () => {
    // Arrange
    const content = buildDocument(VALID_ROWS, ["**Version 1.2（2026-11-17）**: 変更の要約。"]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.deepEqual(problems, []);
  });

  test("x.y.z の Version と CRLF の改行を受け付ける", () => {
    // Arrange
    const rows = ["| 種別 | Specification |", "| Version | 1.5.20 |", "| 更新日 | 2026-11-13 |"];
    const content = buildDocument(rows, ["**Version 1.5.20（2026-11-13）**: 変更の要約。"]).replaceAll("\n", "\r\n");

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.deepEqual(problems, []);
  });

  test("変更履歴が無ければ Version と比べない", () => {
    // Arrange
    const content = buildDocument(VALID_ROWS, ["本文だけ。"]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.deepEqual(problems, []);
  });

  test("タイトル直後にメタブロックが無ければ 1 行目の問題を返す", () => {
    // Arrange
    const content = ["# タイトル", "", "Version: 1.0", "", "本文"].join("\n");

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.equal(problems.length, 1);
    assert.equal(problems[0].line, 1);
    assert.match(problems[0].message, /メタブロックがありません/);
  });

  test("必須の項目が欠けていれば項目ごとに問題を返す", () => {
    // Arrange
    const content = buildDocument(["| 種別 | Decision |", "| ステータス | 採用 |"]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.deepEqual(
      problems.map((problem) => problem.message),
      ["メタブロックに「Version」がありません", "メタブロックに「更新日」がありません"],
    );
  });

  test("7 種以外の種別なら種別の行を指して問題を返す", () => {
    // Arrange
    const content = buildDocument(["| 種別 | Tutorial |", "| Version | 1.0 |", "| 更新日 | 2026-11-17 |"]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.equal(problems.length, 1);
    assert.equal(problems[0].line, 5);
    assert.match(problems[0].message, /種別「Tutorial」は使えません/);
  });

  test("Version が数字の x.y でも x.y.z でもなければ問題を返す", () => {
    // Arrange
    const content = buildDocument(["| 種別 | Guide |", "| Version | v1 |", "| 更新日 | 2026-11-17 |"]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.equal(problems.length, 1);
    assert.match(problems[0].message, /x\.y か x\.y\.z/);
  });

  test("Version が最初の変更履歴と違えば、履歴の行番号つきで問題を返す", () => {
    // Arrange
    const content = buildDocument(VALID_ROWS, [
      "**Version 1.3（2026-11-18）**: 新しい変更。",
      "",
      "**Version 1.2（2026-11-17）**: 前の変更。",
    ]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.equal(problems.length, 1);
    assert.equal(problems[0].line, 6);
    assert.match(problems[0].message, /最初の変更履歴「1\.3」（11 行目）と一致しません/);
  });

  test("更新日が YYYY-MM-DD でなければ問題を返す", () => {
    // Arrange
    const content = buildDocument(["| 種別 | Guide |", "| Version | 1.0 |", "| 更新日 | 2026/11/17 |"]);

    // Act
    const problems = checkDocument(content);

    // Assert
    assert.equal(problems.length, 1);
    assert.match(problems[0].message, /更新日「2026\/11\/17」/);
  });
});

describe("isValidDate", () => {
  test("暦に存在する日付なら true を返す", () => {
    // Arrange
    const leapDay = "2028-02-29";

    // Act
    const result = isValidDate(leapDay);

    // Assert
    assert.equal(result, true);
  });

  test("形式は正しくても暦に無い日付なら false を返す", () => {
    // Arrange
    const nonexistentDay = "2026-02-30";

    // Act
    const result = isValidDate(nonexistentDay);

    // Assert
    assert.equal(result, false);
  });
});
