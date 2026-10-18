import type { AdminFeatureUiText } from "./types";

type ActionBindingText = AdminFeatureUiText["admin"]["actionBindings"];

type LocaleCopy = {
  readonly en: string;
  readonly ja: string;
};

/**
 * Action バインディング画面の文言。
 * 英語と日本語は同じキーに並べ、locale ごとのオブジェクト複製を避ける。
 */
const actionBindingCopy = {
  title: { en: "Action bindings", ja: "Action バインディング" },
  description: {
    en: "Store variables and secrets for custom actions outside the definition.",
    ja: "独自 Action に渡す変数とシークレットを、定義の外に保存します。"
  },
  createTitle: { en: "Create binding", ja: "バインディングを作成" },
  nameLabel: { en: "Name", ja: "名前" },
  kindLabel: { en: "Kind", ja: "種別" },
  kindVariable: { en: "Variable", ja: "変数" },
  kindSecret: { en: "Secret", ja: "シークレット" },
  valueLabel: { en: "Value", ja: "値" },
  secretValueHint: {
    en: "Secret values are not shown again. Leave the field empty to keep the current value.",
    ja: "シークレットの値は保存後に再表示しません。空のまま更新すると、今の値を維持します。"
  },
  allowedActionIdsLabel: { en: "Allowed actions", ja: "許可する Action" },
  allowedActionIdsHint: {
    en: "One canonical action ID per line. 1 to 8.",
    ja: "canonical actionId を 1 行に 1 つ。1〜8 個。"
  },
  createSubmit: { en: "Create", ja: "作成" },
  creating: { en: "Creating…", ja: "作成中…" },
  save: { en: "Save", ja: "保存" },
  saving: { en: "Saving…", ja: "保存中…" },
  delete: { en: "Delete", ja: "削除" },
  deleting: { en: "Deleting…", ja: "削除中…" },
  empty: { en: "No bindings yet.", ja: "バインディングはまだありません。" },
  forbidden: { en: "Tenant administrators only.", ja: "テナント管理者のみ利用できます。" },
  valueConfigured: { en: "Configured", ja: "設定済み" },
  updatedAtPrefix: { en: "Updated: ", ja: "更新: " }
} as const satisfies Record<string, LocaleCopy>;

/**
 * Action バインディング画面の文言を locale で選ぶ。
 * @param locale 表示言語。
 * @returns 画面文言。
 */
export function actionBindingText(locale: "en" | "ja"): ActionBindingText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
    title: text(actionBindingCopy.title),
    description: text(actionBindingCopy.description),
    createTitle: text(actionBindingCopy.createTitle),
    nameLabel: text(actionBindingCopy.nameLabel),
    kindLabel: text(actionBindingCopy.kindLabel),
    kindVariable: text(actionBindingCopy.kindVariable),
    kindSecret: text(actionBindingCopy.kindSecret),
    valueLabel: text(actionBindingCopy.valueLabel),
    secretValueHint: text(actionBindingCopy.secretValueHint),
    allowedActionIdsLabel: text(actionBindingCopy.allowedActionIdsLabel),
    allowedActionIdsHint: text(actionBindingCopy.allowedActionIdsHint),
    createSubmit: text(actionBindingCopy.createSubmit),
    creating: text(actionBindingCopy.creating),
    save: text(actionBindingCopy.save),
    saving: text(actionBindingCopy.saving),
    delete: text(actionBindingCopy.delete),
    deleting: text(actionBindingCopy.deleting),
    empty: text(actionBindingCopy.empty),
    forbidden: text(actionBindingCopy.forbidden),
    valueConfigured: text(actionBindingCopy.valueConfigured),
    updatedAt: (value: string) => `${text(actionBindingCopy.updatedAtPrefix)}${value}`
  };
}
