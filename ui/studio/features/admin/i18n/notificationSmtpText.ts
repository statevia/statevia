import type { AdminFeatureUiText } from "./types";

type NotificationSmtpText = AdminFeatureUiText["admin"]["notificationSmtp"];

type LocaleCopy = {
  readonly en: string;
  readonly ja: string;
};

/**
 * 通知 SMTP 画面の文言。
 * 英語と日本語は同じキーに並べ、locale ごとのオブジェクト複製を避ける。
 */
const notificationSmtpCopy = {
  title: { en: "Notification SMTP", ja: "通知 SMTP" },
  description: {
    en: "Replace or delete the SMTP settings used by notification.send. The password is never shown again.",
    ja: "notification.send が使う SMTP を置換または削除します。パスワードは再表示しません。"
  },
  notConfigured: { en: "Not configured", ja: "未設定" },
  configured: { en: "Configured", ja: "設定済み" },
  hostLabel: { en: "Host", ja: "ホスト" },
  portLabel: { en: "Port", ja: "ポート" },
  userLabel: { en: "User", ja: "ユーザー" },
  passwordLabel: { en: "Password", ja: "パスワード" },
  defaultFromLabel: { en: "Default From", ja: "既定の From" },
  passwordConfigured: {
    en: "A password is stored. This field starts empty.",
    ja: "パスワードは保存済みです。この欄は空から始まります。"
  },
  replaceHint: {
    en: "Save replaces the whole setting. An empty password does not keep the previous one.",
    ja: "保存は設定全体を置き換えます。空のパスワードは前回の値を残しません。"
  },
  save: { en: "Save", ja: "保存" },
  saving: { en: "Saving…", ja: "保存中…" },
  delete: { en: "Delete", ja: "削除" },
  deleting: { en: "Deleting…", ja: "削除中…" },
  deleteConfirmTitle: { en: "Delete SMTP settings", ja: "SMTP 設定を削除" },
  deleteConfirmBody: {
    en: "Delete the stored SMTP settings for this tenant.",
    ja: "このテナントに保存した SMTP 設定を削除します。"
  },
  deleteConfirm: { en: "Delete", ja: "削除する" },
  cancel: { en: "Cancel", ja: "キャンセル" },
  hostInvalid: { en: "Host must be 1 to 255 characters.", ja: "ホストは 1〜255 文字です。" },
  portInvalid: { en: "Port must be an integer from 1 to 65535.", ja: "ポートは 1〜65535 の整数です。" },
  userInvalid: { en: "User must be at most 256 characters.", ja: "ユーザーは 256 文字までです。" },
  passwordPairInvalid: {
    en: "Set both user and password, or leave both empty.",
    ja: "ユーザーとパスワードは、両方入れるか両方空にしてください。"
  },
  passwordBytesInvalid: {
    en: "Password must be 1 to 1024 UTF-8 bytes.",
    ja: "パスワードは UTF-8 で 1〜1024 バイトです。"
  },
  defaultFromInvalid: { en: "Default From must be at most 256 characters.", ja: "既定の From は 256 文字までです。" },
  loadFailed: { en: "Failed to load SMTP settings.", ja: "SMTP 設定の読み込みに失敗しました。" },
  saveFailed: { en: "Failed to save SMTP settings.", ja: "SMTP 設定の保存に失敗しました。" },
  deleteFailed: { en: "Failed to delete SMTP settings.", ja: "SMTP 設定の削除に失敗しました。" },
  retry: { en: "Retry", ja: "再試行" }
} as const satisfies Record<string, LocaleCopy>;

/**
 * 通知 SMTP 画面の文言を locale で選ぶ。
 * @param locale 表示言語。
 * @returns 画面文言。
 */
export function notificationSmtpText(locale: "en" | "ja"): NotificationSmtpText {
  const text = (entry: LocaleCopy) => entry[locale];
  return {
    title: text(notificationSmtpCopy.title),
    description: text(notificationSmtpCopy.description),
    notConfigured: text(notificationSmtpCopy.notConfigured),
    configured: text(notificationSmtpCopy.configured),
    hostLabel: text(notificationSmtpCopy.hostLabel),
    portLabel: text(notificationSmtpCopy.portLabel),
    userLabel: text(notificationSmtpCopy.userLabel),
    passwordLabel: text(notificationSmtpCopy.passwordLabel),
    defaultFromLabel: text(notificationSmtpCopy.defaultFromLabel),
    passwordConfigured: text(notificationSmtpCopy.passwordConfigured),
    replaceHint: text(notificationSmtpCopy.replaceHint),
    save: text(notificationSmtpCopy.save),
    saving: text(notificationSmtpCopy.saving),
    delete: text(notificationSmtpCopy.delete),
    deleting: text(notificationSmtpCopy.deleting),
    deleteConfirmTitle: text(notificationSmtpCopy.deleteConfirmTitle),
    deleteConfirmBody: text(notificationSmtpCopy.deleteConfirmBody),
    deleteConfirm: text(notificationSmtpCopy.deleteConfirm),
    cancel: text(notificationSmtpCopy.cancel),
    hostInvalid: text(notificationSmtpCopy.hostInvalid),
    portInvalid: text(notificationSmtpCopy.portInvalid),
    userInvalid: text(notificationSmtpCopy.userInvalid),
    passwordPairInvalid: text(notificationSmtpCopy.passwordPairInvalid),
    passwordBytesInvalid: text(notificationSmtpCopy.passwordBytesInvalid),
    defaultFromInvalid: text(notificationSmtpCopy.defaultFromInvalid),
    loadFailed: text(notificationSmtpCopy.loadFailed),
    saveFailed: text(notificationSmtpCopy.saveFailed),
    deleteFailed: text(notificationSmtpCopy.deleteFailed),
    retry: text(notificationSmtpCopy.retry)
  };
}
