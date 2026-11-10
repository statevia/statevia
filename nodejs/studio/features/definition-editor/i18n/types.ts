/**
 * definition-editor feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type DefinitionEditorFeatureUiText = {
  /** 定義エディタ画面の文言。 */
  definitionEditor: {
    backToDetail: string;
    descriptionCreating: string;
    descriptionEditingTarget: (definitionId: string) => string;
    loadingMeta: string;
    notMember: string;
    roleDenied: string;
    /** Project ロールは足りるが、グループ権限 `definitions.write` が無いとき。 */
    permissionDenied: string;
    validation: {
      nameRequired: string;
      yamlRequired: string;
      yamlLintInvalid: string;
      nameInvalidFormat: string;
      yamlTooLarge: string;
      projectRequired: string;
    };
    labels: {
      name: string;
      yaml: string;
    };
    actions: {
      saving: string;
      saveWithApiHint: string;
      resetTemplate: string;
      switchToYaml: string;
      switchToGraph: string;
      uploadYaml: string;
      downloadYaml: string;
      confirmOverwrite: string;
      cancelOverwrite: string;
    };
    graph: {
      title: string;
      empty: string;
      addNode: string;
      addNodeDialogTitle: string;
      addNodeDisabledReasonStart: string;
      addNodeDisabledReasonEnd: string;
      nodeInspectorTitle: string;
      edgeInspectorTitle: string;
      deleteNode: string;
      deleteEdge: string;
      apply: string;
      closeDialog: string;
      /** YAML のルートがオブジェクトでないときのメッセージを返す。 */
      rootObjectRequired: () => string;
      /** nodes が配列でないときのメッセージを返す。 */
      nodesArrayRequired: () => string;
      /** nodes が空のときのメッセージを返す。 */
      nodesRequired: () => string;
      /** node.name が無いときのメッセージを返す。 */
      nodeNameRequired: () => string;
      /** node.name が重複しているときのメッセージを返す。 */
      duplicateNodeName: (nodeName: string) => string;
      /** start ノードが 1 件でないときのメッセージを返す。 */
      startCountInvalid: (count: number) => string;
      /** end ノードが 1 件でないときのメッセージを返す。 */
      endCountInvalid: (count: number) => string;
      /** start に next も edges も無いときのメッセージを返す。 */
      startRequiresTransition: (nodeName: string) => string;
      /** action ノードに action が無いときのメッセージを返す。 */
      actionRequired: (nodeName: string) => string;
      /** action に next も edges も無いときのメッセージを返す。 */
      actionRequiresTransition: (nodeName: string) => string;
      /** wait ノードに events も event も無いときのメッセージを返す。 */
      waitEventRequired: (nodeName: string) => string;
      /** 旧形式（event）の wait に next も edges も無いときのメッセージを返す。 */
      waitRequiresTransition: (nodeName: string) => string;
      /** wait に events と event を併用しているときのメッセージを返す。 */
      waitEventsAndEventTogether: (nodeName: string) => string;
      /** wait に events と edges を併用しているときのメッセージを返す。 */
      waitEventsCannotHaveEdges: (nodeName: string) => string;
      /** wait の events のイベントに遷移先が無いときのメッセージを返す。 */
      waitEventTargetRequired: (nodeName: string, eventName: string) => string;
      /** wait に events と subscribe を併用しているときのメッセージを返す。 */
      waitEventsAndSubscribeTogether: (nodeName: string) => string;
      /** wait に subscribe と event を併用しているときのメッセージを返す。 */
      waitSubscribeAndEventTogether: (nodeName: string) => string;
      /** wait に subscribe と edges を併用しているときのメッセージを返す。 */
      waitSubscribeCannotHaveEdges: (nodeName: string) => string;
      /** wait の subscribe が空のときのメッセージを返す。 */
      waitSubscribeRequired: (nodeName: string) => string;
      /** subscribe の要素に topic が無いときのメッセージを返す。 */
      waitSubscribeTopicRequired: (nodeName: string, index: number) => string;
      /** subscribe の要素に next が無いときのメッセージを返す。 */
      waitSubscribeNextRequired: (nodeName: string, index: number) => string;
      /** fork の branches が 2 件未満のときのメッセージを返す。 */
      forkBranchesRequired: (nodeName: string) => string;
      /** join に next も edges も無いときのメッセージを返す。 */
      joinRequiresTransition: (nodeName: string) => string;
      /** join.mode が all 以外のときのメッセージを返す。 */
      joinModeInvalid: (nodeName: string) => string;
      /** end に next か edges があるときのメッセージを返す。 */
      endCannotHaveTransition: (nodeName: string) => string;
      /** edge.to が無いときのメッセージを返す。 */
      edgeToRequired: (nodeName: string) => string;
      /** edge.when.path が無いときのメッセージを返す。 */
      edgeWhenPathRequired: (nodeName: string) => string;
      /** edge.when.op が無いときのメッセージを返す。 */
      edgeWhenOpRequired: (nodeName: string) => string;
      /** EXISTS 以外の演算子で edge.when.value が無いときのメッセージを返す。 */
      edgeWhenValueRequired: (nodeName: string) => string;
      /** IN の edge.when.value が空でない配列でないときのメッセージを返す。 */
      edgeWhenValueInInvalid: (nodeName: string) => string;
      /** BETWEEN の edge.when.value が要素 2 件以上の配列でないときのメッセージを返す。 */
      edgeWhenValueBetweenInvalid: (nodeName: string) => string;
      /** default=true の edge が 2 つ以上あるときのメッセージを返す。 */
      edgeDefaultMultiple: (nodeName: string) => string;
      /** 自分自身へ向かう edge があるときのメッセージを返す。 */
      selfReferenceEdge: (nodeName: string) => string;
      /** 参照先のノードが無いときのメッセージを返す。 */
      missingTargetNode: (nodeName: string, targetName: string) => string;
      /** Fork 領域の外から枝の中へ入る遷移があるときのメッセージを返す。 */
      forkRegionIngressFromOutside: (fromName: string, toName: string) => string;
      /** Join を通らずに Fork 領域の外へ出る遷移があるときのメッセージを返す。 */
      forkRegionEgressWithoutJoin: (fromName: string, toName: string, joinName: string) => string;
      /** wait の遷移先が Fork 領域の外にあるときのメッセージを返す。 */
      forkRegionWaitTargetOutside: (nodeName: string, toName: string) => string;
      selfReferenceRejected: string;
      whenOpPlaceholder: string;
      whenPathPlaceholder: string;
      whenPathHint: string;
      whenValuePlaceholder: string;
      whenValueDisabledForExists: string;
      whenValueHintIn: string;
      whenValueHintBetween: string;
      fullscreenEnter: string;
      fullscreenExit: string;
      parseFailed: string;
      actionErrorLabel: string;
      actionInputLabel: string;
      actionInputPlaceholder: string;
      actionInputHint: string;
      actionInputInvalidJson: string;
      actionIdCandidatesLoading: string;
      actionIdNoResults: string;
      schemaPathPlaceholder: string;
      schemaLiteralOrPathPlaceholder: string;
      waitEventsSectionTitle: string;
      waitEventNameLabel: string;
      waitEventTargetLabel: string;
      waitEventsAdd: string;
      waitEventsRemove: string;
      waitLegacyEventLabel: string;
      waitConvertToEvents: string;
      waitEventsConflictHint: string;
      waitSubscribeSectionTitle: string;
      waitSubscribeTopicLabel: string;
      waitSubscribeKeyLabel: string;
      waitSubscribeNextLabel: string;
      waitSubscribeAdd: string;
      waitSubscribeRemove: string;
      waitSwitchToSubscribe: string;
      waitSwitchToEvents: string;
      waitResolveToSubscribe: string;
      waitSubscribeConflictHint: string;
      waitSubscribeUntitledTopic: string;
    };
    saved: {
      completePrefix: string;
      complete: (displayId: string) => string;
      openNewDetail: string;
      runWithThisDefinition: string;
    };
    toasts: {
      savedWithDisplayId: (displayIdLabel: string, displayId: string) => string;
      yamlLoaded: string;
      yamlExtensionRejected: string;
      yamlReadFailed: string;
    };
    hints: {
      title: string;
    };

  };
};
