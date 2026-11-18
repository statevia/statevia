#!/usr/bin/env node
/**
 * Studio の画面（`nodejs/studio/app/**\/page.tsx`）と、`docs/guides/ui-user-guide.md` の「画面の一覧」の表を突き合わせる。
 *
 * - 画面のパスは `app/` からのディレクトリで決まる。ルートグループ（`(name)`）は URL に出ないので除く。
 * - 動的な部分は名前を無視して比べる（`[definitionId]` と文書の `{definitionId}` は同じ）。
 * - 文書は「## 画面の一覧」の節にある、「パス」列を持つ表だけを読む。セルはバッククォートを除いて使う。
 * - 文書に載せないことが正しい画面は、文書と実装の照合テストと共有する除外一覧
 *   （`dotnet/tests/Statevia.Architecture.Tests/Docs/docs-consistency-exclusions.json`）の `uiRoutes` に理由つきで書く。
 *
 * 表が見つからない・空のときは「一致」とみなさず失敗する。外部には通信しない。
 * 使い方: `node scripts/docs/check-ui-routes.mjs`。失敗があれば理由を標準エラーに出し、終了コード 1 で終わる。
 */
import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** 画面の一覧の節の見出し。 */
export const SCREEN_LIST_HEADING = "## 画面の一覧";

/** パスの列の見出し。 */
export const PATH_COLUMN = "パス";

/** 照合する文書のリポジトリ相対パス。 */
export const GUIDE_PATH = "docs/guides/ui-user-guide.md";

/** Studio の App Router のリポジトリ相対パス。 */
export const APP_DIRECTORY = "nodejs/studio/app";

/** 除外一覧のリポジトリ相対パス（C# の照合テストと共有）。 */
export const EXCLUSIONS_PATH = "dotnet/tests/Statevia.Architecture.Tests/Docs/docs-consistency-exclusions.json";

/**
 * 画面のパスを照合用にそろえる。動的な部分を `{}` にし、末尾の `/` を除く（ルートは `/`）。
 * @param {string} route `/definitions/[definitionId]` や `/definitions/{definitionId}` の形のパス
 * @returns {string} 照合用のパス
 */
export function normalizeRoute(route) {
  const replaced = route.trim().replace(/\[[^\]]+\]|\{[^}]+\}/g, "{}");
  const withoutTrailingSlash = replaced.length > 1 ? replaced.replace(/\/+$/, "") : replaced;
  return withoutTrailingSlash === "" ? "/" : withoutTrailingSlash;
}

/**
 * App Router のディレクトリから、`page.tsx` を持つ画面のパスを集める。
 * @param {string} appDirectory `app/` の絶対パス
 * @returns {string[]} 照合用のパス（重複なし、並べ替え済み）
 */
export function collectAppRoutes(appDirectory) {
  /** @type {(directory: string, segments: string[]) => string[]} */
  const walk = (directory, segments) =>
    readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
      if (entry.isFile()) {
        return entry.name === "page.tsx" ? ["/" + segments.join("/")] : [];
      }
      if (!entry.isDirectory() || entry.name.startsWith("_") || entry.name.startsWith("@")) {
        return [];
      }
      const isRouteGroup = entry.name.startsWith("(") && entry.name.endsWith(")");
      return walk(path.join(directory, entry.name), isRouteGroup ? segments : [...segments, entry.name]);
    });
  return [...new Set(walk(appDirectory, []).map(normalizeRoute))].sort();
}

/**
 * Markdown の表の 1 行をセルに分ける。表の行でなければ null。
 * @param {string} line Markdown の 1 行
 * @returns {string[] | null} 前後の空白を除いたセル
 */
function splitTableRow(line) {
  const trimmed = line.trim();
  if (!trimmed.startsWith("|")) {
    return null;
  }
  const inner = trimmed.endsWith("|") && trimmed.length >= 2 ? trimmed.slice(1, -1) : trimmed.slice(1);
  return inner.split(/(?<!\\)\|/).map((cell) => cell.trim());
}

/**
 * 「画面の一覧」の節の表から、パスの列を読む。
 * @param {string} markdown 文書の全文
 * @returns {{ route: string, raw: string, line: number }[]} 照合用のパス・書かれたまま・行番号
 * @throws {Error} 節・列を持つ表が無い、列が足りない行がある、または表が空のとき
 */
export function readDocumentedRoutes(markdown) {
  const lines = markdown.split(/\r?\n/);
  const start = lines.findIndex((line) => line.trim() === SCREEN_LIST_HEADING);
  if (start === -1) {
    throw new Error(`${GUIDE_PATH}: 見出し「${SCREEN_LIST_HEADING}」が見つかりません。見出しを変えたなら照合スクリプトも直してください。`);
  }
  const nextHeading = lines.findIndex((line, index) => index > start && /^#{1,2}\s/.test(line));
  const section = lines.slice(start + 1, nextHeading === -1 ? lines.length : nextHeading);

  const headerOffset = section.findIndex((line) => (splitTableRow(line) ?? []).includes(PATH_COLUMN));
  if (headerOffset === -1) {
    throw new Error(`${GUIDE_PATH}: 「${SCREEN_LIST_HEADING}」の節に「${PATH_COLUMN}」列を持つ表が見つかりません。`);
  }
  const columnIndex = splitTableRow(section[headerOffset]).indexOf(PATH_COLUMN);
  const bodyStart = headerOffset + 2;
  const bodyEndOffset = section.findIndex((line, index) => index >= bodyStart && splitTableRow(line) === null);
  const body = section.slice(bodyStart, bodyEndOffset === -1 ? section.length : bodyEndOffset);
  const routes = body.map((line, offset) => {
    const cells = splitTableRow(line);
    const lineNumber = start + 1 + bodyStart + offset + 1;
    if (cells.length <= columnIndex) {
      throw new Error(`${GUIDE_PATH}:${lineNumber}: 表の行に「${PATH_COLUMN}」列がありません。`);
    }
    const raw = cells[columnIndex].replaceAll("`", "").trim();
    return { route: normalizeRoute(raw), raw, line: lineNumber };
  });
  if (routes.length === 0) {
    throw new Error(`${GUIDE_PATH}: 「${SCREEN_LIST_HEADING}」の表に行がありません。`);
  }
  return routes;
}

/**
 * 除外一覧の `uiRoutes` を読む。値か理由が空の項目は失敗にする。
 * @param {string} json 除外一覧の JSON
 * @returns {Set<string>} 照合用のパスの集合
 * @throws {Error} 値か理由が空の項目があるとき
 */
export function readExcludedRoutes(json) {
  const entries = JSON.parse(json).uiRoutes ?? [];
  const invalid = entries.filter((entry) => !entry?.value?.trim() || !entry?.reason?.trim());
  if (invalid.length > 0) {
    throw new Error(`${EXCLUSIONS_PATH}: uiRoutes に value か reason が空の項目があります。`);
  }
  return new Set(entries.map((entry) => normalizeRoute(entry.value)));
}

/**
 * 実装と文書のパスを比べ、問題を返す。
 * @param {string[]} appRoutes 実装の画面のパス（照合用）
 * @param {{ route: string, raw: string, line: number }[]} documented 文書のパス
 * @param {Set<string>} excluded 除外するパス（照合用）
 * @returns {string[]} 問題のメッセージ。無ければ空。
 */
export function compareRoutes(appRoutes, documented, excluded) {
  const documentedRoutes = new Set(documented.map((entry) => entry.route));
  const appRouteSet = new Set(appRoutes);
  const undocumented = appRoutes
    .filter((route) => !documentedRoutes.has(route) && !excluded.has(route))
    .map((route) => `${GUIDE_PATH}: 画面 ${route} が「${SCREEN_LIST_HEADING}」の表にありません。行を足すか、載せない理由を ${EXCLUSIONS_PATH} の uiRoutes に書いてください。`);
  const unknown = documented
    .filter((entry) => !appRouteSet.has(entry.route))
    .map((entry) => `${GUIDE_PATH}:${entry.line}: ${entry.raw} は Studio に無い画面です。`);
  const duplicated = [...Map.groupBy(documented, (entry) => entry.route)]
    .filter(([, entries]) => entries.length > 1)
    .map(([route, entries]) => `${GUIDE_PATH}:${entries.map((entry) => entry.line).join(",")}: ${route} が重複しています。`);
  const stale = [...excluded]
    .filter((route) => !appRouteSet.has(route) || documentedRoutes.has(route))
    .map((route) => `${EXCLUSIONS_PATH}: uiRoutes の ${route} は、Studio に無いか文書に載っています。除外を消してください。`);
  return [...undocumented, ...unknown, ...duplicated, ...stale];
}

/**
 * コマンドラインの入口。
 * @returns {number} 終了コード
 */
function main() {
  const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
  try {
    const appRoutes = collectAppRoutes(path.join(repositoryRoot, APP_DIRECTORY));
    const documented = readDocumentedRoutes(readFileSync(path.join(repositoryRoot, GUIDE_PATH), "utf8"));
    const excluded = readExcludedRoutes(readFileSync(path.join(repositoryRoot, EXCLUSIONS_PATH), "utf8"));
    const problems = compareRoutes(appRoutes, documented, excluded);
    problems.forEach((problem) => console.error(problem));
    console.log(`画面の一覧を照合しました: 画面 ${appRoutes.length}、文書 ${documented.length} 行、問題 ${problems.length} 件`);
    return problems.length === 0 ? 0 : 1;
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    return 1;
  }
}

if (process.argv[1] !== undefined && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = main();
}
