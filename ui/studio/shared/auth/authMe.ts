/** 認証済み Principal（`/api/auth/me`）。username は必須。email は任意連絡先。 */
export type AuthMeResponse = {
  tenantId: string;
  tenantKey: string;
  principalId: string;
  username: string;
  email: string | null;
  isTenantAdmin: boolean;
  /** グループから展開した権限キー（例: `executions.write`）。表示制御専用で、認可は Service API が行う。 */
  permissions: string[];
};
