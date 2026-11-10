/**
 * Definition Editor の YAML/Graph 共通ドキュメントモデル。
 * 永続化対象は既存スキーマ（version/workflow/nodes）のみを扱う。
 */
export const NODE_TYPES = ["start", "action", "wait", "fork", "join", "end"] as const;
/** 定義グラフノードの種別。 */
export type NodeType = (typeof NODE_TYPES)[number];

/** 定義グラフ辺の条件式。 */
export type EdgeCondition = {
  /** 値を取り出す JSONPath。 */
  path: string;
  /** 比較の演算子（EXISTS・IN・BETWEEN など）。 */
  op: string;
  /** EXISTS 等では省略されうる */
  value?: unknown;
};

/** 定義グラフドキュメント内の辺。 */
export type DefinitionGraphEdge = {
  /** 遷移先のノード名。 */
  to: string;
  /** 遷移の条件。無ければ無条件。 */
  when?: EdgeCondition;
  /** 評価の順番。 */
  order?: number;
  /** 条件がどれも合わないときに使う辺か。1 ノードに 1 つまで。 */
  default?: boolean;
};

/**
 * Wait Subscribe 行（nodes YAML の `subscribe[]`）。
 * `topic` / `next` は検証で必須。`key` は任意で、空なら YAML では省略する。
 */
export type DefinitionGraphWaitSubscribeEntry = {
  /** 購読する topic。 */
  topic: string;
  /** 購読の key。空なら YAML から省く。 */
  key?: string;
  /** イベントを受けたときの遷移先のノード名。 */
  next: string;
};

/** 定義グラフドキュメント内のノード。 */
export type DefinitionGraphNode = {
  /** 定義内で一意なノード名（YAML `name`、実行時の StateName と一致） */
  name: string;
  /** ノードの種別。 */
  type: NodeType;
  /** action ノードが実行する action ID。 */
  action?: string;
  /** action: 失敗時の遷移先（on.Failed.next） */
  error?: string;
  /** 旧形式の wait が待つイベント名。 */
  event?: string;
  /** wait: イベント名 → 遷移先ノード名（新形式。event+next より優先） */
  events?: Record<string, string>;
  /**
   * wait: Subscribe 配列。プロパティがある（空配列含む）ときは Subscribe モード。
   * `events` との併用は conflict。
   */
  subscribe?: DefinitionGraphWaitSubscribeEntry[];
  /** 次のノード名。 */
  next?: string;
  /** fork の分岐先のノード名（2 件以上）。 */
  branches?: string[];
  /** 条件付きの遷移。 */
  edges?: DefinitionGraphEdge[];
  /** action: パス文字列・リテラル、またはキー→パス／リテラルのマップ（ローダー ParseStrictInputMapping と整合） */
  input?: string | Record<string, unknown>;
  /**
   * action: スロット名からテナント側の名前への参照。値は持たない。
   * グラフの編集フォームは無い。YAML 往復で落とさない。
   */
  bindings?: {
    secrets?: Record<string, string>;
    variables?: Record<string, string>;
  };
  /** join: conversion spec では省略可。指定時は `all` のみ */
  mode?: "all";
};

/** エディタ専用メタ（実行エンジンは無視） */
export type DefinitionGraphMeta = {
  /** グラフキャンバス上のノード座標（node name → x/y） */
  layout?: Record<string, { x: number; y: number }>;
};

/** 定義エディタが編集するグラフドキュメント。 */
export type DefinitionGraphDocument = {
  /** nodes 形式のバージョン。 */
  version: number;
  /** ワークフローの情報。 */
  workflow: {
    name: string;
    /** YAML の workflow.id（名前未指定時ローダーが名前解決に利用） */
    id?: string;
    description?: string;
  };
  /** ノード。 */
  nodes: DefinitionGraphNode[];
  /** エディタ専用のメタ。 */
  meta?: DefinitionGraphMeta;
};

/** YAML パース結果。 */
export type ParseDefinitionYamlResult = {
  /** 解釈したドキュメント。解釈できなければ null。 */
  document: DefinitionGraphDocument | null;
  /** 解釈で見つかった問題のメッセージ。 */
  diagnostics: string[];
};

/** グラフドキュメント検証結果。 */
export type ValidateGraphDocumentResult = {
  /** 問題が無いか。 */
  isValid: boolean;
  /** 見つかった問題のメッセージ。 */
  messages: string[];
};
