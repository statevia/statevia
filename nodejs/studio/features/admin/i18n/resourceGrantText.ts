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
  title: { en: "Definition access", ja: "操作できる定義" },
  backToUsers: { en: "Back to users", ja: "ユーザー一覧へ戻る" },
  backToServiceAccount: { en: "Back to the service account", ja: "サービスアカウントの詳細へ戻る" },
  description: {
    en: "Limits which definitions this user or service account can view, start, resume and cancel (including schedules). An empty list adds no limit within its project memberships; it never denies.",
    ja: "このユーザーまたはサービスアカウントが閲覧・開始・再開・キャンセル（定期実行を含む）できる定義を絞ります。空の種別は、所属の範囲で追加の制限がありません。禁止ではありません。"
  },
  loadFailed: { en: "Could not load the definition access.", ja: "操作できる定義を読み込めませんでした。" },
  notFound: { en: "The user or service account was not found.", ja: "対象のユーザーまたはサービスアカウントが見つかりません。" },
  retry: { en: "Retry", ja: "再試行" },
  candidatesLoadFailed: {
    en: "Could not load the project or definition candidates. Saving is disabled so existing entries are not dropped.",
    ja: "プロジェクトまたは定義の候補を読み込めませんでした。登録済みの項目を落とさないよう、保存できません。"
  },
  limitReason: {
    en: "The limits exist because starting an execution checks these entries in memory.",
    ja: "上限は、実行の開始時にこの一覧をメモリで確かめるためです。"
  },
  projectsTitle: { en: "Projects", ja: "プロジェクト" },
  projectsEmpty: {
    en: "No projects. No additional limit by project.",
    ja: "プロジェクトはありません。プロジェクトによる追加の制限はありません。"
  },
  projectsMembershipNote: {
    en: "A project where this user or service account is not an Executor or higher is rejected when saving.",
    ja: "このユーザーまたはサービスアカウントが実行者以上で所属していないプロジェクトは、保存時に拒否されます。"
  },
  projectAddLabel: { en: "Add a project", ja: "プロジェクトを追加" },
  projectAddPlaceholder: { en: "Select a project", ja: "プロジェクトを選択" },
  projectAddSubmit: { en: "Add", ja: "追加" },
  noMoreProjects: { en: "No more projects to add.", ja: "追加できるプロジェクトはありません。" },
  remove: { en: "Remove", ja: "外す" },
  unknownProject: { en: "Project not in the list", ja: "一覧に無いプロジェクト" },
  definitionsTitle: { en: "Definitions", ja: "定義" },
  definitionsEmpty: {
    en: "No definitions. No additional limit by definition.",
    ja: "定義はありません。定義による追加の制限はありません。"
  },
  definitionsScopeNote: {
    en: "Only definitions in projects you belong to are listed.",
    ja: "候補は、あなたが所属するプロジェクトの定義だけです。"
  },
  definitionsTruncated: {
    en: "Some definitions are not listed because there are too many.",
    ja: "定義が多いため、候補に出ていない定義があります。"
  },
  definitionsUnassigned: { en: "Other definitions", ja: "その他の定義" },
  noDefinitionCandidates: { en: "No definitions to choose from.", ja: "選べる定義はありません。" },
  unknownDefinitionsTitle: {
    en: "Added definitions not in the list",
    ja: "候補に無い登録済みの定義"
  },
  outsideProjectsWarning: {
    en: "Only definitions that satisfy both lists are allowed. These definitions are outside the selected projects:",
    ja: "両方を満たす定義だけが許可されます。次の定義は、選んだプロジェクトの外にあります:"
  },
  saveSubmit: { en: "Save", ja: "保存" },
  saving: { en: "Saving…", ja: "保存中…" },
  saved: { en: "Saved the definition access.", ja: "操作できる定義を保存しました。" },
  clearConfirmTitle: { en: "Remove the limit?", ja: "制限を外しますか？" },
  clearConfirm: { en: "Save", ja: "保存する" },
  cancel: { en: "Cancel", ja: "やめる" }
} as const satisfies Record<string, LocaleCopy>;

/**
 * 実行リソース許可画面の文言を locale で選ぶ。
 * @param locale 表示言語。
 * @returns 画面文言。
 */
export function resourceGrantText(locale: "en" | "ja"): ResourceGrantText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
    title: text(resourceGrantCopy.title),
    backToUsers: text(resourceGrantCopy.backToUsers),
    backToServiceAccount: text(resourceGrantCopy.backToServiceAccount),
    description: text(resourceGrantCopy.description),
    loadFailed: text(resourceGrantCopy.loadFailed),
    notFound: text(resourceGrantCopy.notFound),
    retry: text(resourceGrantCopy.retry),
    candidatesLoadFailed: text(resourceGrantCopy.candidatesLoadFailed),
    limitReason: text(resourceGrantCopy.limitReason),
    projectsTitle: text(resourceGrantCopy.projectsTitle),
    projectCount: (count: number, max: number) => `${count} / ${max}`,
    projectsTooMany: (max: number) =>
      locale === "ja" ? `Project は ${max} 件までです。` : `Up to ${max} projects can be granted.`,
    projectsEmpty: text(resourceGrantCopy.projectsEmpty),
    projectsMembershipNote: text(resourceGrantCopy.projectsMembershipNote),
    projectAddLabel: text(resourceGrantCopy.projectAddLabel),
    projectAddPlaceholder: text(resourceGrantCopy.projectAddPlaceholder),
    projectAddSubmit: text(resourceGrantCopy.projectAddSubmit),
    noMoreProjects: text(resourceGrantCopy.noMoreProjects),
    remove: text(resourceGrantCopy.remove),
    removeNamed: (name: string) => (locale === "ja" ? `${name} を外す` : `Remove ${name}`),
    unknownProject: text(resourceGrantCopy.unknownProject),
    definitionsTitle: text(resourceGrantCopy.definitionsTitle),
    definitionCount: (count: number, max: number) => `${count} / ${max}`,
    definitionsTooMany: (max: number) =>
      locale === "ja" ? `定義は ${max} 件までです。` : `Up to ${max} definitions can be granted.`,
    definitionsEmpty: text(resourceGrantCopy.definitionsEmpty),
    definitionsScopeNote: text(resourceGrantCopy.definitionsScopeNote),
    definitionsTruncated: text(resourceGrantCopy.definitionsTruncated),
    definitionsUnassigned: text(resourceGrantCopy.definitionsUnassigned),
    noDefinitionCandidates: text(resourceGrantCopy.noDefinitionCandidates),
    unknownDefinitionsTitle: text(resourceGrantCopy.unknownDefinitionsTitle),
    outsideProjectsWarning: text(resourceGrantCopy.outsideProjectsWarning),
    saveSubmit: text(resourceGrantCopy.saveSubmit),
    saving: text(resourceGrantCopy.saving),
    saved: text(resourceGrantCopy.saved),
    clearConfirmTitle: text(resourceGrantCopy.clearConfirmTitle),
    clearConfirmBody: (fieldLabels: readonly string[]) =>
      locale === "ja"
        ? `${fieldLabels.join("と")}の許可を空にします。空の種別は、所属の範囲で追加の制限が無くなります。`
        : `This empties the ${fieldLabels.join(" and ")} grants. An empty list removes the additional limit within project memberships.`,
    clearConfirm: text(resourceGrantCopy.clearConfirm),
    cancel: text(resourceGrantCopy.cancel)
  };
}
