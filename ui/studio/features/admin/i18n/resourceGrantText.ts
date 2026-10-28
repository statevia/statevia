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
  description: {
    en: "Limits which definitions this principal can view, start, resume and cancel (including scheduled runs). An empty list adds no limit within its project memberships; it never denies.",
    ja: "このプリンシパルが閲覧・開始・再開・取消（定期実行を含む）できる定義を絞ります。空の種別は、所属の範囲で追加の制限がありません。禁止ではありません。"
  },
  loadFailed: { en: "Could not load the resource grants.", ja: "実行リソース許可を読み込めませんでした。" },
  notFound: { en: "The principal was not found.", ja: "対象のプリンシパルが見つかりません。" },
  retry: { en: "Retry", ja: "再試行" },
  candidatesLoadFailed: {
    en: "Could not load the project or definition candidates. Saving is disabled so existing grants are not dropped.",
    ja: "Project または定義の候補を読み込めませんでした。付与済みの許可を落とさないよう、保存できません。"
  },
  limitReason: {
    en: "The limits exist because Start evaluates the grants in memory.",
    ja: "上限は、Start が許可をメモリで評価する契約によるものです。"
  },
  projectsTitle: { en: "Projects", ja: "Project" },
  projectsEmpty: {
    en: "No project grants. No additional limit by project.",
    ja: "Project の許可はありません。Project による追加の制限はありません。"
  },
  projectsMembershipNote: {
    en: "A project where this principal is not an executor or higher is rejected when saving.",
    ja: "このプリンシパルが executor 以上で所属していない Project は、保存時に拒否されます。"
  },
  projectAddLabel: { en: "Add a project", ja: "Project を追加" },
  projectAddPlaceholder: { en: "Select a project", ja: "Project を選択" },
  projectAddSubmit: { en: "Add", ja: "追加" },
  noMoreProjects: { en: "No more projects to add.", ja: "追加できる Project はありません。" },
  remove: { en: "Remove", ja: "外す" },
  unknownProject: { en: "Project not in the list", ja: "一覧に無い Project" },
  definitionsTitle: { en: "Definitions", ja: "定義" },
  definitionsEmpty: {
    en: "No definition grants. No additional limit by definition.",
    ja: "定義の許可はありません。定義による追加の制限はありません。"
  },
  definitionsScopeNote: {
    en: "Only definitions in projects you belong to are listed.",
    ja: "候補は、あなたが所属する Project の定義だけです。"
  },
  definitionsTruncated: {
    en: "Some definitions are not listed because there are too many.",
    ja: "定義が多いため、候補に出ていない定義があります。"
  },
  definitionsUnassigned: { en: "Other definitions", ja: "その他の定義" },
  noDefinitionCandidates: { en: "No definitions to choose from.", ja: "選べる定義はありません。" },
  unknownDefinitionsTitle: {
    en: "Granted definitions not in the list",
    ja: "候補に無い付与済みの定義"
  },
  outsideProjectsWarning: {
    en: "Only definitions that satisfy both lists are allowed. These definitions are outside the selected projects:",
    ja: "両方を満たす定義だけが許可されます。次の定義は、選んだ Project の外にあります:"
  },
  saveSubmit: { en: "Save", ja: "保存" },
  saving: { en: "Saving…", ja: "保存中…" },
  saved: { en: "Saved the resource grants.", ja: "実行リソース許可を保存しました。" },
  clearConfirmTitle: { en: "Remove the limit?", ja: "制限を外しますか？" },
  clearConfirm: { en: "Save", ja: "保存する" },
  cancel: { en: "Cancel", ja: "キャンセル" }
} as const satisfies Record<string, LocaleCopy>;

/**
 * 実行リソース許可画面の文言を locale で選ぶ。
 * @param locale 表示言語。
 * @returns 画面文言。
 */
export function resourceGrantText(locale: "en" | "ja"): ResourceGrantText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
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
