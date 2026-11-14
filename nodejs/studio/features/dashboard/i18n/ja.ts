import type { DashboardFeatureUiText } from "./types";

/**
 * dashboard feature の日本語辞書切片。
 */
export const dashboardUiTextJa: DashboardFeatureUiText = {
  dashboard: {
    title: "ダッシュボード",
    descriptionRecent: "選択中のプロジェクトの実行の状況です。",
    loadingRecent: "直近の実行を取得しています。",
    emptyStartFromDefinitionsOrExecutions: "定義一覧または実行一覧から操作を開始できます。",
    totalCount: (count: number | null) => (count == null ? "合計件数: --" : `合計件数: ${count}`),
    recentTitle: "直近の実行",
    openExecutions: "実行の一覧へ",
    lastLoadedAt: (formattedTime: string) => `更新 ${formattedTime}`,
    columns: {
      status: "状態",
      execution: "実行",
      updatedAt: "更新日時",
    },
    aria: {
      recentExecutionsList: "直近実行一覧",
    },
    tiles: {
      ariaLabel: "実行の件数",
      active: "実行中",
      waiting: "待機中",
      waitingNote: "イベントを待っています",
      failed24h: "失敗（24 時間）",
      completed24h: "完了（24 時間）",
      successRate: (percent: number) => `成功率 ${percent}%`,
    },
    attention: {
      title: "対応が必要な実行",
      description: "待機中と失敗を新しい順に並べます。",
      openAll: "すべて見る",
      empty: "対応が必要な実行はありません。",
    },
    error: {
      fetchFailed: "データを取得できませんでした。",
    },
  },
};
