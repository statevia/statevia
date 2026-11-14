/**
 * dashboard feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type DashboardFeatureUiText = {
  /** ダッシュボード画面の文言。 */
  dashboard: {
    title: string;
    descriptionRecent: string;
    loadingRecent: string;
    emptyStartFromDefinitionsOrExecutions: string;
    totalCount: (count: number | null) => string;
    /** 直近の実行の節の見出し。 */
    recentTitle: string;
    /** 実行一覧へのリンク。 */
    openExecutions: string;
    /** 最後に読み込んだ時刻。引数は時刻だけの書式。 */
    lastLoadedAt: (formattedTime: string) => string;
    /** 直近の実行の列の見出し。 */
    columns: {
      status: string;
      execution: string;
      updatedAt: string;
    };
    aria: {
      recentExecutionsList: string;
    };
    error: {
      fetchFailed: string;
    };
  };
};
