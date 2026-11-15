"use client";

import { useCallback, useEffect, useState } from "react";
import { apiGet } from "@/shared/api";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { useProject } from "@/shared/project/projectContext";
import type { DashboardExecutionCounts, DashboardExecutionItem, DashboardExecutionsPage } from "../types";

/** 直近の実行の件数。 */
const RECENT_LIMIT = 10;
/** 対応が必要な実行の件数。全件は実行一覧の「要対応」で見る。 */
const ATTENTION_LIMIT = 5;
/** 失敗・完了のタイルで数える期間（開始日時で区切る）。 */
const RECENT_WINDOW_MS = 24 * 60 * 60 * 1000;

/**
 * 実行一覧の取得パス。
 * @param projectId 選択中 Project。Service API は未指定だと所属 Project の和を返すため、必ず付ける。
 * @param limit 件数。
 * @param needsAttention true なら要対応（待機中または `FAILED`）だけ。
 * @returns `/executions` の相対パス。
 */
export function buildDashboardExecutionsPath(projectId: string, limit: number, needsAttention = false): string {
  const query = new URLSearchParams({ limit: String(limit), offset: "0" });
  if (needsAttention) query.set("needsAttention", "true");
  query.set("projectId", projectId);
  return `/executions?${query.toString()}`;
}

/**
 * 実行の件数の取得パス。
 * @param projectId 選択中 Project。
 * @param startedFrom 開始日時の下限（ISO 8601）。省略すると期間で区切らない。
 * @returns `/executions/counts` の相対パス。
 */
export function buildDashboardCountsPath(projectId: string, startedFrom?: string): string {
  const query = new URLSearchParams({ projectId });
  if (startedFrom) query.set("startedFrom", startedFrom);
  return `/executions/counts?${query.toString()}`;
}

/** ダッシュボードの描画用状態。 */
export type DashboardModel = {
  /** 直近の実行。失敗時は null。 */
  recent: DashboardExecutionItem[] | null;
  /** 直近の実行の総件数。 */
  totalCount: number | null;
  /** 対応が必要な実行（新しい順に最大 5 件）。取得に失敗したら null（節を出さない）。 */
  attention: DashboardExecutionItem[] | null;
  /** 期間で区切らない件数（実行中・待機中のタイル）。取得に失敗したら null（タイルを出さない）。 */
  countsAll: DashboardExecutionCounts | null;
  /** 直近 24 時間に始まった実行の件数（失敗・完了のタイル）。取得に失敗したら null。 */
  countsRecent: DashboardExecutionCounts | null;
  /** 取得中か。 */
  loading: boolean;
  /** 最後に読み込んだ時刻。 */
  lastLoadedAt: Date | null;
  /** 直近の実行の取得失敗のトースト。 */
  toast: ToastState | null;
  /** トーストを閉じる。 */
  dismissToast: () => void;
  /** 取り直す。 */
  reload: () => Promise<void>;
};

/**
 * ダッシュボードの取得を持つ（直近の実行・対応が必要な実行・件数のタイル）。
 * @remarks サイドメニューで選択中の Project に絞る。所属一覧の取得前は取得せず、所属が無いときは空にする。
 * 直近の実行が主で、失敗したらトーストとエラーを出す。件数と対応が必要な実行は補助で、失敗したらその部分を出さないだけにする。
 * 失敗・完了のタイルは「直近 24 時間に始まった実行」で数える（API の期間は開始日時で区切るため。それより前に始まって最近失敗した実行は入らない）。
 * @returns 描画用の状態と操作。
 */
export function useDashboard(): DashboardModel {
  const { selectedProject, loaded: projectsLoaded } = useProject();
  const projectId = selectedProject?.projectId ?? null;
  const [recent, setRecent] = useState<DashboardExecutionItem[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [attention, setAttention] = useState<DashboardExecutionItem[] | null>(null);
  const [countsAll, setCountsAll] = useState<DashboardExecutionCounts | null>(null);
  const [countsRecent, setCountsRecent] = useState<DashboardExecutionCounts | null>(null);
  const [loading, setLoading] = useState(true);
  const [lastLoadedAt, setLastLoadedAt] = useState<Date | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);

  const reload = useCallback(async () => {
    if (!projectsLoaded) return;
    if (projectId === null) {
      setRecent([]);
      setTotalCount(0);
      setAttention(null);
      setCountsAll(null);
      setCountsRecent(null);
      setToast(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    setToast(null);
    const startedFrom = new Date(Date.now() - RECENT_WINDOW_MS).toISOString();
    const [recentResult, attentionResult, allResult, recentCountsResult] = await Promise.allSettled([
      apiGet<DashboardExecutionsPage>(buildDashboardExecutionsPath(projectId, RECENT_LIMIT)),
      apiGet<DashboardExecutionsPage>(buildDashboardExecutionsPath(projectId, ATTENTION_LIMIT, true)),
      apiGet<DashboardExecutionCounts>(buildDashboardCountsPath(projectId)),
      apiGet<DashboardExecutionCounts>(buildDashboardCountsPath(projectId, startedFrom))
    ]);
    if (recentResult.status === "fulfilled") {
      setRecent(recentResult.value.items);
      setTotalCount(recentResult.value.totalCount);
      setLastLoadedAt(new Date());
    } else {
      setToast(toToastError(recentResult.reason));
      setRecent(null);
      setTotalCount(null);
    }
    setAttention(attentionResult.status === "fulfilled" ? attentionResult.value.items : null);
    setCountsAll(allResult.status === "fulfilled" ? allResult.value : null);
    setCountsRecent(recentCountsResult.status === "fulfilled" ? recentCountsResult.value : null);
    setLoading(false);
  }, [projectId, projectsLoaded]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const dismissToast = useCallback(() => setToast(null), []);

  return { recent, totalCount, attention, countsAll, countsRecent, loading, lastLoadedAt, toast, dismissToast, reload };
}
