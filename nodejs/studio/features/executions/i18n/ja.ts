import type { ExecutionsFeatureUiText } from "./types";

/**
 * executions feature の日本語辞書切片。
 */
export const executionsUiTextJa: ExecutionsFeatureUiText = {
  executionDashboard: {
    operationsPermissionDenied: "キャンセル・再開・イベント送信には、所属グループに「実行の操作」の権限が必要です。テナント管理者に依頼してください。",
    header: {
      titleDefault: "実行の詳細",
    },
    actions: {
      sectionTitle: "実行操作",
      eventNameLabel: "イベント名",
      eventNamePlaceholder: "event-name",
    },
    validation: {
      eventNameTooLong: "イベント名は64文字以内で入力してください。",
      eventNameInvalidFormat: "イベント名は半角英字開始で、半角英数字と . - _ のみ利用できます。",
    },
    graph: {
      fullscreenEnter: "全画面表示",
      fullscreenExit: "全画面終了 (Esc)",
      definitionMissingFallback: "この実行の定義が見つからないため、遷移を仮の線で表示しています。",
    },
    errors: {
      executionNotFound: "指定された実行が見つかりませんでした。ID を確認してください。",
    },
    toasts: {
      cancelAccepted: "キャンセルを受け付けました。",
      publishAccepted: "イベントを送信しました。",
      resumeAccepted: "再開を受け付けました。",
    },
    replayDisabledReason: "リプレイ表示中は実行できません",
    operationsAggregatedInRun: (cancelLabel: string, resumeLabel: string, sendEventLabel: string) =>
      `${cancelLabel} / ${resumeLabel} / ${sendEventLabel} は操作画面にまとめています。`,

  },
  executionTimeline: {
    title: "イベントタイムライン",
    backToCurrent: "現在に戻る",
    replayingPastStateMessage: "過去の時点を表示中です。「現在に戻る」で最新の状態に戻せます。",
    empty: "イベントがありません",
    loadMore: "続きを読み込む",
    errorUnknown: "不明なエラーが発生しました。",

  },
  executionComparison: {
    title: "2実行の比較",
    executionIdPlaceholder: "例: a1B2c3D4e5",
    executionABaselineLabel: (executionLabel: string) => `${executionLabel} A（基準）`,
    executionBLabel: (executionLabel: string) => `${executionLabel} B`,
    kind: {
      onlyLeft: "A のみ",
      onlyRight: "B のみ",
      diff: "差分",
    },
    state: {
      notLoaded: "未読み込み",
    },
    summary: {
      title: "差分サマリ",
      failedOrCancelled: "失敗 / キャンセル",
      others: "その他",
      noDiff: "ノード差分なし",
      loadBothToShow: "A と B を読み込むと表示されます",
    },

  },
  nodeDetail: {
    prompts: {
      loadExecution: (executionLabel: string) => `${executionLabel} を読み込んでください。`,
      selectNode: (nodeLabel: string) => `${nodeLabel} を選択してください。`,
    },
    title: (_nodeLabel: string) => "ノード詳細",
    meta: {
      type: (nodeType: string) => `タイプ: ${nodeType}`,
      nodeName: (nodeName: string) => `ノード名: ${nodeName}`,
      nodeId: (id: string) => `ノードID: ${id}`,
      attempt: (attempt: number) => `回数: ${attempt} 回目`,
      waitKey: (waitKey: string) => `待機キー: ${waitKey}`,
      allowedEvents: (events: string) => `受付イベント: ${events}`,
    },
    waiting: {
      title: "待機の詳細",
      reasonWaitByWaitKeyAndResumeWait: "理由: 再開のイベントを待機キーで待っています。",
      resumeEventName: (eventName: string) => `再開イベント名: ${eventName}`,
      selectResumeEvent: "再開するイベント",
      selectInternalResumeEvent: "イベント名",
      subscribeOnlyHint: "この実行だけを、イベント名を指定して再開します。\n同じトピック / キーを待つ実行への一斉配送ではありません。",
      subscribeResumeAction: "この実行だけ再開",
    },
    cancel: {
      detailTitle: (cancelLabel: string) => `${cancelLabel}の詳細`,
      reason: (cancelReason: string) => `理由: ${cancelReason}`,
      convergedByExecutionCancel: "実行のキャンセルで止まりました。",
    },
    failure: {
      title: "失敗情報",
      noMessage: "（メッセージなし）",
    },
    trace: {
      startedAt: (formattedInstant: string) => `開始: ${formattedInstant}`,
      completedAt: (formattedInstant: string) => `終了: ${formattedInstant}`,
      duration: (durationText: string) => `実行時間: ${durationText}`,
      durationUnavailable: "実行時間: —",
      outputHeading: "出力",
      outputEmpty: "（なし）",
      inputHeading: "入力",
      inputEmpty: "（なし）",
      conditionRoutingHeading: "条件の判定結果",
      conditionRoutingEmpty: "（なし）",
    },
    visitNavigation: {
      jumpToLatest: "最新の回へ",
      stepNewer: "1 つ新しい回へ",
      stepOlder: "1 つ古い回へ",
      jumpToFirst: "1 回目へ",
    },

  },
  graphLegend: {
    heading: {
      nodeStatus: "ノードのステータス",
      edgeType: "遷移の種類",
    },
    aria: {
      root: "グラフ凡例",
      nodeStatus: "ノードのステータスの凡例",
      edgeType: "遷移の種類の凡例",
    },
    edgeKind: {
      nextTraversed: "通過した経路",
      nextNotTraversed: "通っていない経路",
    },

  },
  executionHeader: {
    placeholderExecutionId: "例: a1B2c3D4e5",
    executionIdLabel: (executionLabel: string) => `${executionLabel} ID`,
    compareLabel: "比較",
    realtimeSseLabel: "リアルタイム更新",
    cancelRequestedLabel: "キャンセル要求",
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) =>
      `${cancelRequestedLabel}: ${cancelRequested ? "あり" : "なし"}`,

  },
  executionsPage: {
    pagination: {
      ariaLabel: "実行一覧ページネーション",
      currentPage: (page: number) => `${page} ページ目`,
      prev: "前へ",
      next: "次へ",
    },
    filter: {
      contextActivePrefix: "定義で絞り込み中:",
      clearDefinition: "定義条件を外す",
      title: "フィルタ",
      all: "（すべて）",
      definitionInputHint: "定義の ID または UUID",
      definitionLabelWithHint: (definitionLabel: string) => `${definitionLabel}（ID または UUID）`,
      definitionPlaceholder: "例: a1B2c3D4e5",
      nameInputHint: "実行の ID（部分一致）または UUID（完全一致）",
      search: "検索",
      clear: "クリア",
      sortByLabel: "ソート項目",
      sortOrderLabel: "順序",
      sortByUpdatedAt: "更新日時",
      sortByDisplayId: "ID",
      sortOrderDesc: "降順",
      sortOrderAsc: "昇順",
      invalidName: "実行の ID は半角英数字と . - _ のみ、100文字以内で入力してください。",
      invalidDefinitionId: "定義IDは半角英数字と - _ のみ、80文字以内で入力してください。",
      pageInfo: (limit: number) => `1 ページに ${limit} 件まで表示します。`,
    },
    loading: "実行一覧を読み込み中です。",
    listSummary: (totalCount: number, page: number) => `合計 ${totalCount} 件（${page} ページ目）`,
    updatedAt: (formattedDateTime: string) => `更新: ${formattedDateTime}`,
    actions: {
      openDetail: "詳細",
    },
    ingress: {
      open: "イベントを送信",
      title: "イベントを送信",
      topicLabel: "トピック",
      keyLabel: "キー",
      emptyKey: "キーなし",
      fanOutHint: "同じトピック / キーを待つ実行が複数あれば、すべて再開し得ます。\nこの一覧の 1 件だけではありません。",
      submit: "送る",
      cancel: "閉じる",
      candidatesUnavailable: "候補を読めませんでした。トピック / キーは手入力できます。",
      accepted: "受理しました。一致する購読が 0 件でも成功です。",
      invalidTopic: "トピックは英字で始まり、半角英数字と . _ - のみ、256 文字以内です。",
      invalidKey: "キーを書くときは英字で始まり、半角英数字と . _ - のみ、256 文字以内です。",
    },
    empty: "条件に合う実行はありません。",
    error: "取得に失敗しました。時間をおいて再試行してください。",

  },
  executionDetailPage: {
    title: "実行詳細",
    missingExecutionId: "実行 ID が指定されていません。",
    navRun: "操作",
    navGraph: "グラフ",

  },
  executionGraphPage: {
    title: "実行グラフ",
    missingExecutionId: "実行 ID が指定されていません。",
    navDetail: "詳細",
    navRun: "操作",

  },
  executionRunPage: {
    title: "実行の操作",
    missingExecutionId: "実行 ID が指定されていません。",
    navDetail: "詳細",
    navGraph: "グラフ",

  },
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) =>
      `${cancelLabel}を要求済みのため、${resumeLabel}などの操作はできません。`,
    terminalNotice: (executionLabel: string) => `${executionLabel}は終了しています。`,

  },
  nodeList: {
    title: "ノード一覧",
    summary: (stateCount: number, visitCount: number) =>
      stateCount === visitCount ? `${stateCount} 件` : `${stateCount} ノード / ${visitCount} 回`,
    visitCount: (count: number) => `${count} 回`,
    visitLabel: (nodeName: string, attempt: number) => `${nodeName}（${attempt} 回目）`,
    expandVisits: (nodeName: string) => `${nodeName} の各回を展開`,
    collapseVisits: (nodeName: string) => `${nodeName} の各回を畳む`,
    columns: {
      status: "ステータス",
      type: "タイプ",
      nodeName: "ノード名",
      nodeId: "ノードID",
      duration: "実行時間",
      expand: "開閉",
    },
    pagination: {
      ariaLabel: "ノード一覧のページ",
      currentPage: (page: number, pageCount: number) => `${page} / ${pageCount}`,
    },

  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => `タイプ: ${nodeType}`,
      attempt: (attempt: number) => `回数: ${attempt} 回目`,
      waitKey: (waitKey: string) => `待機キー: ${waitKey}`,
    },
    aria: {
      selectNode: (displayLabel: string) => `ノードを選択: ${displayLabel}`,
    },

  },
  nodeCommands: {
    resumeDisabledReason: {
      runOnly: "操作画面でのみ再開できます。",
      permissionDenied: "再開には、所属グループに「実行の操作」の権限が必要です。",
      executionNotLoaded: "実行が読み込まれていません。",
      nodeNotSelected: "ノードを選択してください。",
      executionTerminal: "実行は終了しています。",
      cancelRequested: "キャンセルを要求済みのため、再開などの操作はできません。",
      waitingOnly: "待機中のノードだけ再開できます。",
      noResumeEvent: "受け付けるイベントが無いため、再開できません。",
    },

  },
};
