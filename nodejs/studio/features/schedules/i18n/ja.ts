import type { SchedulesFeatureUiText } from "./types";

/**
 * schedules feature の日本語辞書切片。
 */
export const schedulesUiTextJa: SchedulesFeatureUiText = {
  schedules: {
    title: "定期実行",
    description: "定義を決まった時刻に実行します。発火時は run-as の ServiceAccount が実行を開始します。",
    create: "定期実行を作成",
    empty: "表示できる定期実行はありません。定期実行は、所属しているプロジェクトの定義のものだけが表示されます。",
    joinProjectLink: "プロジェクトのメンバーを管理する",
    loadFailed: "定期実行の読み込みに失敗しました。",
    retry: "再試行",
    definitionNamesUnavailable: "定義名を読み込めなかったため、定義 ID を表示しています。",
    columns: {
      name: "名前",
      definition: "定義",
      cron: "cron",
      timeZone: "タイムゾーン",
      overlap: "重なり",
      status: "状態",
      nextFireAt: "次の実行",
      actions: "操作"
    },
    overlap: {
      skip: "前の実行が残っていれば飛ばす",
      allow: "前の実行が残っていても開始する"
    },
    enabled: "有効",
    disabled: "無効",
    actions: {
      edit: "編集",
      delete: "削除",
      confirmDelete: "削除する",
      deleting: "削除中…",
      run: "今すぐ実行",
      confirmRun: "実行する",
      running: "開始中…",
      cancel: "キャンセル",
      runConfirmNote: "1 回だけ実行を開始します。次の実行の予定は進みません。前の実行が残っていても開始します。",
      runStarted: (displayId) => `実行 ${displayId} を開始しました。`,
      deleted: (name) => `${name} を削除しました。`
    },
    form: {
      createTitle: "定期実行を作成",
      editTitle: "定期実行を編集",
      backToList: "定期実行の一覧へ戻る",
      notFound: "この定期実行は見つかりません。削除されたか、所属していないプロジェクトの定義のものです。",
      forbidden: "この定期実行を表示する実行リソース許可がありません。",
      loadFailed: "定期実行の読み込みに失敗しました。",
      nameLabel: "名前",
      nameHint: "英数字のみです。スペース・ハイフン・アンダースコア・ドットは途中だけ使えます。",
      definitionLabel: "定義",
      definitionPlaceholder: "定義を選んでください",
      definitionsLoadFailed: "定義の読み込みに失敗しました。",
      definitionsTruncated: (limit) => `定義が ${limit} 件を超えるため、候補の一部だけを表示しています。`,
      currentDefinitionOption: (definitionId) => `現在の定義（${definitionId}）`,
      cronLabel: "cron",
      cronHint: "5 フィールド（分 時 日 月 曜日）で入力します。例: 0 9 * * 1-5",
      timeZoneLabel: "タイムゾーン",
      timeZoneHint: "IANA のタイムゾーン名で入力します。例: Asia/Tokyo。空のまま UTC にはしません。",
      overlapLabel: "重なり",
      runAsLabel: "run-as（実行する ServiceAccount）",
      runAsPlaceholder: "ServiceAccount を選んでください",
      runAsLoadFailed: "ServiceAccount の読み込みに失敗しました。",
      runAsEmpty: "有効な ServiceAccount がありません。先に ServiceAccount 画面で作成してください。",
      currentRunAsOption: (principalId) => `現在の run-as（${principalId}）`,
      versionLabel: "版番号（任意）",
      versionHintCreate: "空のままにすると、発火時点の最新版で実行します。",
      versionHintEdit: "空のままにすると、今の版の指定を変えません。",
      inputLabel: "input（任意・JSON）",
      inputHint: "実行の開始時に渡す input です。空なら付けません。",
      inputHintEdit: "保存済みの input を空にすることはできません。",
      enabledLabel: "有効にする",
      submitCreate: "作成",
      submitSave: "保存",
      saving: "保存中…",
      saved: "保存しました。",
      unchanged: "変更はありません。",
      errors: {
        required: "入力してください。",
        nameTooLong: (maxLength) => `名前は ${maxLength} 文字までです。`,
        nameFormat: "名前は英数字のみです。スペース・ハイフン・アンダースコア・ドットは途中だけ使えます。",
        cronFormat: "cron は 5 フィールドで入力してください。",
        versionFormat: "版番号は 1 以上の整数で入力してください。",
        versionRequired: "版を指定している定期実行の定義を変えるときは、移し先の版番号を入力してください。",
        inputFormat: "input は JSON で入力してください。",
        inputCannotClear: "保存済みの input は空にできません。"
      }
    },
    serverErrors: {
      runAsUnusable:
        "この run-as では実行できません。有効な ServiceAccount で、定義のプロジェクトに実行以上のロールで所属している必要があります。",
      membersLink: "プロジェクトのメンバーを管理する",
      runAsMissingPermission: "この run-as には実行を開始する権限がありません。権限を持つグループに ServiceAccount を所属させてください。",
      serviceAccountsLink: "ServiceAccount を管理する",
      definitionUnavailable: "この定義は削除されているため、実行できません。",
      disableHint: "無効にすれば保存できます。",
      runAsGrantDenied: "run-as の ServiceAccount の実行リソース許可に、この定義が入っていません。",
      projectAccessDenied: "定義のプロジェクトで、実行以上のロールが必要です。",
      notFound: "定期実行または定義が見つかりません。削除されたか、所属していないプロジェクトのものです。"
    }
  }
};
