/**
 * Execution / Node の状態表示スタイル（横断 UI 用）。
 * ドメイン DTO の状態文字列と一致させる。
 */

/** 実行全体の状態（Service API 準拠）。 */
export type ExecutionStatus = "Running" | "Completed" | "Cancelled" | "Failed";

/** 実行ノードの状態（Engine / Service API 準拠）。 */
export type NodeStatus =
  | "IDLE"
  | "READY"
  | "RUNNING"
  | "WAITING"
  | "SUCCEEDED"
  | "FAILED"
  | "CANCELED";

/**
 * ライトはトークンどおり、ダークでは *-container のベタ塗りが濃く見えるため混色で弱める。
 * `var(...)/NN` は hex トークンでは効かないことがあるため `color-mix` を使う。
 * IDLE / RUNNING のニュートラル帯には使わない。
 *
 * クラス名はソースに静的な文字列としてのみ現わす（Tailwind が `` `...${x}...` `` を arbitrary として誤検出するのを避ける）。
 */
const darkSoftContainerBgClass = {
  info: "bg-md-info-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-info-container)_30%,transparent)]",
  success:
    "bg-md-success-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-success-container)_30%,transparent)]",
  error:
    "bg-md-error-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-error-container)_30%,transparent)]",
  warning:
    "bg-md-warning-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-warning-container)_30%,transparent)]",
} as const;

type DarkSoftContainerSemantic = keyof typeof darkSoftContainerBgClass;

/** @param semantic コンテナ色の系統（info / success / error / warning） */
function softDarkContainerBackground(semantic: DarkSoftContainerSemantic): string {
  return darkSoftContainerBgClass[semantic];
}

/** ステータスバッジ用の状態型。 */
export type StatusLike = ExecutionStatus | NodeStatus;

/** ステータス表示の色・ラベル。 */
export type StatusStyle = {
  badgeClass: string;
  borderClass: string;
  bgClass: string;
  textClass: string;
  icon: string;
  emphasisRank: number;
};

const STATUS_STYLE: Record<StatusLike, StatusStyle> = {
  Running: {
    badgeClass: "bg-md-info text-md-on-info",
    borderClass: "border-md-info",
    bgClass: softDarkContainerBackground("info"),
    textClass: "text-md-on-info-container",
    icon: "•",
    emphasisRank: 20,
  },
  Completed: {
    badgeClass: "bg-md-success text-md-on-success",
    borderClass: "border-md-success",
    bgClass: softDarkContainerBackground("success"),
    textClass: "text-md-on-success-container",
    icon: "✓",
    emphasisRank: 40,
  },
  Failed: {
    badgeClass: "bg-md-error text-md-on-error",
    borderClass: "border-md-error",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-md-on-error-container",
    icon: "⚠",
    emphasisRank: 80,
  },
  Cancelled: {
    badgeClass: "bg-md-error text-md-on-error",
    borderClass: "border-md-error",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-md-on-error-container",
    icon: "✕",
    emphasisRank: 100,
  },
  IDLE: {
    badgeClass: "bg-md-neutral text-md-on-neutral",
    borderClass: "border-md-outline-variant",
    bgClass: "bg-md-neutral-container",
    textClass: "text-md-on-neutral-container",
    icon: "○",
    emphasisRank: 10,
  },
  READY: {
    badgeClass: "bg-md-info text-md-on-info",
    borderClass: "border-md-info",
    bgClass: softDarkContainerBackground("info"),
    textClass: "text-md-on-info-container",
    icon: "•",
    emphasisRank: 20,
  },
  RUNNING: {
    badgeClass: "bg-md-neutral text-md-on-neutral",
    borderClass: "border-md-outline-variant",
    bgClass: "bg-md-neutral-container",
    textClass: "text-md-on-neutral-container",
    icon: "▶",
    emphasisRank: 30,
  },
  WAITING: {
    badgeClass: "bg-md-warning text-md-on-warning",
    borderClass: "border-md-warning",
    bgClass: softDarkContainerBackground("warning"),
    textClass: "text-md-on-warning-container",
    icon: "⏸",
    emphasisRank: 70,
  },
  SUCCEEDED: {
    badgeClass: "bg-md-success text-md-on-success",
    borderClass: "border-md-success",
    bgClass: softDarkContainerBackground("success"),
    textClass: "text-md-on-success-container",
    icon: "✓",
    emphasisRank: 40,
  },
  FAILED: {
    badgeClass: "bg-md-error text-md-on-error",
    borderClass: "border-md-error",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-md-on-error-container",
    icon: "⚠",
    emphasisRank: 80,
  },
  CANCELED: {
    badgeClass: "bg-md-error text-md-on-error",
    borderClass: "border-md-error",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-md-on-error-container",
    icon: "✕",
    emphasisRank: 100,
  },
};

/**
 * 状態から表示スタイルを返す。
 * API が想定外の status（例: Unknown）を返しても一覧が落ちないよう、未定義時は Failed 相当に倒す。
 */
export function getStatusStyle(status: string): StatusStyle {
  if (status in STATUS_STYLE) {
    return STATUS_STYLE[status as StatusLike];
  }

  return STATUS_STYLE.Failed;
}

/** 一覧ソート用のノード状態ウェイト。 */
export function getNodeSortWeight(status: NodeStatus): number {
  const order: Record<NodeStatus, number> = {
    WAITING: 1,
    CANCELED: 2,
    FAILED: 3,
    RUNNING: 4,
    READY: 5,
    SUCCEEDED: 6,
    IDLE: 7,
  };
  return order[status];
}
