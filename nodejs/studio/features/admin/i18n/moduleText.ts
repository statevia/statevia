import type { AdminFeatureUiText } from "./types";

type ModuleText = AdminFeatureUiText["admin"]["modules"];

type LocaleCopy = {
  readonly en: string;
  readonly ja: string;
};

/**
 * Module 一覧画面の文言。
 * 英語と日本語は同じキーに並べ、locale ごとのオブジェクト複製を避ける。
 * ロード結果の値（`Loaded` など）は訳さず画面で大文字にそろえるため、ここには置かない。
 */
const moduleCopy = {
  title: { en: "Action modules", ja: "アクションモジュール" },
  description: {
    en: "The result of loading each action module for this tenant. This page only shows the result. Installing and reloading modules are done with the CLI.",
    ja: "このテナントのアクションモジュールを読み込んだ結果です。この画面は結果を見るだけで、モジュールの配置と再読み込みは CLI で行います。"
  },
  listLabel: { en: "Action modules", ja: "アクションモジュールの一覧" },
  moduleColumn: { en: "Module", ja: "モジュール" },
  versionColumn: { en: "Version", ja: "バージョン" },
  statusColumn: { en: "Result", ja: "結果" },
  sourceColumn: { en: "Source", ja: "取得元" },
  loadedAtColumn: { en: "Loaded at", ja: "読み込んだ日時" },
  messageLabel: { en: "Message", ja: "メッセージ" },
  sha256Label: { en: "SHA256", ja: "SHA256" },
  reloadNote: {
    en: "Refresh only reads the latest result again. It does not reload the modules.",
    ja: "「一覧を読み直す」は最新の結果を読み直すだけで、モジュールを読み込み直しません。"
  },
  empty: { en: "No action modules have been loaded.", ja: "読み込んだアクションモジュールはありません。" },
  loadFailed: { en: "Failed to load the action modules.", ja: "アクションモジュールの一覧を取得できませんでした。" },
  retry: { en: "Retry", ja: "再試行" },
  refresh: { en: "Refresh", ja: "一覧を読み直す" }
} as const satisfies Record<string, LocaleCopy>;

/**
 * Module 一覧画面の文言を locale で選ぶ。
 * @param locale 表示言語。
 * @returns 画面文言。
 */
export function moduleText(locale: "en" | "ja"): ModuleText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
    title: text(moduleCopy.title),
    description: text(moduleCopy.description),
    listLabel: text(moduleCopy.listLabel),
    moduleColumn: text(moduleCopy.moduleColumn),
    versionColumn: text(moduleCopy.versionColumn),
    statusColumn: text(moduleCopy.statusColumn),
    sourceColumn: text(moduleCopy.sourceColumn),
    loadedAtColumn: text(moduleCopy.loadedAtColumn),
    messageLabel: text(moduleCopy.messageLabel),
    sha256Label: text(moduleCopy.sha256Label),
    reloadNote: text(moduleCopy.reloadNote),
    empty: text(moduleCopy.empty),
    loadFailed: text(moduleCopy.loadFailed),
    retry: text(moduleCopy.retry),
    refresh: text(moduleCopy.refresh)
  };
}
