import type { AdminFeatureUiText } from "./types";

type ServiceAccountText = AdminFeatureUiText["admin"]["serviceAccounts"];

type LocaleCopy = {
  readonly en: string;
  readonly ja: string;
};

/**
 * ServiceAccount 画面の固定文言。
 * 英語と日本語は同じキーに並べ、locale ごとのオブジェクト複製を避ける。
 */
const serviceAccountCopy = {
  title: { en: "Service accounts", ja: "ServiceAccount" },
  description: {
    en: "Principals without credentials, used as run-as for schedules. Accounts are disabled instead of deleted. API keys are issued on the API keys page.",
    ja: "資格を持たない主体です。定期実行の run-as に使います。削除の代わりに無効化します。API キーの発行は API キー画面で行います。"
  },
  empty: { en: "No service accounts yet.", ja: "ServiceAccount はまだありません。" },
  active: { en: "Active", ja: "有効" },
  inactive: { en: "Inactive", ja: "無効" },
  hasApiKey: { en: "Has API key", ja: "API キーあり" },
  noApiKey: { en: "No API key", ja: "API キーなし" },
  openDetail: { en: "Details", ja: "詳細" },
  loadFailed: { en: "Failed to load service accounts.", ja: "ServiceAccount の読み込みに失敗しました。" },
  retry: { en: "Retry", ja: "再試行" }
} as const satisfies Record<string, LocaleCopy>;

/**
 * ServiceAccount 画面の文言を locale で選ぶ。
 * @param locale 表示言語。
 * @returns 画面文言。
 */
export function serviceAccountText(locale: "en" | "ja"): ServiceAccountText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
    title: text(serviceAccountCopy.title),
    description: text(serviceAccountCopy.description),
    empty: text(serviceAccountCopy.empty),
    active: text(serviceAccountCopy.active),
    inactive: text(serviceAccountCopy.inactive),
    hasApiKey: text(serviceAccountCopy.hasApiKey),
    noApiKey: text(serviceAccountCopy.noApiKey),
    groupCount: (count: number) => (locale === "ja" ? `グループ ${count} 件` : `${count} group(s)`),
    createdLabel: (value: string) => (locale === "ja" ? `作成: ${value}` : `Created: ${value}`),
    openDetail: text(serviceAccountCopy.openDetail),
    loadFailed: text(serviceAccountCopy.loadFailed),
    retry: text(serviceAccountCopy.retry)
  };
}
