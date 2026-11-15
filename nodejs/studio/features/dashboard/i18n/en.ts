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
    descriptionRecent: "Executions in the selected project.",
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
    tiles: {
      ariaLabel: "Execution counts",
      active: "Running",
      waiting: "Waiting",
      waitingNote: "Waiting for events",
      failed24h: "Failed (24h)",
      completed24h: "Completed (24h)",
      successRate: (percent: number) => `Success rate ${percent}%`,
    },
    attention: {
      title: "Needs attention",
      description: "Waiting and failed executions, newest first.",
      openAll: "View all",
      empty: "Nothing needs attention.",
    },
    error: {
      fetchFailed: "Failed to fetch data.",
    },
  },
};
