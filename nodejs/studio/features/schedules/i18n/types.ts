/**
 * schedules feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type SchedulesFeatureUiText = {
  schedules: {
    title: string;
    description: string;
    /** 作成画面へのボタン。 */
    create: string;
    /** 0 件のとき。所属する Project の定義のものだけが見える旨も含む。 */
    empty: string;
    /** 所属の案内（`/admin/projects` へのリンク文言）。 */
    joinProjectLink: string;
    loadFailed: string;
    retry: string;
    /** 定義名を引けなかったとき（UUID をそのまま出す）。 */
    definitionNamesUnavailable: string;
    columns: {
      name: string;
      definition: string;
      cron: string;
      timeZone: string;
      overlap: string;
      status: string;
      nextFireAt: string;
      actions: string;
    };
    overlap: {
      skip: string;
      allow: string;
    };
    enabled: string;
    disabled: string;
    actions: {
      edit: string;
      delete: string;
      confirmDelete: string;
      deleting: string;
      run: string;
      confirmRun: string;
      running: string;
      cancel: string;
      /** 手動実行の確認文。次枠が進まないことと、重なり skip が効かないこと。 */
      runConfirmNote: string;
      /** 手動実行の成功。実行詳細へのリンク文言。 */
      runStarted: (displayId: string) => string;
      deleted: (name: string) => string;
    };
    form: {
      createTitle: string;
      editTitle: string;
      backToList: string;
      notFound: string;
      /** 単票 GET の 403（呼び出し元の実行リソース許可の外）。 */
      forbidden: string;
      loadFailed: string;
      nameLabel: string;
      nameHint: string;
      definitionLabel: string;
      definitionPlaceholder: string;
      definitionsLoadFailed: string;
      /** 候補が 1 ページに収まらず欠けているとき。 */
      definitionsTruncated: (limit: number) => string;
      /** 現在の定義が候補に無いとき（削除済み・欠け）の選択肢ラベル。 */
      currentDefinitionOption: (definitionId: string) => string;
      cronLabel: string;
      cronHint: string;
      timeZoneLabel: string;
      timeZoneHint: string;
      overlapLabel: string;
      runAsLabel: string;
      runAsPlaceholder: string;
      runAsLoadFailed: string;
      runAsEmpty: string;
      /** 現在の run-as が候補に無いとき（無効化された ServiceAccount など）の選択肢ラベル。 */
      currentRunAsOption: (principalId: string) => string;
      versionLabel: string;
      versionHintCreate: string;
      versionHintEdit: string;
      inputLabel: string;
      inputHint: string;
      inputHintEdit: string;
      enabledLabel: string;
      submitCreate: string;
      submitSave: string;
      saving: string;
      saved: string;
      unchanged: string;
      /** 送信前に止めた項目エラー。 */
      errors: {
        required: string;
        nameTooLong: (maxLength: number) => string;
        nameFormat: string;
        cronFormat: string;
        versionFormat: string;
        versionRequired: string;
        inputFormat: string;
        inputCannotClear: string;
      };
    };
    /** API が返した失敗の文言。保存と手動実行で共有する。 */
    serverErrors: {
      runAsUnusable: string;
      membersLink: string;
      runAsMissingPermission: string;
      serviceAccountsLink: string;
      definitionUnavailable: string;
      disableHint: string;
      runAsGrantDenied: string;
      projectAccessDenied: string;
      notFound: string;
    };
  };
};
