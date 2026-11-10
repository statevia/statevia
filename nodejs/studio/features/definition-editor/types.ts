/**
 * Definition Editor feature が扱う API 応答形。
 * catalog 一覧の正本は `features/definitions/types`。フィールドは Service API 契約に合わせる。
 */

/** GET/PUT/POST /definitions の要素（編集画面用）。 */
export type DefinitionDTO = {
  /** 定義の表示用 ID。 */
  displayId: string;
  /** 定義のリソース UUID。 */
  resourceId: string;
  /** 定義名。 */
  name: string;
  /** 所属 Project。 */
  projectId?: string;
  /** 作成日時（ISO 8601）。 */
  createdAt: string;
  /** 最終更新日時（ISO 8601）。 */
  updatedAt: string;
  /** 定義の YAML。単一の取得・作成・更新・復元の応答に入り、一覧には入らない。 */
  yaml?: string;
  /** 最新版番号（API が返す場合）。 */
  latestVersion?: number;
  /**
   * catalog 論理削除日時（UTC）。
   * 編集画面の通常 GET では含めない想定。
   */
  deletedAt?: string | null;
};

/** 定義スキーマ取得 API（`/definitions/schema/nodes`）のレスポンス。 */
export type DefinitionSchemaResponse = {
  /** スキーマ文書のバージョン。 */
  schemaVersion: string;
  /** nodes 形式の整数バージョン。 */
  nodesVersion: number;
  /** nodes 形式の JSON Schema 相当の文書。 */
  schema: Record<string, unknown>;
};
