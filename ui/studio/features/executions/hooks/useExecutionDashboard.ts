"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { apiGet } from "@/shared/api";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { buildExecutionView } from "../lib/executionView";
import { computeExecutionDiff } from "../lib/executionDiff";
import type { ExecutionDTO, ExecutionGraphDTO, ExecutionView } from "../types";
import type { GraphViewport } from "../ui/NodeGraphView";
import type { ViewMode } from "../ui/ViewToggle";
import { useExecution } from "./useExecution";
import { useExecutionEvents } from "./useExecutionEvents";
import { useExecutionStateAtSeq } from "./useExecutionStateAtSeq";
import { getNodeWithFallback, useGraphData } from "./useGraphData";
import { useGraphDefinition } from "./useGraphDefinition";
import { getResumeDisabledReason, useNodeCommands } from "./useNodeCommands";

const STREAM_PREF_STORAGE_KEY = "statevia.execution.streamEnabled";

/** executionId ごとの Graph ビューポート（ズーム・パン位置）。 */
type GraphViewportByExecutionId = Record<string, GraphViewport>;

/** 実行ダッシュボードの振る舞い。ヘッダのナビ部品は含まない。 */
export type UseExecutionDashboardOptions = {
  /** 初期の実行 ID（URL から渡す場合は key と併用）。 */
  initialExecutionId: string;
  /** true のときマウント直後に Load を実行する。 */
  autoLoadOnMount?: boolean;
  /** メイン見出し。未指定時は既定文言。 */
  headerTitle?: string;
  /** false のとき executionId を URL 固定として編集させない。 */
  executionIdEditable?: boolean;
  /** false のとき比較モード UI を出さない。 */
  comparisonEnabled?: boolean;
  /** false のとき Cancel / Resume / Event などの実行操作を無効化する。 */
  operationsEnabled?: boolean;
  /** 初期の表示モード。 */
  initialViewMode?: ViewMode;
  /** true のとき View モード切り替えを固定し、UI から変更不可にする。 */
  lockViewMode?: boolean;
};

/** 実行ダッシュボードの描画用状態。ReactNode は含まない。 */
export type ExecutionDashboardModel = {
  /** グラフを全画面にしているか。 */
  graphFullscreen: boolean;
  /** 見出し。未指定時は既定文言。 */
  headerTitle: string;
  /** 操作結果のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを閉じる。 */
  onCloseToast: () => void;
  /** 入力中または URL の実行 ID。 */
  executionId: string;
  /** 実行 ID を編集できるか。 */
  executionIdEditable: boolean;
  /**
   * 実行 ID の入力を更新する。
   * @param executionId 入力値。
   */
  onExecutionIdChange: (executionId: string) => void;
  /** 実行を読み込む。 */
  onLoadExecution: () => void;
  /** 実行のキャンセルを要求する。 */
  onCancelExecution: () => void;
  /** 実行・ノード操作・再生・定義の取得中か。 */
  loading: boolean;
  /** キャンセルを受け付けるか。 */
  canCancel: boolean;
  /**
   * イベントを送る。
   * @param eventName イベント名。
   */
  onPublishEvent: (eventName: string) => void;
  /** 現在の実行。未取得時は null。 */
  execution: ExecutionView | null;
  /** 一覧かグラフか。 */
  viewMode: ViewMode;
  /**
   * 表示モードを変える。固定中は無視する。
   * @param mode 一覧またはグラフ。
   */
  onViewModeChange: (mode: ViewMode) => void;
  /** 表示モードの切り替えを出すか。 */
  showViewToggle: boolean;
  /** 比較モードか。 */
  compareMode: boolean;
  /**
   * 比較モードを切り替える。
   * @param compareMode 比較するか。
   */
  onCompareModeChange: (compareMode: boolean) => void;
  /** 比較 UI を出すか。 */
  comparisonEnabled: boolean;
  /** 実行操作を出すか。 */
  operationsEnabled: boolean;
  /** 実行のストリームを受けるか。 */
  streamEnabled: boolean;
  /**
   * ストリームのオンオフを保存する。
   * @param enabled ストリームを受けるか。
   */
  onStreamEnabledChange: (enabled: boolean) => void;
  /** 実行が読めてパネルを出すか。 */
  showExecutionPanels: boolean;
  /** 比較対象の実行。未取得時は null。 */
  executionB: ExecutionView | null;
  /** 比較対象の実行 ID。 */
  executionIdB: string;
  /**
   * 比較対象の実行 ID を更新する。
   * @param executionId 入力値。
   */
  onExecutionIdBChange: (executionId: string) => void;
  /** 比較対象を読み込む。 */
  onLoadExecutionB: () => void;
  /** 比較対象の取得中か。 */
  loadingB: boolean;
  /** 二つの実行の差分。 */
  executionDiff: ReturnType<typeof computeExecutionDiff>;
  /**
   * ノードを選択する。
   * @param nodeId ノード ID。解除は null。
   */
  onSelectNode: (nodeId: string | null) => void;
  /** 実行が終端か。 */
  terminal: boolean;
  /** 過去時点を表示しているか。 */
  isReplaying: boolean;
  /** 再生をやめて現在へ戻す。 */
  onBackToCurrent: () => void;
  /** タイムラインのイベント。 */
  timelineEvents: ReturnType<typeof useExecutionEvents>["events"];
  /** タイムラインの初回取得中か。 */
  timelineLoading: boolean;
  /** タイムラインの取得失敗。無ければ null。 */
  timelineError: ReturnType<typeof useExecutionEvents>["error"];
  /** 再生中のシーケンス。現在表示は null。 */
  replayAtSeq: number | null;
  /**
   * 再生位置を選ぶ。
   * @param seq シーケンス。現在へ戻すときは null。
   */
  onSelectSeq: (seq: number | null) => void;
  /** タイムラインの続きがあるか。 */
  timelineHasMore: boolean;
  /** タイムラインの追加取得中か。 */
  timelineLoadingMore: boolean;
  /** タイムラインの続きを取得する。 */
  onTimelineLoadMore: () => void;
  /** 再生中ならその時点の実行。再生していなければ現在の実行。 */
  displayExecution: ExecutionView | null;
  /** 選択中のノード ID。 */
  selectedNodeId: string | null;
  /** グラフ描画データ。 */
  graphData: ReturnType<typeof useGraphData>;
  /** グラフの全画面を切り替える。 */
  onToggleGraphFullscreen: () => void;
  /**
   * 指定ノードを Resume する。
   * @param nodeId ノード ID。
   * @param eventName Resume に使うイベント名。
   */
  onResumeNode: (nodeId: string, eventName: string) => void;
  /**
   * 選択中ノードを Resume する。再生中または未選択なら何もしない。
   * @param eventName Resume に使うイベント名。
   */
  onResumeSelectedNode: (eventName: string) => void;
  /**
   * ノードを Resume できない理由。できるときは null。
   * @param nodeId ノード ID。
   */
  getResumeDisabledReasonForNode: (nodeId: string) => string | null;
  /** この実行で保存しているグラフのビューポート。 */
  savedGraphViewport?: GraphViewport;
  /**
   * グラフのズームとパンを実行 ID ごとに覚える。
   * @param viewport 現在のビューポート。
   */
  onGraphViewportChange: (viewport: GraphViewport) => void;
  /** 選択中ノード。無ければ null。 */
  selectedNode: ReturnType<typeof getNodeWithFallback>;
  /** 選択中ノードを Resume できない理由。できるときは null。 */
  selectedResumeDisabledReason: string | null;
  /** 選択中ノードの Resume エッジが持つイベント名。無ければ null。 */
  resumeEventName: string | null;
};

/**
 * 実行ダッシュボードの取得、比較、再生、ノード操作を持つ。
 *
 * 画面は戻り値を `ExecutionDashboardView` へ渡す。全画面でないときの見出しや、イベント名入力は画面側に残す。
 *
 * @param options 初期実行 ID と、比較・操作・表示モードの有無。
 * @returns 描画用の状態とコマンド。
 */
export function useExecutionDashboard({
  initialExecutionId,
  autoLoadOnMount = false,
  headerTitle,
  executionIdEditable = true,
  comparisonEnabled = true,
  operationsEnabled = true,
  initialViewMode = "list",
  lockViewMode = false
}: UseExecutionDashboardOptions): ExecutionDashboardModel {
  const { uiText, locale } = useI18n();
  const effectiveHeaderTitle = headerTitle ?? uiText.executionDashboard.header.titleDefault;
  const [executionId, setExecutionId] = useState(initialExecutionId);
  const [viewMode, setViewMode] = useState<ViewMode>(initialViewMode);
  const [graphFullscreen, setGraphFullscreen] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [graphViewportByExecutionId, setGraphViewportByExecutionId] = useState<GraphViewportByExecutionId>({});
  const [replayAtSeq, setReplayAtSeq] = useState<number | null>(null);
  const [compareMode, setCompareMode] = useState(false);
  const [executionIdB, setExecutionIdB] = useState("");
  const [executionB, setExecutionB] = useState<ExecutionView | null>(null);
  const [loadingB, setLoadingB] = useState(false);
  const [streamEnabled, setStreamEnabled] = useState(true);

  useEffect(() => {
    setExecutionId(initialExecutionId);
  }, [initialExecutionId]);

  useEffect(() => {
    if (lockViewMode) setViewMode(initialViewMode);
  }, [initialViewMode, lockViewMode]);

  useEffect(() => {
    try {
      const raw = sessionStorage.getItem(STREAM_PREF_STORAGE_KEY);
      if (raw === "0") setStreamEnabled(false);
      else if (raw === "1") setStreamEnabled(true);
    } catch {
      // sessionStorage 不可時は既定のまま
    }
  }, []);

  const handleStreamEnabledChange = useCallback((enabled: boolean) => {
    setStreamEnabled(enabled);
    try {
      sessionStorage.setItem(STREAM_PREF_STORAGE_KEY, enabled ? "1" : "0");
    } catch {
      // sessionStorage 不可時は画面上の切り替えだけ残す
    }
  }, []);

  const executionHookOptions = useMemo(
    () => ({
      onError: (err: unknown) => setToast(toToastError(err)),
      onCancelSuccess: () => setToast({ tone: "success", message: uiText.executionDashboard.toasts.cancelAccepted }),
      onPublishSuccess: () => setToast({ tone: "success", message: uiText.executionDashboard.toasts.publishAccepted }),
      streamEnabled
    }),
    [
      streamEnabled,
      uiText.executionDashboard.toasts.cancelAccepted,
      uiText.executionDashboard.toasts.publishAccepted
    ]
  );

  const {
    execution,
    loading: executionLoading,
    canCancel,
    terminal,
    loadExecution,
    cancelExecution,
    publishEvent,
    selectedNodeId,
    setSelectedNodeId
  } = useExecution(executionId, executionHookOptions);

  const loadExecutionRef = useRef(loadExecution);
  loadExecutionRef.current = loadExecution;
  const didAutoLoadRef = useRef(false);

  useEffect(() => {
    if (!autoLoadOnMount) return;
    didAutoLoadRef.current = false;
  }, [initialExecutionId, autoLoadOnMount]);

  useEffect(() => {
    if (!autoLoadOnMount || !executionId.trim() || didAutoLoadRef.current) return;
    didAutoLoadRef.current = true;
    void loadExecutionRef.current();
  }, [autoLoadOnMount, executionId]);

  const {
    events: timelineEvents,
    hasMore: timelineHasMore,
    loading: timelineLoading,
    loadingMore: timelineLoadingMore,
    error: timelineError,
    loadMore: timelineLoadMore
  } = useExecutionEvents(execution?.displayId ?? null);
  const { state: stateAtSeq, loading: stateAtSeqLoading } = useExecutionStateAtSeq(
    execution?.displayId ?? null,
    replayAtSeq
  );

  const displayExecution: ExecutionView | null =
    replayAtSeq != null && stateAtSeq != null ? stateAtSeq : execution;
  const isReplaying = replayAtSeq != null && stateAtSeq != null;

  const { definition: graphDefinition, loading: graphDefinitionLoading } = useGraphDefinition(
    execution?.graphId ?? null
  );
  const graphData = useGraphData(displayExecution, graphDefinition);

  const { resumeNode, loading: nodeLoading } = useNodeCommands(execution, {
    commandsEnabled: operationsEnabled,
    onSuccess: () => {
      setToast({ tone: "success", message: uiText.executionDashboard.toasts.resumeAccepted });
      void loadExecution();
    },
    onError: (err) => setToast(toToastError(err))
  });

  const loading =
    executionLoading || nodeLoading || stateAtSeqLoading || graphDefinitionLoading;

  const loadExecutionB = useCallback(async () => {
    if (!executionIdB.trim()) return;
    setLoadingB(true);
    try {
      const executionDto = await apiGet<ExecutionDTO>(`/executions/${executionIdB.trim()}`);
      let graph: ExecutionGraphDTO | null = null;
      try {
        graph = await apiGet<ExecutionGraphDTO>(`/executions/${executionIdB.trim()}/graph`);
      } catch {
        // グラフが無くても実行本体の比較は続ける
      }
      setExecutionB(buildExecutionView(executionDto, graph));
    } catch {
      setExecutionB(null);
    } finally {
      setLoadingB(false);
    }
  }, [executionIdB]);

  const executionDiff = useMemo(
    () => computeExecutionDiff(execution, executionB),
    [execution, executionB]
  );

  const selectedNode = useMemo(
    () => getNodeWithFallback(displayExecution, graphData, selectedNodeId),
    [displayExecution, graphData, selectedNodeId]
  );

  const selectedResumeDisabledReason = isReplaying
    ? uiText.executionDashboard.replayDisabledReason
    : getResumeDisabledReason(execution, selectedNode, operationsEnabled, locale);

  const resumeEventName = useMemo(() => {
    if (!selectedNodeId || !graphData?.edges) return null;
    const resumeEdge = graphData.edges.find(
      (edge) => edge.from === selectedNodeId && edge.edgeType === "Resume"
    );
    return resumeEdge?.eventName ?? null;
  }, [selectedNodeId, graphData?.edges]);

  const savedGraphViewport = execution ? graphViewportByExecutionId[execution.displayId] : undefined;
  const handleGraphViewportChange = useCallback(
    (viewport: GraphViewport) => {
      if (displayExecution) {
        setGraphViewportByExecutionId((prev) => ({
          ...prev,
          [displayExecution.displayId]: viewport
        }));
      }
    },
    [displayExecution]
  );

  useEffect(() => {
    if (viewMode !== "graph" || !execution) setGraphFullscreen(false);
  }, [viewMode, execution]);

  useEffect(() => {
    if (!graphFullscreen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setGraphFullscreen(false);
    };
    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    globalThis.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = originalOverflow;
      globalThis.removeEventListener("keydown", onKeyDown);
    };
  }, [graphFullscreen]);

  const showExecutionPanels = !!execution;
  const getResumeDisabledReasonForNode = useCallback(
    (nodeId: string) => {
      const node = getNodeWithFallback(displayExecution, graphData, nodeId);
      return isReplaying
        ? uiText.executionDashboard.replayDisabledReason
        : getResumeDisabledReason(execution, node, operationsEnabled, locale);
    },
    [
      displayExecution,
      graphData,
      isReplaying,
      execution,
      operationsEnabled,
      locale,
      uiText.executionDashboard.replayDisabledReason
    ]
  );

  const handleToggleGraphFullscreen = useCallback(() => {
    setGraphFullscreen((value) => !value);
  }, []);

  const handleCloseToast = useCallback(() => {
    setToast(null);
  }, []);

  const handleBackToCurrent = useCallback(() => {
    setReplayAtSeq(null);
  }, []);

  const requestLoadExecution = useCallback(() => {
    void loadExecution();
  }, [loadExecution]);

  const requestCancelExecution = useCallback(() => {
    void cancelExecution();
  }, [cancelExecution]);

  const requestPublishEvent = useCallback(
    (eventName: string) => {
      void publishEvent(eventName);
    },
    [publishEvent]
  );

  const requestLoadExecutionB = useCallback(() => {
    void loadExecutionB();
  }, [loadExecutionB]);

  const requestResumeNode = useCallback(
    (nodeId: string, eventName: string) => {
      void resumeNode(nodeId, eventName);
    },
    [resumeNode]
  );

  const resumeSelectedNode = useCallback(
    (eventName: string) => {
      if (isReplaying || !selectedNode) return;
      void resumeNode(selectedNode.nodeId, eventName);
    },
    [isReplaying, resumeNode, selectedNode]
  );

  const changeViewMode = useCallback(
    (mode: ViewMode) => {
      if (lockViewMode) return;
      setViewMode(mode);
    },
    [lockViewMode]
  );

  return {
    graphFullscreen,
    headerTitle: effectiveHeaderTitle,
    toast,
    onCloseToast: handleCloseToast,
    executionId,
    executionIdEditable,
    onExecutionIdChange: setExecutionId,
    onLoadExecution: requestLoadExecution,
    onCancelExecution: requestCancelExecution,
    loading,
    canCancel,
    onPublishEvent: requestPublishEvent,
    execution,
    viewMode,
    onViewModeChange: changeViewMode,
    showViewToggle: !lockViewMode,
    compareMode,
    onCompareModeChange: setCompareMode,
    comparisonEnabled,
    operationsEnabled,
    streamEnabled,
    onStreamEnabledChange: handleStreamEnabledChange,
    showExecutionPanels,
    executionB,
    executionIdB,
    onExecutionIdBChange: setExecutionIdB,
    onLoadExecutionB: requestLoadExecutionB,
    loadingB,
    executionDiff,
    onSelectNode: setSelectedNodeId,
    terminal,
    isReplaying,
    onBackToCurrent: handleBackToCurrent,
    timelineEvents,
    timelineLoading,
    timelineError,
    replayAtSeq,
    onSelectSeq: setReplayAtSeq,
    timelineHasMore,
    timelineLoadingMore,
    onTimelineLoadMore: timelineLoadMore,
    displayExecution,
    selectedNodeId,
    graphData,
    onToggleGraphFullscreen: handleToggleGraphFullscreen,
    onResumeNode: requestResumeNode,
    onResumeSelectedNode: resumeSelectedNode,
    getResumeDisabledReasonForNode,
    savedGraphViewport,
    onGraphViewportChange: handleGraphViewportChange,
    selectedNode,
    selectedResumeDisabledReason,
    resumeEventName
  };
}
