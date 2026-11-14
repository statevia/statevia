/**
 * definitions feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type DefinitionsFeatureUiText = {
  /** 定義一覧画面の文言。 */
  definitionsPage: {
    description: string;
    pagination: {
      ariaLabel: string;
      currentPage: (page: number) => string;
    };
    search: {
      label: string;
      placeholder: string;
      submit: string;
      invalidName: string;
    };
    /** 並び順の選択欄のラベル。選択肢は項目と順序を組み合わせて作る。 */
    sortLabel: string;
    sortByCreatedAt: string;
    sortByName: string;
    sortOrderDesc: string;
    sortOrderAsc: string;
    loading: string;
    emptyNoMatch: string;
    listSummary: (totalCount: number, page: number) => string;
    /** 一覧の列の見出し。 */
    columns: {
      name: string;
      createdAt: string;
      actions: string;
    };
    /** 名前の検索のチップ。 */
    nameChip: (keyword: string) => string;
    /** 削除の確認ダイアログ。名前の入力を求める（U6）。 */
    deleteConfirm: {
      title: (name: string) => string;
      message: string;
    };
    /** 復元の確認ダイアログ。 */
    restoreConfirm: {
      title: (name: string) => string;
      message: string;
    };
    actions: {
      openDetail: string;
      createNew: string;
      delete: string;
      restore: string;
      confirmDelete: string;
      confirmRestore: string;
      cancelConfirm: string;
      deleting: string;
      restoring: string;
    };
    includeDeleted: {
      label: string;
    };
    deletedBadge: string;
    deletedAt: (formattedDateTime: string) => string;
    toasts: {
      deleted: string;
      restored: string;
    };
    error: string;
  };
  /** 定義の詳細画面の文言。 */
  definitionDetail: {
    title: string;
    errorFetchFailed: string;
    notMember: string;
    roleDenied: string;
    ariaMeta: string;
    meta: {
      name: string;
      createdAt: string;
    };
    relatedExecutions: {
      title: string;
      description: string;
      openList: string;
    };
    actions: {
      title: string;
      edit: string;
      run: string;
      delete: string;
      confirmDelete: string;
      cancelConfirm: string;
      deleting: string;
    };
    /** 削除の確認ダイアログ。名前の入力を求める（U6）。 */
    deleteConfirm: {
      title: (name: string) => string;
      message: string;
    };
    toasts: {
      deleted: string;
    };
  };
  /** 定義から実行を開始する画面の文言。 */
  definitionRunPage: {
    title: string;
    unspecifiedDefinitionId: string;
    notMember: string;
    roleDenied: string;
    /** Project ロールは足りるが、グループ権限 `executions.write` が無いとき。 */
    permissionDenied: string;
    definitionIdLine: (definitionIdLabel: string, definitionId: string) => string;
    inputLabelWithHint: (inputLabel: string) => string;
    inputJsonPlaceholder: string;
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => string;
      invalidInputJson: (inputLabel: string) => string;
      inputTooLarge: string;
      executionStarted: (executionDisplayId: string) => string;
    };
    actions: {
      starting: string;
      startExecution: string;
    };
    help: {
      redirectAfterStart: string;
    };
  };
};
