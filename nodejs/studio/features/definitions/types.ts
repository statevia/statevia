import type { ExecutionStatus } from "@/shared/lib/statusStyle";

/**
 * Definitions feature のドメイン型（Service API `DefinitionResponse` 相当）。
 */

/** 定義の最後の実行（`GET /v1/definitions` の各要素の `lastExecution`）。 */
export type DefinitionLastExecution = {
  /** 実行の表示用 ID。 */
  displayId: string;
  /** 実行のステータス（契約の値）。 */
  status: ExecutionStatus;
  /** 開始日時（ISO 8601）。 */
  startedAt: string;
  /** 最終更新日時（ISO 8601）。 */
  updatedAt?: string | null;
  /** 待機中か（状態の値ではない）。 */
  waiting: boolean;
};

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
  /**
   * 最後の実行（全版を通して開始日時がいちばん新しいルートの実行）。実行が無ければ null。
   * @remarks 一覧の応答にだけ入り、単体の取得・作成・更新の応答には入らない（そのときは undefined）。
   */
  lastExecution?: DefinitionLastExecution | null;
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
