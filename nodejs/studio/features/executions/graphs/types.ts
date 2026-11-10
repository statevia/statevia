/** 定義グラフ上のノード定義。 */
export type GraphNodeDef = {
  /** 定義グラフ上のノード名（エッジ・レイアウトのキー。ワークフローの状態名＝API / 実行の `nodeName` と一致）。 */
  nodeName: string;
  /** ノードの種別。 */
  nodeType: string;
  /** 表示ラベル。無ければノード名を出す。 */
  label?: string;
  /** 属する分岐の名前。無ければ分岐の外。 */
  branch?: string;
};

/** エッジの意味（視覚仕様: Next=実線, Resume=破線+イベント名, Cancel=太線+Cancel表示） */
export type EdgeType = "Next" | "Resume" | "Cancel";

/** 定義グラフ上の辺定義。 */
export type GraphEdgeDef = {
  /** 始点のノード名。 */
  from: string;
  /** 終点のノード名。 */
  to: string;
  /** 辺の種類（通常・fork・join）。 */
  kind?: "normal" | "fork" | "join";
  /** エッジ種別（省略時は Next＝通常の遷移） */
  edgeType?: EdgeType;
  /** Resume 時: 表示するイベント名 */
  eventName?: string;
  /** Cancel 時: 表示する reason */
  cancelReason?: string;
  /** Cancel 時: 表示する cause（任意） */
  cancelCause?: string;
};

/** 定義グラフのグループ（ノードのまとまり）。 */
export type GraphGroupDef = {
  /** グループ ID。 */
  groupId: string;
  /** グループの表示名。 */
  label: string;
  /** グループに属するノード名（GraphNodeDef.nodeName）の一覧。 */
  nodeNames: string[];
};

/**
 * 定義 YAML の `meta` と将来の API レスポンスを揃える。
 * 保存座標・dagre 向けヒント・グループ余白などを同一オブジェクトで扱う。
 */
export type GraphDefinitionMeta = {
  /** ノード名 → 保存済みキャンバス座標 */
  layout?: Record<string, { x: number; y: number }>;
  /** dagre rankdir: LR=左→右, TB=上→下, RL/BT も指定可 */
  direction?: "LR" | "TB" | "RL" | "BT";
  /** 分岐を並べる順（branch の名前）。 */
  branchOrder?: string[];
  /** ノード名 → 個別の幅と高さ。 */
  nodeSizeOverrides?: Record<string, { w: number; h: number }>;
  /**
   * 種別デフォルトより先に使う「通常ノードの幅・高さ」。
   * 定義エディタのコンパクト行高など（個別 override より優先度が低い）。
   */
  defaultNodeSize?: { w: number; h: number };
  /** グループの余白（左右・上下・見出し）。 */
  groupPadding?: { x: number; y: number; header: number };
};

/** 定義グラフ全体（ノード・辺・メタ）。 */
export type GraphDefinition = {
  /** グラフ ID（定義の表示用 ID）。 */
  graphId: string;
  /** ノードの定義。 */
  nodes: GraphNodeDef[];
  /** 辺の定義。 */
  edges: GraphEdgeDef[];
  /** ノードのグループ。 */
  groups?: GraphGroupDef[];
  /** 配置のメタ情報。 */
  meta?: GraphDefinitionMeta;
};
