import type { DefinitionEditorFeatureUiText } from "./types";

/**
 * definition-editor feature の日本語辞書切片。
 */
export const definitionEditorUiTextJa: DefinitionEditorFeatureUiText = {
  definitionEditor: {
    backToDetail: "定義の詳細へ戻る",
    descriptionCreating: "新しい定義を作成します。",
    descriptionEditingTarget: (definitionId: string) => `編集対象: ${definitionId}`,
    loadingMeta: "定義メタ情報を読み込み中...",
    notMember: "この定義のプロジェクトに所属していないため、エディタを開けません。",
    roleDenied: "定義の作成と編集には公開者以上のロールが必要です。",
    permissionDenied: "定義の作成と編集には、所属グループに「定義の編集」の権限が必要です。テナント管理者に依頼してください。",
    validation: {
      nameRequired: "定義名を入力してください。",
      yamlRequired: "YAML を入力してください。",
      yamlLintInvalid: "YAML の構文エラーを修正してください。",
      nameInvalidFormat: "定義名は半角英字で開始し、半角英数字と . - _ のみ100文字以内で入力してください。",
      yamlTooLarge: "YAMLは256KB（262144バイト）以内で入力してください。",
      projectRequired: "作成先のプロジェクトを選んでください。",
    },
    labels: {
      name: "定義名（name）",
      yaml: "YAML",
      yamlEditor: "YAML エディタ",
    },
    actions: {
      saving: "保存中...",
      saveWithApiHint: "保存",
      resetTemplate: "編集前に戻す",
      switchToYaml: "YAML",
      switchToGraph: "グラフ",
      uploadYaml: "アップロード",
      downloadYaml: "ダウンロード",
      confirmOverwrite: "上書きする",
      cancelOverwrite: "やめる",
    },
    graph: {
      fieldName: "name",
      fieldAction: "action",
      fieldBranches: "branches（カンマ区切り）",
      fieldTo: "to",
      fieldDefault: "default",
      fieldWhenPath: "when.path",
      fieldWhenOp: "when.op",
      fieldWhenValue: "when.value",
      title: "グラフ編集",
      empty: "YAML のパースに失敗したためグラフを表示できません。YAML を修正してください。",
      addNode: "ノード追加",
      addNodeDialogTitle: "ノードを追加",
      addNodeDisabledReasonStart: "START は 1 つまで追加できます。",
      addNodeDisabledReasonEnd: "END は 1 つまで追加できます。",
      nodeInspectorTitle: "ノード編集",
      edgeInspectorTitle: "遷移の編集",
      deleteNode: "ノード削除",
      deleteEdge: "遷移を削除",
      apply: "反映",
      closeDialog: "閉じる",
      rootObjectRequired: () => "YAML ルートはオブジェクトである必要があります。",
      nodesArrayRequired: () => "nodes は配列で指定してください。",
      nodesRequired: () => "nodes は1件以上必要です。",
      nodeNameRequired: () => "node.name は必須です。",
      duplicateNodeName: (nodeName: string) => `重複 node.name: '${nodeName}'`,
      startCountInvalid: (count: number) => `start ノードは1件のみ許可されます（現在 ${count} 件）。`,
      endCountInvalid: (count: number) => `end ノードは1件のみ許可されます（現在 ${count} 件）。`,
      startRequiresTransition: (nodeName: string) => `ノード '${nodeName}': start は next または edges が必要です。`,
      actionRequired: (nodeName: string) => `ノード '${nodeName}': action ノードは action が必須です。`,
      actionRequiresTransition: (nodeName: string) => `ノード '${nodeName}': action は next または edges が必要です。`,
      waitEventRequired: (nodeName: string) =>
        `ノード '${nodeName}': wait ノードは events または event が必須です。`,
      waitRequiresTransition: (nodeName: string) =>
        `ノード '${nodeName}': wait の旧形式（event）は next または edges が必要です。`,
      waitEventsAndEventTogether: (nodeName: string) =>
        `ノード '${nodeName}': wait の events と event は併用できません。`,
      waitEventsCannotHaveEdges: (nodeName: string) =>
        `ノード '${nodeName}': wait の events と edges は併用できません。遷移先は events に書いてください。`,
      waitEventTargetRequired: (nodeName: string, eventName: string) =>
        `ノード '${nodeName}': events['${eventName}'] の遷移先が必須です。`,
      waitEventsAndSubscribeTogether: (nodeName: string) =>
        `ノード '${nodeName}': wait の events と subscribe は併用できません。`,
      waitSubscribeAndEventTogether: (nodeName: string) =>
        `ノード '${nodeName}': wait の subscribe と event は併用できません。`,
      waitSubscribeCannotHaveEdges: (nodeName: string) =>
        `ノード '${nodeName}': wait の subscribe と edges は併用できません。遷移先は subscribe[].next に書いてください。`,
      waitSubscribeRequired: (nodeName: string) =>
        `ノード '${nodeName}': wait.subscribe は 1 件以上必要です。`,
      waitSubscribeTopicRequired: (nodeName: string, index: number) =>
        `ノード '${nodeName}': subscribe[${index}].topic は必須です。`,
      waitSubscribeNextRequired: (nodeName: string, index: number) =>
        `ノード '${nodeName}': subscribe[${index}].next は必須です。`,
      forkBranchesRequired: (nodeName: string) => `ノード '${nodeName}': fork は branches を2件以上指定してください。`,
      joinRequiresTransition: (nodeName: string) => `ノード '${nodeName}': join は next または edges が必要です。`,
      joinModeInvalid: (nodeName: string) => `ノード '${nodeName}': join.mode は 'all' のみ許可されます。`,
      endCannotHaveTransition: (nodeName: string) => `ノード '${nodeName}': end は next/edges を持てません。`,
      edgeToRequired: (nodeName: string) => `ノード '${nodeName}': edge.to は必須です。`,
      edgeWhenPathRequired: (nodeName: string) => `ノード '${nodeName}': edge.when.path は必須です。`,
      edgeWhenOpRequired: (nodeName: string) => `ノード '${nodeName}': edge.when.op は必須です。`,
      edgeWhenValueRequired: (nodeName: string) =>
        `ノード '${nodeName}': この演算子では edge.when.value が必須です（EXISTS 以外）。`,
      edgeWhenValueInInvalid: (nodeName: string) =>
        `ノード '${nodeName}': IN では edge.when.value は空でない配列（または JSON 配列文字列）が必要です。`,
      edgeWhenValueBetweenInvalid: (nodeName: string) =>
        `ノード '${nodeName}': BETWEEN では edge.when.value は要素2件以上の配列（または JSON 配列文字列）が必要です。`,
      edgeDefaultMultiple: (nodeName: string) =>
        `ノード '${nodeName}': default=true の edge は 1 つまでです。`,
      selfReferenceEdge: (nodeName: string) => `ノード '${nodeName}': 自分自身への遷移は作れません。`,
      missingTargetNode: (nodeName: string, targetName: string) => `ノード '${nodeName}': 参照先 '${targetName}' が存在しません。`,
      forkRegionIngressFromOutside: (fromName: string, toName: string) =>
        `ノード '${fromName}': 並列分岐の範囲の外から、枝の中の '${toName}' へは入れません。`,
      forkRegionEgressWithoutJoin: (fromName: string, toName: string, joinName: string) =>
        `ノード '${fromName}': 合流 '${joinName}' を通らずに '${toName}' へは出られません。`,
      forkRegionWaitTargetOutside: (nodeName: string, toName: string) =>
        `ノード '${nodeName}': wait の遷移先 '${toName}' は並列分岐の範囲の外です。`,
      selfReferenceRejected: "自分自身への遷移は作れません。",
      whenOpPlaceholder: "演算子を選択",
      whenPathPlaceholder: "$.states.Fetch.output.amount",
      whenPathHint:
        "パスの起点は実行のデータです（input と同じ）。例: $.states.A.output.y / $.states['a.b'].output.z / $.vars.flag / $.sys.today",
      whenValuePlaceholder: "例: 100 / true / \"100\"",
      whenValueDisabledForExists: "EXISTS では value は不要です。",
      whenValueHintIn: "IN は JSON配列で入力します。例: [\"A\", \"B\"]",
      whenValueHintBetween: "BETWEEN は [最小, 最大] の JSON配列で入力します。例: [1, 10]",
      fullscreenEnter: "全画面表示",
      fullscreenExit: "全画面終了",
      parseFailed: "YAML のパースに失敗したため、直前の有効グラフを表示しています。",
      actionErrorLabel: "error 遷移先（任意）",
      actionInputLabel: "input（任意）",
      actionInputPlaceholder:
        '例: $.input.orderId / $.states.A.output.x / {"id":"$.states[\'a.b\'].output.id"}',
      actionInputHint:
        "パスの起点は実行のデータです（$.input / $.states / $.vars / $.sys）。名前にドットを含むノードは $.states['id'].output と書きます。オブジェクトは JSON で書きます。",
      actionInputInvalidJson: "JSON の形式が正しくありません。",
      actionIdCandidatesLoading: "アクションの一覧を読み込み中…",
      actionIdNoResults: "一致するアクションがありません",
      waitEventsSectionTitle: "events（イベント → 遷移先）",
      waitEventNameLabel: "イベント名",
      waitEventTargetLabel: "遷移先ノード名",
      waitEventsAdd: "イベント行を追加",
      waitEventsRemove: "行を削除",
      waitLegacyEventLabel: "event（旧形式）",
      waitConvertToEvents: "events 形式へ変換",
      waitEventsConflictHint:
        "events と event が両方あるため編集できません。YAML を修正するか events 形式へ変換してください。",
      waitSubscribeSectionTitle: "subscribe（topic → 遷移先）",
      waitSubscribeTopicLabel: "topic",
      waitSubscribeKeyLabel: "key（任意）",
      waitSubscribeNextLabel: "遷移先ノード名",
      waitSubscribeAdd: "購読行を追加",
      waitSubscribeRemove: "行を削除",
      waitSwitchToSubscribe: "トピックを購読する形に切り替える（events は破棄）",
      waitSwitchToEvents: "実行を指定して再開する形に切り替える（subscribe は破棄）",
      waitResolveToSubscribe: "subscribe 形式へ寄せる",
      waitSubscribeConflictHint:
        "events と subscribe を併用しているため編集できません。どちらかへ寄せてください。",
      waitSubscribeUntitledTopic: "(topic)",

      schemaPathPlaceholder: "$.input.x または $.states.A.output.y",
      schemaLiteralOrPathPlaceholder: "リテラルまたは $.input.x",
    },
    saved: {
      completePrefix: "保存完了:",
      complete: (displayId: string) => `保存完了: ${displayId}`,
      openNewDetail: "新しい定義の詳細へ",
      runWithThisDefinition: "この定義で実行開始",
    },
    toasts: {
      savedWithDisplayId: (displayIdLabel: string, displayId: string) =>
        `定義を保存しました（${displayIdLabel}: ${displayId}）`,
      yamlLoaded: "YAML を読み込みました。登録は保存を押すまで行われません。",
      yamlExtensionRejected: "拡張子は .yaml または .yml のみ読み込めます。",
      yamlReadFailed: "ファイルを読み取れませんでした。",
    },
    hints: {
      title: "修正ヒント",
    },
  },
};
