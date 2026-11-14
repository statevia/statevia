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
    /** 件数のタイル。 */
    tiles: {
      ariaLabel: string;
      active: string;
      waiting: string;
      waitingNote: string;
      failed24h: string;
      completed24h: string;
      /** 完了のタイルの成功率。引数は小数 1 桁までの百分率。 */
      successRate: (percent: number) => string;
    };
    /** 対応が必要な実行の節。 */
    attention: {
      title: string;
      description: string;
      openAll: string;
      empty: string;
    };
    error: {
      fetchFailed: string;
    };
  };
};
