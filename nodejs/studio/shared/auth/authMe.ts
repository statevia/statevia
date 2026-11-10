/** 認証済み Principal（`/api/auth/me`）。username は必須。email は任意連絡先。 */
export type AuthMeResponse = {
  /** テナントの内部 ID。 */
  tenantId: string;
  /** テナントキー。 */
  tenantKey: string;
  /** Principal ID。 */
  principalId: string;
  /** ログインに使うユーザー名。 */
  username: string;
  /** 連絡先のメールアドレス。無ければ null。 */
  email: string | null;
  /** テナント管理者か。 */
  isTenantAdmin: boolean;
  /** グループから展開した権限キー（例: `executions.write`）。表示制御専用で、認可は Service API が行う。 */
  permissions: string[];
};
