/** 管理 API の Action バインディング 1 件。シークレットの値は来ない。 */
export type TenantActionBinding = {
  /** 名前。テナント内で一意。 */
  name: string;
  /** 種別（変数またはシークレット）。 */
  kind: "variable" | "secret";
  /** 使ってよい action の ID（canonical 形式、1〜8 個）。 */
  allowedActionIds: string[];
  /** 更新日時（ISO 8601）。 */
  updatedAt: string;
  /** 変数の値。シークレットでは返らない。 */
  value?: string;
  /** シークレットの値が設定済みか。シークレットでだけ返る。 */
  valueConfigured?: boolean;
};

/** 作成・更新で送る本文。シークレット更新で値を省略できる。 */
export type ActionBindingWriteBody = {
  /** 種別。更新では変えられない。 */
  kind: "variable" | "secret";
  /** 使ってよい action の ID（全体を置き換える）。 */
  allowedActionIds: string[];
  /** 値。変数では必須。シークレットの更新で省略すると今の値を保つ。 */
  value?: string;
};

/**
 * 許可 Action の入力を行へ分ける。空行は捨てる。
 * @param text テキストエリアの内容。
 * @returns canonical actionId。
 */
export function parseAllowedActionIds(text: string): string[] {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
}

/**
 * 更新本文を作る。シークレットの空欄は値を送らない。
 * @param kind 種別。
 * @param value 入力中の値。
 * @param allowedActionIds 許可一覧。
 * @returns PUT 本文。
 */
export function buildActionBindingUpdateBody(
  kind: "variable" | "secret",
  value: string,
  allowedActionIds: string[]
): ActionBindingWriteBody {
  if (kind === "secret" && value.trim().length === 0) {
    return { kind, allowedActionIds };
  }
  return { kind, allowedActionIds, value };
}
