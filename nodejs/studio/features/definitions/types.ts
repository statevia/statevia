/**
 * Definitions feature のドメイン型（Service API `DefinitionResponse` 相当）。
 */

/** GET /v1/definitions の要素。 */
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
   * `includeDeleted=true` の一覧でのみ truthy になりうる。通常 GET / restore 成功では含めない。
   */
  deletedAt?: string | null;
};

/** ページング付き定義一覧。 */
export type PagedDefinitions = {
  /** ページ内の定義。 */
  items: DefinitionDTO[];
  /** 絞り込み後の総件数。 */
  totalCount: number;
  /** ページの先頭の位置（0 始まり）。 */
  offset: number;
  /** 1 ページの件数。 */
  limit: number;
  /** 次のページがあるか。 */
  hasMore: boolean;
};

/**
 * 定義が catalog 上削除済みかどうかを判定する。
 * @param definition 一覧・詳細の要素
 * @returns `deletedAt` が truthy のとき true
 */
export function isDeletedDefinition(definition: DefinitionDTO): boolean {
  return Boolean(definition.deletedAt);
}
