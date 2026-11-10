/** 通知 SMTP の参照。パスワード本体は含まない。 */
export type TenantNotificationSmtp = {
  /** 設定済みか。未設定なら接続の項目は null。 */
  configured: boolean;
  /** SMTP サーバーのホスト名。 */
  host: string | null;
  /** SMTP サーバーのポート番号。 */
  port: number | null;
  /** 認証のユーザー名。 */
  user: string | null;
  /** 既定の送信元アドレス。 */
  defaultFrom: string | null;
  /** パスワードが設定済みか。パスワード自体は返らない。 */
  passwordConfigured: boolean;
};

/** 全体置換の本文。パスワードは省略しない。 */
export type NotificationSmtpPutBody = {
  /** SMTP サーバーのホスト名（1〜255 文字）。 */
  host: string;
  /** SMTP サーバーのポート番号（1〜65535）。 */
  port: number;
  /** 認証のユーザー名。空ならパスワードも空にする。 */
  user: string;
  /** パスワード。ユーザー名があるときは必須。 */
  password: string;
  /** 既定の送信元アドレス。 */
  defaultFrom: string;
};

/** 画面の入力。パスワードは GET から埋めない。 */
export type NotificationSmtpDraft = {
  /** ホスト名の入力。 */
  host: string;
  /** ポート番号の入力（文字列のまま）。 */
  port: string;
  /** ユーザー名の入力。 */
  user: string;
  /** パスワードの入力。GET の値では埋めない。 */
  password: string;
  /** 既定の送信元アドレスの入力。 */
  defaultFrom: string;
};

/** 送信前に止める契約違反。 */
export type NotificationSmtpFieldError =
  | "host"
  | "port"
  | "user"
  | "passwordPair"
  | "passwordBytes"
  | "defaultFrom";

/** 置換本文、または送らない理由。 */
export type NotificationSmtpPutResult =
  | { ok: true; body: NotificationSmtpPutBody }
  | { ok: false; field: NotificationSmtpFieldError };

const hostMaxLength = 255;
const userMaxLength = 256;
const defaultFromMaxLength = 256;
const passwordMaxBytes = 1024;
const portPattern = /^[1-9]\d{0,4}$/;

/**
 * GET の応答をフォームへ写す。パスワードは常に空にする。
 * @param response パスワードを含まない参照。
 * @returns 画面の初期入力。
 */
export function notificationSmtpFormFromResponse(response: TenantNotificationSmtp): NotificationSmtpDraft {
  return {
    host: response.host ?? "",
    port: response.port === null ? "" : String(response.port),
    user: response.user ?? "",
    password: "",
    defaultFrom: response.defaultFrom ?? ""
  };
}

/**
 * 通知 SMTP の全体置換本文を作る。契約外は送らない。
 * パスワードの前後空白は削らない。空のパスワードもキーを残して送る。
 * @param draft 画面の入力。
 * @returns PUT 本文、または止めた項目。
 */
export function buildNotificationSmtpPutBody(draft: NotificationSmtpDraft): NotificationSmtpPutResult {
  const host = draft.host.trim();
  if (host.length < 1 || host.length > hostMaxLength) {
    return { ok: false, field: "host" };
  }

  const portText = draft.port.trim();
  if (!portPattern.test(portText)) {
    return { ok: false, field: "port" };
  }
  const port = Number(portText);
  if (port < 1 || port > 65535) {
    return { ok: false, field: "port" };
  }

  const user = draft.user.trim();
  if (user.length > userMaxLength) {
    return { ok: false, field: "user" };
  }

  const password = draft.password;
  if (user.length === 0 && password.length > 0) {
    return { ok: false, field: "passwordPair" };
  }
  if (user.length > 0 && password.length === 0) {
    return { ok: false, field: "passwordPair" };
  }
  if (password.length > 0 && new TextEncoder().encode(password).length > passwordMaxBytes) {
    return { ok: false, field: "passwordBytes" };
  }

  const defaultFrom = draft.defaultFrom.trim();
  if (defaultFrom.length > defaultFromMaxLength) {
    return { ok: false, field: "defaultFrom" };
  }

  return {
    ok: true,
    body: {
      host,
      port,
      user,
      password,
      defaultFrom
    }
  };
}
