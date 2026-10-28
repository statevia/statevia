import type { AdminFeatureUiText } from "./types";

type ResourceGrantText = AdminFeatureUiText["admin"]["resourceGrants"];

type LocaleCopy = {
  readonly en: string;
  readonly ja: string;
};

/**
 * 実行リソース許可画面の固定文言。
 * 英語と日本語は同じキーに並べ、locale ごとのオブジェクト複製を避ける。
 */
const resourceGrantCopy = {
  saved: { en: "Saved the resource grants.", ja: "実行リソース許可を保存しました。" }
} as const satisfies Record<string, LocaleCopy>;

/**
 * locale に合わせた実行リソース許可画面の文言。
 * @param locale 表示言語。
 * @returns 辞書の切片。
 */
export function resourceGrantText(locale: "en" | "ja"): ResourceGrantText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
    saved: text(resourceGrantCopy.saved)
  };
}
