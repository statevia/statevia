#!/usr/bin/env node
/**
 * docs/ の Markdown のメタブロックを検査する。
 *
 * 書式の正本は docs/DOCUMENTATION-STANDARD.md の「メタブロック」。検査するのは次の 4 点だけで、
 * 関連（`| 関連 |`）の有無と本文の書き方は検査しない。
 *
 * - タイトル（`# `）の直後に `| 項目 | 値 |` の表がある
 * - 種別が Guide / Concept / Architecture / Reference / Specification / Decision / Future のどれか
 * - Version が `x.y` か `x.y.z` で、本文の最初の変更履歴 `**Version …（…）**` と一致する（履歴が無ければ比べない）
 * - 更新日が `YYYY-MM-DD` で、暦に存在する日付
 *
 * 使い方: `node scripts/docs/check-doc-meta.mjs [paths...]`。パスを省くと docs/ 配下の全 Markdown を検査する。
 * 失敗があれば `ファイル:行: 理由` を標準エラーに出し、終了コード 1 で終わる。外部には通信しない。
 */
import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** メタブロックの種別として許す値。docs/README.md の「知識種別」と同じ 7 種。 */
export const ALLOWED_KINDS = Object.freeze([
  "Guide",
  "Concept",
  "Architecture",
  "Reference",
  "Specification",
  "Decision",
  "Future",
]);

/**
 * メタブロックを持たない docs/ 直下のファイル。
 * README.md は索引、DOCUMENTATION-STANDARD.md は本検査の正本なので、標準の対象外としている。
 */
export const EXCLUDED_RELATIVE_PATHS = Object.freeze(["docs/README.md", "docs/DOCUMENTATION-STANDARD.md"]);

const VERSION_PATTERN = /^\d+\.\d+(\.\d+)?$/;
const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const HISTORY_PATTERN = /^\*\*Version (\S+?)（/;
const TABLE_ROW_PATTERN = /^\|(.*)\|\s*$/;

/**
 * `YYYY-MM-DD` が暦に存在する日付かを返す。
 * @param {string} value 検査する文字列
 * @returns {boolean} 形式が正しく、存在する日付なら true
 */
export function isValidDate(value) {
  const match = DATE_PATTERN.exec(value);
  if (match === null) {
    return false;
  }
  const [year, month, day] = match.slice(1).map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

/**
 * 表の 1 行をセルに分ける。表の行でなければ null を返す。
 * @param {string} line Markdown の 1 行
 * @returns {string[] | null} 前後の空白を除いたセル
 */
function splitTableRow(line) {
  const match = TABLE_ROW_PATTERN.exec(line.trim());
  return match === null ? null : match[1].split("|").map((cell) => cell.trim());
}

/**
 * タイトル直後のメタブロックの表を読む。
 * @param {string[]} lines ファイルの行
 * @returns {{ headerLine: number, rows: Map<string, { value: string, line: number }> } | null}
 *   表が無ければ null。行番号は 1 始まり。
 */
function readMetaTable(lines) {
  const titleIndex = lines.findIndex((line) => line.startsWith("# "));
  if (titleIndex === -1) {
    return null;
  }
  const headerIndex = lines.findIndex((line, index) => index > titleIndex && line.trim() !== "");
  const header = headerIndex === -1 ? null : splitTableRow(lines[headerIndex]);
  if (header === null || header[0] !== "項目" || header[1] !== "値") {
    return null;
  }
  const bodyStart = headerIndex + 2;
  const bodyEnd = lines.findIndex((line, index) => index >= bodyStart && splitTableRow(line) === null);
  const bodyLines = lines.slice(bodyStart, bodyEnd === -1 ? lines.length : bodyEnd);
  const rows = new Map(
    bodyLines
      .map((line, offset) => ({ cells: splitTableRow(line), line: bodyStart + offset + 1 }))
      .filter(({ cells }) => cells !== null && cells.length >= 2)
      .map(({ cells, line }) => [cells[0], { value: cells[1], line }]),
  );
  return { headerLine: headerIndex + 1, rows };
}

/**
 * checkDocument が返す問題 1 件の形。
 * @typedef {object} MetaProblem
 * @property {number} line 問題のある行（1 始まり）。ファイル全体の問題は 1。
 * @property {string} message 理由と直し方。
 */

/**
 * 1 つの Markdown のメタブロックを検査する。
 * @param {string} content ファイルの中身
 * @returns {MetaProblem[]} 見つかった問題。問題が無ければ空。
 */
export function checkDocument(content) {
  const lines = content.split(/\r?\n/);
  const table = readMetaTable(lines);
  if (table === null) {
    return [
      {
        line: 1,
        message: "タイトル（# ）の直後に `| 項目 | 値 |` のメタブロックがありません。DOCUMENTATION-STANDARD.md の「メタブロック」を足してください",
      },
    ];
  }

  const kind = table.rows.get("種別");
  const version = table.rows.get("Version");
  const updatedOn = table.rows.get("更新日");
  const missing = [
    ["種別", kind],
    ["Version", version],
    ["更新日", updatedOn],
  ]
    .filter(([, row]) => row === undefined)
    .map(([key]) => ({ line: table.headerLine, message: `メタブロックに「${key}」がありません` }));

  const kindProblems =
    kind !== undefined && !ALLOWED_KINDS.includes(kind.value)
      ? [{ line: kind.line, message: `種別「${kind.value}」は使えません。${ALLOWED_KINDS.join(" / ")} のどれかにしてください` }]
      : [];

  const versionFormatValid = version !== undefined && VERSION_PATTERN.test(version.value);
  const versionFormatProblems =
    version !== undefined && !versionFormatValid
      ? [{ line: version.line, message: `Version「${version.value}」は x.y か x.y.z の形式にしてください` }]
      : [];

  const historyIndex = lines.findIndex((line) => HISTORY_PATTERN.test(line));
  const latestHistory = historyIndex === -1 ? null : HISTORY_PATTERN.exec(lines[historyIndex])[1];
  const historyProblems =
    versionFormatValid && latestHistory !== null && latestHistory !== version.value
      ? [
          {
            line: version.line,
            message: `Version「${version.value}」が最初の変更履歴「${latestHistory}」（${historyIndex + 1} 行目）と一致しません。どちらかの更新漏れです`,
          },
        ]
      : [];

  const dateProblems =
    updatedOn !== undefined && !isValidDate(updatedOn.value)
      ? [{ line: updatedOn.line, message: `更新日「${updatedOn.value}」は存在する日付を YYYY-MM-DD で書いてください` }]
      : [];

  return [...missing, ...kindProblems, ...versionFormatProblems, ...historyProblems, ...dateProblems];
}

/**
 * ディレクトリ配下の Markdown を再帰的に集める。
 * @param {string} directory 起点のディレクトリ
 * @returns {string[]} Markdown のパス
 */
function collectMarkdownFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return collectMarkdownFiles(entryPath);
    }
    return entry.isFile() && entry.name.endsWith(".md") ? [entryPath] : [];
  });
}

/**
 * 検査対象のファイルを決める。パスの指定が無ければ docs/ 配下の全 Markdown（対象外を除く）。
 * @param {string} repositoryRoot リポジトリのルート
 * @param {string[]} requestedPaths コマンドラインで渡されたパス
 * @returns {string[]} リポジトリのルートからの相対パス（区切りは `/`）
 */
export function resolveTargets(repositoryRoot, requestedPaths) {
  const candidates =
    requestedPaths.length > 0
      ? requestedPaths.flatMap((requested) => {
          const absolute = path.resolve(repositoryRoot, requested);
          return statSync(absolute).isDirectory() ? collectMarkdownFiles(absolute) : [absolute];
        })
      : collectMarkdownFiles(path.join(repositoryRoot, "docs"));
  return candidates
    .map((absolute) => path.relative(repositoryRoot, absolute).split(path.sep).join("/"))
    .filter((relative) => relative.startsWith("docs/") && !EXCLUDED_RELATIVE_PATHS.includes(relative))
    .sort();
}

/**
 * コマンドラインの入口。
 * @param {string[]} argv `process.argv.slice(2)` 相当
 * @returns {number} 終了コード
 */
function main(argv) {
  const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
  const targets = resolveTargets(repositoryRoot, argv);
  const problems = targets.flatMap((relative) =>
    checkDocument(readFileSync(path.join(repositoryRoot, relative), "utf8")).map(
      (problem) => `${relative}:${problem.line}: ${problem.message}`,
    ),
  );
  problems.forEach((problem) => console.error(problem));
  console.log(`メタブロックを検査しました: ${targets.length} ファイル、問題 ${problems.length} 件`);
  return problems.length === 0 ? 0 : 1;
}

if (process.argv[1] !== undefined && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = main(process.argv.slice(2));
}
