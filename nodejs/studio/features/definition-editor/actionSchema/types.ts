/** Action Schema API の型定義（Service API `/v1/actions/schema` 準拠）。 */

/** `/v1/actions/schema/index` のレスポンス（登録 action 一覧）。 */
export type ActionSchemaIndexResponse = {
  /** 登録された action の一覧。 */
  items: ActionSchemaIndexItem[];
};

/** action 一覧の 1 要素（識別子・表示名・バージョン）。 */
export type ActionSchemaIndexItem = {
  /** action ID。 */
  actionId: string;
  /** 表示名。 */
  displayName: string;
  /** action のバージョン。 */
  version: string;
};

/** `/v1/actions/schema/{actionId}` のレスポンス（descriptor + schema + UI メタ）。 */
export type ActionSchemaDetailResponse = {
  /** action のメタ情報。 */
  descriptor: ActionSchemaDescriptor;
  /** 入出力の JSON Schema。 */
  schema: ActionSchemaBundle;
  /** フォーム生成の UI メタ。無ければ null または省略。 */
  uiMetadata?: ActionUiMetadata | null;
};

/** action のメタ情報（識別子・バージョン・表示名・説明・カテゴリ）。 */
export type ActionSchemaDescriptor = {
  /** action ID。 */
  actionId: string;
  /** action のバージョン。 */
  version: string;
  /** 表示名。 */
  displayName: string;
  /** 説明。無ければ null または省略。 */
  description?: string | null;
  /** カテゴリ。無ければ null または省略。 */
  category?: string | null;
};

/** action の入出力 JSON Schema バンドル。 */
export type ActionSchemaBundle = {
  /** input の JSON Schema。 */
  inputSchema: JsonSchemaObject;
  /** output の JSON Schema。 */
  outputSchema: JsonSchemaObject;
  /** スキーマのバージョン。 */
  schemaVersion: string;
};

/** Playground フォーム生成に用いる UI メタ（フィールド順・ヒント・enum ラベル）。 */
export type ActionUiMetadata = {
  /** フィールドを並べる順。無ければ null または省略。 */
  fieldOrder?: string[] | null;
  /** フィールド名 → UI ヒント。 */
  fields?: Record<string, ActionFieldUiHints> | null;
  /** enum の値 → ラベルの i18n キー。 */
  enumLabelKeys?: Record<string, string> | null;
};

/** 個々のフィールドの UI ヒント（widget・ラベルキー・機微フラグなど）。 */
export type ActionFieldUiHints = {
  /** 使う入力部品（`secret` など）。 */
  widget?: string | null;
  /** ラベルの i18n キー。 */
  labelKey?: string | null;
  /** 説明の i18n キー。 */
  descriptionKey?: string | null;
  /** プレースホルダーの i18n キー。 */
  placeholderKey?: string | null;
  /** 機微な値か。true なら伏せ字の入力にする。 */
  sensitive?: boolean;
};

/** JSON Schema の object ノード（Playground フォーム生成用の最小表現）。 */
export type JsonSchemaObject = {
  /** JSON Schema の型名。 */
  type?: string;
  /** タイトル。 */
  title?: string;
  /** 説明。 */
  description?: string;
  /** 取りうる値。 */
  enum?: unknown[];
  /** 文字列の書式。 */
  format?: string;
  /** プロパティ名 → 子のスキーマ。 */
  properties?: Record<string, JsonSchemaObject>;
  /** 必須のプロパティ名。 */
  required?: string[];
  /** 追加のプロパティを許すか、追加のプロパティのスキーマ。 */
  additionalProperties?: boolean | JsonSchemaObject;
  /** いずれか 1 つに合うべきスキーマ。 */
  oneOf?: JsonSchemaObject[];
  /** 数値の最小値。 */
  minimum?: number;
  /** 既定値。 */
  default?: unknown;
  /** statevia の拡張。`path` なら値を JSONPath のまま渡し、型変換しない。 */
  "x-statevia-valueKind"?: string;
};

/** 422 応答に含まれる action input 検証エラーの詳細。 */
export type ActionInputValidationDetail = {
  /** エラーの説明。 */
  message?: string;
  /** 検証に失敗した状態名。 */
  state?: string;
  /** action ID。 */
  actionId?: string;
  /** 失敗した値の JSONPath。 */
  jsonPath?: string;
};
