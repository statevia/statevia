"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { apiGet, apiPost, apiPut } from "@/shared/api";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { getUtf8ByteLength, matchesPattern } from "@/shared/lib/validation/primitives";
import { DEFINITION_NAME_PATTERN, DEFINITION_YAML_MAX_BYTES } from "@/shared/lib/validation/formRules";
import type { ActionLinkItem } from "@/shared/ui/ActionLinkGroup";
import type { ActionInputValidationDetail } from "../actionSchema/types";
import { defaultDefinitionYaml } from "../lib/defaultDefinitionYaml";
import { parseDefinitionYaml, type ParseDefinitionYamlMessageOptions } from "../lib/parseDefinitionYaml";
import { serializeDefinitionYaml } from "../lib/serializeDefinitionYaml";
import {
  buildDefinitionYamlDownloadFileName,
  downloadDefinitionYamlText,
  readDefinitionYamlFile
} from "../lib/definitionYamlFile";
import { definitionYamlFileToastMessage } from "../i18n/yamlFileToasts";
import type { DefinitionGraphDocument } from "../lib/types";
import { validateGraphDocument, type ValidateGraphDocumentMessageOptions } from "../lib/validateGraphDocument";
import type { DefinitionDTO, DefinitionSchemaResponse } from "../types";

/** 定義エディタの表示モード。 */
export type DefinitionEditorMode = "yaml" | "graph";

/** 定義エディタの振る舞い。 */
export type UseDefinitionEditorPageOptions = {
  /** 編集対象。無いときは新規作成。 */
  definitionId?: string;
};

type ApiErrorLike = {
  status?: number;
  error?: {
    message?: string;
    details?: unknown;
  };
};

type ValidationDetailItem = ActionInputValidationDetail & {
  field?: string;
};

/** 定義エディタの描画用状態。ReactNode は含まない。 */
export type DefinitionEditorPageModel = {
  /** 新規作成か。 */
  isCreateMode: boolean;
  /** 編集対象の ID。新規作成では undefined。 */
  definitionId: string | undefined;
  /** 既存定義の読み込み中か。新規作成では false。 */
  loadingMeta: boolean;
  /** 定義名。 */
  definitionName: string;
  /**
   * 定義名を更新する。
   * @param value 入力値。
   */
  setDefinitionName: (value: string) => void;
  /** 名前欄に出す API 検証メッセージ。 */
  apiNameMessages: string[];
  /** 現在の編集モード。 */
  editorMode: DefinitionEditorMode;
  /** YAML タブを開く。 */
  showYaml: () => void;
  /** 現在の YAML を解釈してからグラフタブを開く。 */
  showGraph: () => void;
  /** 編集中の YAML。 */
  yaml: string;
  /**
   * YAML テキストを更新する。
   * @param nextYaml エディタの全文。
   */
  onYamlChange: (nextYaml: string) => void;
  /** 補完キーワード。 */
  completionKeywords: string[];
  /**
   * YAML lint の有無を受ける。
   * @param hasErrors lint エラーがあるか。
   */
  onYamlLintChange: (hasErrors: boolean) => void;
  /**
   * YAML パーサの診断を受ける。
   * @param diagnostics 診断メッセージ。
   */
  onYamlDiagnosticsChange: (diagnostics: string[]) => void;
  /** YAML をグラフへ解釈できなかったメッセージ。 */
  yamlParseMessages: string[];
  /** グラフ編集の文書。解釈できないときは null。 */
  graphDocument: DefinitionGraphDocument | null;
  /**
   * グラフ文書の変更を YAML へ反映する。
   * @param nextDocument 編集後の文書。
   */
  onGraphDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  /** グラフに出す検証メッセージ。 */
  graphHintMessages: string[];
  /** action input の 422 詳細。 */
  actionValidationDetails: ActionInputValidationDetail[];
  /** 保存できない YAML か。 */
  hasYamlError: boolean;
  /** ヒント領域のメッセージ。API 診断を YAML 診断より先に出す。 */
  hintMessages: string[];
  /** 保存中か。 */
  saving: boolean;
  /** 検証して POST または PUT する。 */
  save: () => Promise<void>;
  /** 読み込み直後の内容と違うか。 */
  canResetToInitial: boolean;
  /** 読み込み直後の名前と YAML に戻す。 */
  resetToInitial: () => void;
  /** ヘッダの導線。 */
  actionLinks: ActionLinkItem[];
  /** 保存結果と失敗のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを閉じる。 */
  dismissToast: () => void;
  /** 保存できた定義。未保存なら null。 */
  savedDefinition: DefinitionDTO | null;
  /** 保存した定義の詳細を開く。 */
  openSavedDetail: () => void;
  /** 保存した定義で実行を開始する。 */
  openSavedRun: () => void;
  /** 未保存の上書き確認を出しているか。 */
  yamlUploadConfirming: boolean;
  /**
   * ローカル YAML を読む。未保存のときは確認まで適用しない。
   * @param file 選択ファイル。
   */
  requestYamlUpload: (file: File) => Promise<void>;
  /** 確認中の YAML をエディタへ適用する。定義名は変えない。 */
  confirmYamlOverwrite: () => void;
  /** 確認を取り消し、エディタを変えない。 */
  cancelYamlOverwrite: () => void;
  /** 現在の YAML をファイルへ書き出す。保存 API は呼ばない。 */
  downloadYaml: () => void;
};

/**
 * nodes スキーマから補完キーワードを集める。
 * @param schema GET /definitions/schema/nodes の schema。
 * @returns ルート、workflow、ノード項目のキー。
 */
function extractCompletionKeywordsFromNodesSchema(schema: unknown): string[] {
  const props = recordProperties(schema);
  if (!props) {
    return [];
  }
  const workflowProps = recordProperties(props.workflow);
  const nodes = isRecord(props.nodes) ? props.nodes : undefined;
  const items = nodes && isRecord(nodes.items) ? nodes.items : undefined;
  const nodeProps = items ? recordProperties(items) : undefined;
  return [...new Set([
    ...Object.keys(props),
    ...Object.keys(workflowProps ?? {}),
    ...Object.keys(nodeProps ?? {})
  ])];
}

/**
 * オブジェクトをレコードとして読む。
 * @param value 任意の値。
 * @returns プレーンオブジェクトならそのレコード。
 */
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

/**
 * JSON Schema の properties を読む。
 * @param value schema ノード。
 * @returns properties がオブジェクトのときだけその中身。
 */
function recordProperties(value: unknown): Record<string, unknown> | undefined {
  if (!isRecord(value)) {
    return undefined;
  }
  return isRecord(value.properties) ? value.properties : undefined;
}

/**
 * API エラー形か。
 * @param value 不明な失敗。
 * @returns error を持つオブジェクトなら true。
 */
function isApiErrorLike(value: unknown): value is ApiErrorLike {
  return isRecord(value) && "error" in value;
}

/**
 * 422 詳細の 1 件をフィールド付きの項目にする。
 * @param value 詳細の要素。
 * @returns オブジェクトなら項目。それ以外は null。
 */
function toValidationDetailItem(value: unknown): ValidationDetailItem | null {
  if (!isRecord(value)) {
    return null;
  }
  return {
    field: typeof value.field === "string" ? value.field : undefined,
    message: typeof value.message === "string" ? value.message : undefined,
    state: typeof value.state === "string" ? value.state : undefined,
    actionId: typeof value.actionId === "string" ? value.actionId : undefined,
    jsonPath: typeof value.jsonPath === "string" ? value.jsonPath : undefined
  };
}

/**
 * 422 の構造化詳細。フィールドごとの表示に使う。
 * @param error API 失敗。
 * @returns メッセージを持つ詳細。422 以外は空。
 */
function extractApiValidationDetails(error: unknown): ValidationDetailItem[] {
  if (!isApiErrorLike(error) || error.status !== 422 || !Array.isArray(error.error?.details)) {
    return [];
  }
  return error.error.details
    .map((item) => (typeof item === "string" ? { message: item } : toValidationDetailItem(item)))
    .filter((detail): detail is ValidationDetailItem => detail !== null && typeof detail.message === "string");
}

/**
 * 422 の表示用メッセージ。構造化詳細が無ければ error.message を使う。
 * @param error API 失敗。
 * @returns ヒントに出す文字列。
 */
function extractApiDiagnosticMessages(error: unknown): string[] {
  if (!isApiErrorLike(error) || error.status !== 422) {
    return [];
  }
  const details = error.error?.details;
  if (Array.isArray(details)) {
    return details.flatMap((item) => {
      if (typeof item === "string") {
        return [item];
      }
      const parsed = toValidationDetailItem(item);
      if (!parsed?.message) {
        return [];
      }
      return [parsed.field ? `[${parsed.field}] ${parsed.message}` : parsed.message];
    });
  }
  if (isRecord(details)) {
    return Object.values(details).filter((item): item is string => typeof item === "string");
  }
  return error.error?.message ? [error.error.message] : [];
}

/**
 * 指定フィールドの検証メッセージだけを取り出す。
 * @param details 422 詳細。
 * @param field 対象フィールド。
 * @returns 空でないメッセージ。
 */
function messagesForField(details: readonly ValidationDetailItem[], field: string): string[] {
  return details.flatMap((detail) => (detail.field === field && detail.message ? [detail.message] : []));
}

/**
 * 定義エディタの読み込み、YAML とグラフの同期、保存を持つ。
 *
 * 文言オプションは ref に置き、ロケール変更では既存定義を再取得しない。
 * 画面は戻り値を既存の YAML エディタとグラフエディタへ渡す。
 *
 * @param options 編集対象の定義 ID。
 * @returns 描画用の状態とコマンド。
 */
export function useDefinitionEditorPage({
  definitionId
}: UseDefinitionEditorPageOptions): DefinitionEditorPageModel {
  const uiText = useUiText();
  const router = useRouter();
  const graphValidationMessageOptions = useMemo<ValidateGraphDocumentMessageOptions>(
    () => ({
      nodesRequired: uiText.definitionEditor.graph.nodesRequired,
      nodeNameRequired: uiText.definitionEditor.graph.nodeNameRequired,
      duplicateNodeName: uiText.definitionEditor.graph.duplicateNodeName,
      startCountInvalid: uiText.definitionEditor.graph.startCountInvalid,
      endCountInvalid: uiText.definitionEditor.graph.endCountInvalid,
      startRequiresTransition: uiText.definitionEditor.graph.startRequiresTransition,
      actionRequired: uiText.definitionEditor.graph.actionRequired,
      actionRequiresTransition: uiText.definitionEditor.graph.actionRequiresTransition,
      waitEventRequired: uiText.definitionEditor.graph.waitEventRequired,
      waitRequiresTransition: uiText.definitionEditor.graph.waitRequiresTransition,
      waitEventsAndEventTogether: uiText.definitionEditor.graph.waitEventsAndEventTogether,
      waitEventsCannotHaveEdges: uiText.definitionEditor.graph.waitEventsCannotHaveEdges,
      waitEventTargetRequired: uiText.definitionEditor.graph.waitEventTargetRequired,
      waitEventsAndSubscribeTogether: uiText.definitionEditor.graph.waitEventsAndSubscribeTogether,
      waitSubscribeAndEventTogether: uiText.definitionEditor.graph.waitSubscribeAndEventTogether,
      waitSubscribeCannotHaveEdges: uiText.definitionEditor.graph.waitSubscribeCannotHaveEdges,
      waitSubscribeRequired: uiText.definitionEditor.graph.waitSubscribeRequired,
      waitSubscribeTopicRequired: uiText.definitionEditor.graph.waitSubscribeTopicRequired,
      waitSubscribeNextRequired: uiText.definitionEditor.graph.waitSubscribeNextRequired,
      forkBranchesRequired: uiText.definitionEditor.graph.forkBranchesRequired,
      joinRequiresTransition: uiText.definitionEditor.graph.joinRequiresTransition,
      joinModeInvalid: uiText.definitionEditor.graph.joinModeInvalid,
      endCannotHaveTransition: uiText.definitionEditor.graph.endCannotHaveTransition,
      edgeToRequired: uiText.definitionEditor.graph.edgeToRequired,
      edgeWhenPathRequired: uiText.definitionEditor.graph.edgeWhenPathRequired,
      edgeWhenOpRequired: uiText.definitionEditor.graph.edgeWhenOpRequired,
      edgeWhenValueRequired: uiText.definitionEditor.graph.edgeWhenValueRequired,
      edgeWhenValueInInvalid: uiText.definitionEditor.graph.edgeWhenValueInInvalid,
      edgeWhenValueBetweenInvalid: uiText.definitionEditor.graph.edgeWhenValueBetweenInvalid,
      edgeDefaultMultiple: uiText.definitionEditor.graph.edgeDefaultMultiple,
      selfReferenceEdge: uiText.definitionEditor.graph.selfReferenceEdge,
      missingTargetNode: uiText.definitionEditor.graph.missingTargetNode,
      forkRegionIngressFromOutside: uiText.definitionEditor.graph.forkRegionIngressFromOutside,
      forkRegionEgressWithoutJoin: uiText.definitionEditor.graph.forkRegionEgressWithoutJoin,
      forkRegionWaitTargetOutside: uiText.definitionEditor.graph.forkRegionWaitTargetOutside
    }),
    [uiText.definitionEditor.graph]
  );
  const parseYamlMessageOptions = useMemo<ParseDefinitionYamlMessageOptions>(
    () => ({
      rootObjectRequired: uiText.definitionEditor.graph.rootObjectRequired,
      nodesArrayRequired: uiText.definitionEditor.graph.nodesArrayRequired
    }),
    [uiText.definitionEditor.graph.nodesArrayRequired, uiText.definitionEditor.graph.rootObjectRequired]
  );
  const isCreateMode = !definitionId;
  const [loadingMeta, setLoadingMeta] = useState(!isCreateMode);
  const [definitionName, setDefinitionName] = useState("");
  const [yaml, setYaml] = useState(defaultDefinitionYaml);
  const [editorMode, setEditorMode] = useState<DefinitionEditorMode>("yaml");
  const [graphDocument, setGraphDocument] = useState<DefinitionGraphDocument | null>(null);
  const [graphValidationMessages, setGraphValidationMessages] = useState<string[]>([]);
  const [yamlParseMessages, setYamlParseMessages] = useState<string[]>([]);
  const [initialSnapshot, setInitialSnapshot] = useState<{ name: string; yaml: string } | null>(null);
  const [yamlHasLintErrors, setYamlHasLintErrors] = useState(false);
  const [yamlDiagnostics, setYamlDiagnostics] = useState<string[]>([]);
  const [apiValidationDetails, setApiValidationDetails] = useState<ValidationDetailItem[]>([]);
  const [apiDiagnostics, setApiDiagnostics] = useState<string[]>([]);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedDefinition, setSavedDefinition] = useState<DefinitionDTO | null>(null);
  const [completionKeywords, setCompletionKeywords] = useState<string[]>([]);
  const yamlRef = useRef(yaml);
  const graphDocumentRef = useRef(graphDocument);
  const graphValidationMessageOptionsRef = useRef(graphValidationMessageOptions);
  const parseYamlMessageOptionsRef = useRef(parseYamlMessageOptions);
  graphValidationMessageOptionsRef.current = graphValidationMessageOptions;
  parseYamlMessageOptionsRef.current = parseYamlMessageOptions;
  const hasYamlError = useMemo(
    () => !yaml.trim() || yamlHasLintErrors || yamlParseMessages.length > 0 || graphValidationMessages.length > 0,
    [graphValidationMessages.length, yaml, yamlHasLintErrors, yamlParseMessages.length]
  );
  const apiNameMessages = useMemo(
    () => messagesForField(apiValidationDetails, "name"),
    [apiValidationDetails]
  );
  const apiYamlMessages = useMemo(
    () => messagesForField(apiValidationDetails, "yaml"),
    [apiValidationDetails]
  );
  const hintMessages = useMemo(() => {
    if (apiYamlMessages.length > 0) {
      return apiYamlMessages;
    }
    if (apiDiagnostics.length > 0) {
      return apiDiagnostics;
    }
    if (yamlParseMessages.length > 0) {
      return yamlParseMessages;
    }
    return yamlDiagnostics;
  }, [apiDiagnostics, apiYamlMessages, yamlDiagnostics, yamlParseMessages]);
  const graphHintMessages = useMemo(() => {
    if (graphValidationMessages.length > 0) {
      return graphValidationMessages;
    }
    return yamlParseMessages;
  }, [graphValidationMessages, yamlParseMessages]);
  const actionValidationDetails = useMemo(
    () => apiValidationDetails.filter((detail) => detail.state || detail.jsonPath || detail.actionId),
    [apiValidationDetails]
  );
  const canResetToInitial = useMemo(() => {
    if (!initialSnapshot) {
      return false;
    }
    return initialSnapshot.name !== definitionName || initialSnapshot.yaml !== yaml;
  }, [definitionName, initialSnapshot, yaml]);
  const actionLinks = useMemo((): ActionLinkItem[] => {
    if (!definitionId) {
      return [{ label: uiText.lists.definitions, href: "/definitions", priority: "primary" }];
    }
    return [
      {
        label: uiText.definitionEditor.backToDetail,
        href: `/definitions/${encodeURIComponent(definitionId)}`,
        priority: "primary"
      },
      { label: uiText.lists.definitions, href: "/definitions" }
    ];
  }, [definitionId, uiText.definitionEditor.backToDetail, uiText.lists.definitions]);

  const loadDefinition = useCallback(async () => {
    if (!definitionId) {
      setLoadingMeta(false);
      return;
    }
    setLoadingMeta(true);
    try {
      const row = await apiGet<DefinitionDTO>(`/definitions/${encodeURIComponent(definitionId)}`);
      setDefinitionName((current) => (current.trim() ? current : row.name));
      const sourceYaml = typeof row.yaml === "string" && row.yaml.trim().length > 0 ? row.yaml : defaultDefinitionYaml;
      const parsed = parseDefinitionYaml(sourceYaml, parseYamlMessageOptionsRef.current);
      if (parsed.document) {
        const normalizedYaml = serializeDefinitionYaml(parsed.document);
        setYaml(normalizedYaml);
        yamlRef.current = normalizedYaml;
        const validated = validateGraphDocument(parsed.document, graphValidationMessageOptionsRef.current);
        setYamlParseMessages(validated.isValid ? [] : validated.messages);
        setGraphValidationMessages(validated.messages);
        if (validated.isValid) {
          setGraphDocument(parsed.document);
        }
      } else {
        setYaml(sourceYaml);
        yamlRef.current = sourceYaml;
        setYamlParseMessages(parsed.diagnostics);
      }
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setLoadingMeta(false);
    }
  }, [definitionId]);

  const loadSchemaKeywords = useCallback(async () => {
    const fallback = [
      "version",
      "workflow",
      "nodes",
      "name",
      "id",
      "description",
      "type",
      "action",
      "error",
      "input",
      "next",
      "event",
      "events",
      "branches",
      "mode",
      "edges",
      "to",
      "when",
      "path",
      "op",
      "value",
      "order",
      "default"
    ];
    try {
      const response = await apiGet<DefinitionSchemaResponse>("/definitions/schema/nodes");
      const fromSchema = extractCompletionKeywordsFromNodesSchema(response.schema);
      setCompletionKeywords(fromSchema.length > 0 ? fromSchema : fallback);
    } catch {
      setCompletionKeywords(fallback);
    }
  }, []);

  useEffect(() => {
    void loadDefinition();
  }, [definitionId, loadDefinition]);

  useEffect(() => {
    void loadSchemaKeywords();
  }, [loadSchemaKeywords]);

  useEffect(() => {
    yamlRef.current = yaml;
  }, [yaml]);

  useEffect(() => {
    graphDocumentRef.current = graphDocument;
  }, [graphDocument]);

  const parseYamlImmediately = useCallback((yamlText: string) => {
    const parsed = parseDefinitionYaml(yamlText, parseYamlMessageOptionsRef.current);
    if (!parsed.document) {
      setYamlParseMessages(parsed.diagnostics);
      return false;
    }
    const validated = validateGraphDocument(parsed.document, graphValidationMessageOptionsRef.current);
    setYamlParseMessages(validated.isValid ? [] : validated.messages);
    setGraphValidationMessages(validated.messages);
    if (validated.isValid) {
      setGraphDocument(parsed.document);
    }
    return validated.isValid;
  }, []);

  const onGraphDocumentChange = useCallback((nextDocument: DefinitionGraphDocument) => {
    graphDocumentRef.current = nextDocument;
    const validated = validateGraphDocument(nextDocument, graphValidationMessageOptionsRef.current);
    setGraphValidationMessages(validated.messages);
    setYamlParseMessages(validated.isValid ? [] : validated.messages);
    setGraphDocument(nextDocument);
    if (!validated.isValid) {
      return;
    }
    const serialized = serializeDefinitionYaml(nextDocument);
    yamlRef.current = serialized;
    setYaml(serialized);
  }, []);

  // グラフタブでは文書が親の YAML より先行することがある。デバウンスした再パースで編集中の状態を消さない。
  useEffect(() => {
    if (editorMode === "graph") {
      return;
    }
    const timer = setTimeout(() => {
      parseYamlImmediately(yaml);
    }, 300);
    return () => clearTimeout(timer);
  }, [editorMode, parseYamlImmediately, yaml]);

  useEffect(() => {
    if (initialSnapshot || loadingMeta) {
      return;
    }
    setInitialSnapshot({
      name: definitionName,
      yaml
    });
  }, [definitionName, initialSnapshot, loadingMeta, yaml]);

  const onYamlChange = useCallback((nextYaml: string) => {
    yamlRef.current = nextYaml;
    setYaml(nextYaml);
  }, []);

  const save = useCallback(async () => {
    const name = definitionName.trim();
    const latestYaml = yamlRef.current;
    const latestGraphDocument = graphDocumentRef.current;
    let yamlForSave = latestYaml;
    if (editorMode === "graph" && latestGraphDocument) {
      yamlForSave = serializeDefinitionYaml(latestGraphDocument);
    } else {
      const parsedForSave = parseDefinitionYaml(latestYaml, parseYamlMessageOptions);
      if (parsedForSave.document) {
        yamlForSave = serializeDefinitionYaml(parsedForSave.document);
      }
    }
    const yamlText = yamlForSave.trim();
    if (!name) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.nameRequired
      });
      return;
    }
    if (!yamlText) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.yamlRequired
      });
      return;
    }
    if (!matchesPattern(name, DEFINITION_NAME_PATTERN)) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.nameInvalidFormat
      });
      return;
    }
    const yamlBytes = getUtf8ByteLength(yamlText);
    if (yamlBytes > DEFINITION_YAML_MAX_BYTES) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.yamlTooLarge
      });
      return;
    }
    if (hasYamlError) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.yamlLintInvalid
      });
      return;
    }

    setSaving(true);
    setToast(null);
    setSavedDefinition(null);
    setApiValidationDetails([]);
    setApiDiagnostics([]);
    try {
      yamlRef.current = yamlForSave;
      setYaml(yamlForSave);
      const saved = definitionId
        ? await apiPut<DefinitionDTO>(`/definitions/${encodeURIComponent(definitionId)}`, { name, yaml: yamlForSave })
        : await apiPost<DefinitionDTO>("/definitions", { name, yaml: yamlForSave });
      setSavedDefinition(saved);
      setToast({
        tone: "success",
        message: uiText.definitionEditor.toasts.savedWithDisplayId(uiText.labels.displayId, saved.displayId)
      });
    } catch (error) {
      setApiValidationDetails(extractApiValidationDetails(error));
      setApiDiagnostics(extractApiDiagnosticMessages(error));
      setToast(toToastError(error));
    } finally {
      setSaving(false);
    }
  }, [
    definitionId,
    definitionName,
    editorMode,
    hasYamlError,
    parseYamlMessageOptions,
    uiText.definitionEditor.toasts,
    uiText.definitionEditor.validation.nameInvalidFormat,
    uiText.definitionEditor.validation.nameRequired,
    uiText.definitionEditor.validation.yamlLintInvalid,
    uiText.definitionEditor.validation.yamlRequired,
    uiText.definitionEditor.validation.yamlTooLarge,
    uiText.labels.displayId
  ]);

  const showYaml = useCallback(() => {
    setEditorMode("yaml");
  }, []);

  const showGraph = useCallback(() => {
    parseYamlImmediately(yaml);
    setEditorMode("graph");
  }, [parseYamlImmediately, yaml]);

  const resetToInitial = useCallback(() => {
    if (!initialSnapshot) {
      return;
    }
    setDefinitionName(initialSnapshot.name);
    setYaml(initialSnapshot.yaml);
    parseYamlImmediately(initialSnapshot.yaml);
  }, [initialSnapshot, parseYamlImmediately]);

  const dismissToast = useCallback(() => {
    setToast(null);
  }, []);

  const [pendingUploadText, setPendingUploadText] = useState<string | null>(null);

  const applyUploadedYaml = useCallback((text: string) => {
    yamlRef.current = text;
    setYaml(text);
    parseYamlImmediately(text);
    setPendingUploadText(null);
    setToast({
      tone: "success",
      message: definitionYamlFileToastMessage(uiText, "loaded")
    });
  }, [parseYamlImmediately, uiText]);

  const requestYamlUpload = useCallback(async (file: File) => {
    const result = await readDefinitionYamlFile(file);
    if (!result.ok) {
      setPendingUploadText(null);
      setToast({
        tone: "error",
        message: definitionYamlFileToastMessage(uiText, result.reason)
      });
      return;
    }
    if (canResetToInitial) {
      setPendingUploadText(result.text);
      return;
    }
    applyUploadedYaml(result.text);
  }, [applyUploadedYaml, canResetToInitial, uiText]);

  const confirmYamlOverwrite = useCallback(() => {
    if (pendingUploadText === null) {
      return;
    }
    applyUploadedYaml(pendingUploadText);
  }, [applyUploadedYaml, pendingUploadText]);

  const cancelYamlOverwrite = useCallback(() => {
    setPendingUploadText(null);
  }, []);

  const downloadYaml = useCallback(() => {
    const yamlText = editorMode === "graph" && graphDocument
      ? serializeDefinitionYaml(graphDocument)
      : yamlRef.current;
    downloadDefinitionYamlText(buildDefinitionYamlDownloadFileName(definitionName), yamlText);
  }, [definitionName, editorMode, graphDocument]);

  const openSavedDetail = useCallback(() => {
    if (!savedDefinition) {
      return;
    }
    router.push(`/definitions/${encodeURIComponent(savedDefinition.displayId)}`);
  }, [router, savedDefinition]);

  const openSavedRun = useCallback(() => {
    if (!savedDefinition) {
      return;
    }
    router.push(`/definitions/${encodeURIComponent(savedDefinition.displayId)}/run`);
  }, [router, savedDefinition]);

  return {
    isCreateMode,
    definitionId,
    loadingMeta,
    definitionName,
    setDefinitionName,
    apiNameMessages,
    editorMode,
    showYaml,
    showGraph,
    yaml,
    onYamlChange,
    completionKeywords,
    onYamlLintChange: setYamlHasLintErrors,
    onYamlDiagnosticsChange: setYamlDiagnostics,
    yamlParseMessages,
    graphDocument,
    onGraphDocumentChange,
    graphHintMessages,
    actionValidationDetails,
    hasYamlError,
    hintMessages,
    saving,
    save,
    canResetToInitial,
    resetToInitial,
    actionLinks,
    toast,
    dismissToast,
    savedDefinition,
    openSavedDetail,
    openSavedRun,
    yamlUploadConfirming: pendingUploadText !== null,
    requestYamlUpload,
    confirmYamlOverwrite,
    cancelYamlOverwrite,
    downloadYaml
  };
}
