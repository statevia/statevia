/** 管理 API の Action バインディング 1 件。シークレットの値は来ない。 */
export type TenantActionBinding = {
  name: string;
  kind: "variable" | "secret";
  allowedActionIds: string[];
  updatedAt: string;
  value?: string;
  valueConfigured?: boolean;
};

/** 作成・更新で送る本文。シークレット更新で値を省略できる。 */
export type ActionBindingWriteBody = {
  kind: "variable" | "secret";
  allowedActionIds: string[];
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
