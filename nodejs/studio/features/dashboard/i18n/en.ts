import type { DashboardFeatureUiText } from "./types";
import { dashboardUiTextJa } from "./ja";

/**
 * dashboard feature の英語辞書切片（未翻訳は ja を継承）。
 */
export const dashboardUiTextEn: DashboardFeatureUiText = {
  ...dashboardUiTextJa,
  dashboard: {
    ...dashboardUiTextJa.dashboard,
    title: "Dashboard",
    descriptionRecent: "Recent executions (up to 10).",
    loadingRecent: "Loading recent executions.",
    emptyStartFromDefinitionsOrExecutions: "Start from Definitions or Executions.",
    totalCount: (count: number | null) => (count == null ? "Total: --" : `Total: ${count}`),
    recentTitle: "Recent executions",
    openExecutions: "Go to executions",
    lastLoadedAt: (formattedTime: string) => `Updated ${formattedTime}`,
    columns: {
      status: "Status",
      execution: "Execution",
      updatedAt: "Updated at",
    },
    aria: {
      recentExecutionsList: "Recent executions",
    },
    error: {
      fetchFailed: "Failed to fetch data.",
    },
  },
};
