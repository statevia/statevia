/**
 * Admin feature のドメイン型（テナント管理者向け API 応答）。
 */

/** 権限カタログ 1 件。 */
export type PermissionDefinitionDto = {
  /** 権限キー（例: `executions.read`）。 */
  permissionKey: string;
  /** 表示ラベル。 */
  displayLabel: string;
  /** UI 表示用の i18n 辞書キー。無ければ null または省略。 */
  displayKey?: string | null;
  /** システム予約の権限か。 */
  isSystem: boolean;
  /** 非推奨の権限か。 */
  isDeprecated: boolean;
};

/** テナントユーザー一覧項目。 */
export type AdminUserListItem = {
  /** ユーザー ID。 */
  userId: string;
  /** Principal ID。 */
  principalId: string;
  /** ログインに使うユーザー名。 */
  username: string;
  /** 連絡先のメールアドレス。無ければ null。 */
  email: string | null;
  /** 表示名。 */
  displayName: string;
  /** テナント管理者か。 */
  isTenantAdmin: boolean;
  /** 有効か。 */
  isActive: boolean;
  /** 所属グループの ID。 */
  groupIds: string[];
  /** 作成日時（ISO 8601）。 */
  createdAt: string;
};

/** グループ一覧項目。 */
export type AdminGroupListItem = {
  /** グループ ID。 */
  groupId: string;
  /** グループ名。 */
  name: string;
  /** システム予約のグループか。 */
  isSystem: boolean;
  /** メンバー数。 */
  memberCount: number;
  /** 付与した権限の数。 */
  permissionCount: number;
  /** 更新日時（ISO 8601）。 */
  updatedAt: string;
};

/** グループ詳細。 */
export type AdminGroupDetail = {
  /** グループ ID。 */
  groupId: string;
  /** グループ名。 */
  name: string;
  /** システム予約のグループか。 */
  isSystem: boolean;
  /** メンバーのユーザー ID。 */
  memberUserIds: string[];
  /** 付与した権限キー。 */
  permissionKeys: string[];
};

/** API キー一覧項目（平文なし）。 */
export type AdminApiKeyListItem = {
  /** API キー ID。 */
  apiKeyId: string;
  /** API キーの名前。 */
  name: string;
  /** 平文キーの先頭数文字（表示用）。 */
  keyPrefix: string;
  /** 許可したスコープ。 */
  allowedScopes: string[];
  /** 有効期限（ISO 8601）。無期限なら null。 */
  expiresAt: string | null;
  /** 最終利用日時（ISO 8601）。未使用なら null。 */
  lastUsedAt: string | null;
  /** 作成日時（ISO 8601）。 */
  createdAt: string;
  /** 有効か。 */
  isActive: boolean;
};

/**
 * ServiceAccount 一覧・詳細・作成・更新の応答。
 * 平文キーは型に持たない。`hasApiKey` はキー付きの行かどうかの表示にだけ使う。
 */
export type AdminServiceAccount = {
  /** サービスアカウント ID。 */
  serviceAccountId: string;
  /** Principal ID。 */
  principalId: string;
  /** サービスアカウントの名前。 */
  name: string;
  /** 有効か。 */
  isActive: boolean;
  /** API キーを持つか。 */
  hasApiKey: boolean;
  /** 所属グループの ID。 */
  groupIds: string[];
  /** 作成日時（ISO 8601）。 */
  createdAt: string;
};

/** API キー作成応答（平文は一度だけ）。 */
export type CreatedAdminApiKey = {
  /** API キー ID。 */
  apiKeyId: string;
  /** API キーの名前。 */
  name: string;
  /** 平文キーの先頭数文字。 */
  keyPrefix: string;
  /** 平文の API キー。この応答でだけ返る。 */
  plainKey: string;
  /** 許可したスコープ。 */
  allowedScopes: string[];
  /** 有効期限（ISO 8601）。無期限なら null。 */
  expiresAt: string | null;
  /** 作成日時（ISO 8601）。 */
  createdAt: string;
};
