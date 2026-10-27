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
  retry: { en: "Retry", ja: "再試行" },
  createTitle: { en: "Create service account", ja: "ServiceAccount を作成" },
  createDescription: {
    en: "Creates a principal without an API key. Permissions come from the selected groups.",
    ja: "API キーを持たない主体を作ります。権限は選んだグループから付きます。"
  },
  nameLabel: { en: "Name", ja: "名前" },
  nameHint: {
    en: "ASCII letters or digits. Space, hyphen, underscore, and dot are allowed only between them.",
    ja: "英数字のみです。スペース・ハイフン・アンダースコア・ドットは途中だけ使えます。"
  },
  groupsLabel: { en: "Groups (at least one)", ja: "所属グループ（1 件以上）" },
  noGroups: {
    en: "There are no groups. Create a group on the Groups page first.",
    ja: "グループがありません。先にグループ管理でグループを作ってください。"
  },
  groupsLoadFailed: { en: "Failed to load groups.", ja: "グループの読み込みに失敗しました。" },
  createSubmit: { en: "Create", ja: "作成" },
  creating: { en: "Creating…", ja: "作成中…" },
  nameRequired: { en: "Enter a name.", ja: "名前を入力してください。" },
  nameInvalidFormat: {
    en: "Use ASCII letters or digits. Space, hyphen, underscore, and dot are allowed only between them.",
    ja: "名前は英数字のみです。スペース・ハイフン・アンダースコア・ドットは途中だけ使えます。"
  },
  groupsRequired: { en: "Select at least one group.", ja: "グループを 1 件以上選んでください。" }
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
    retry: text(serviceAccountCopy.retry),
    createTitle: text(serviceAccountCopy.createTitle),
    createDescription: text(serviceAccountCopy.createDescription),
    nameLabel: text(serviceAccountCopy.nameLabel),
    nameHint: text(serviceAccountCopy.nameHint),
    groupsLabel: text(serviceAccountCopy.groupsLabel),
    noGroups: text(serviceAccountCopy.noGroups),
    groupsLoadFailed: text(serviceAccountCopy.groupsLoadFailed),
    createSubmit: text(serviceAccountCopy.createSubmit),
    creating: text(serviceAccountCopy.creating),
    created: (name: string) => (locale === "ja" ? `${name} を作成しました。` : `Created ${name}.`),
    nameRequired: text(serviceAccountCopy.nameRequired),
    nameTooLong: (maxLength: number) =>
      locale === "ja" ? `名前は ${maxLength} 文字までです。` : `Name must be at most ${maxLength} characters.`,
    nameInvalidFormat: text(serviceAccountCopy.nameInvalidFormat),
    groupsRequired: text(serviceAccountCopy.groupsRequired)
  };
}
