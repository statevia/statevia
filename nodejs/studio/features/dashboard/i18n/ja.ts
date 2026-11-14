import type { DashboardFeatureUiText } from "./types";

/**
 * dashboard feature の日本語辞書切片。
 */
export const dashboardUiTextJa: DashboardFeatureUiText = {
  dashboard: {
    title: "ダッシュボード",
    descriptionRecent: "直近の実行（最大 10 件）です。",
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
    error: {
      fetchFailed: "データを取得できませんでした。",
    },
  },
};
