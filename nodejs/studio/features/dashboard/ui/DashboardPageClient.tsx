"use client";

import { RefreshCw } from "lucide-react";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useProject } from "@/shared/project/projectContext";
import { Button } from "@/shared/ui/Button";
import { DataList, type DataListColumn } from "@/shared/ui/DataList";
import { StatusBadge } from "@/shared/ui/StatusBadge";
import { TenantMissingBanner } from "@/shared/ui/TenantMissingBanner";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { apiGet } from "@/shared/api";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import type { DashboardExecutionItem, DashboardExecutionsPage } from "../types";
import { useI18n } from "@/shared/i18n/uiTextContext";

/**
 * 直近実行の取得パス。
 * @param projectId 選択中 Project。Service API は未指定だと所属 Project の和を返すため、必ず付ける。
 * @returns `/executions` の相対パス。
 */
function buildRecentExecutionsPath(projectId: string): string {
  const query = new URLSearchParams({ limit: "10", offset: "0", projectId });
  return `/executions?${query.toString()}`;
}

/**
 * 直近実行 10 件のダッシュボード（一覧取得・空状態・詳細への導線）。
 * 一覧はサイドメニューで選択中の Project に絞る。所属一覧の取得前は取得せず、所属が無いときは空表示にする。
 * 見出しの右に最後に読み込んだ時刻と再読み込みを置く（モック 09）。状態ごとの件数のタイルと「対応が必要な実行」は、
 * 一覧 API の拡張（`studio-list-api-extensions`）を画面へ出すタスクで足す。
 */
export function DashboardPageClient() {
  const { uiText, locale } = useI18n();
  const text = uiText.dashboard;
  const dateTimeLocale = getDateTimeLocale(locale);
  const [items, setItems] = useState<DashboardExecutionItem[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [lastLoadedAt, setLastLoadedAt] = useState<Date | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const { selectedProject, loaded: projectsLoaded } = useProject();
  const projectId = selectedProject?.projectId ?? null;

  const load = useCallback(async () => {
    if (!projectsLoaded) {
      return;
    }
    if (projectId === null) {
      // 未指定で呼ぶと所属の和が返る。選択が無いときは取得せず空にする。
      setItems([]);
      setTotalCount(0);
      setToast(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    setToast(null);
    try {
      const page = await apiGet<DashboardExecutionsPage>(buildRecentExecutionsPath(projectId));
      setItems(page.items);
      setTotalCount(page.totalCount);
      setLastLoadedAt(new Date());
    } catch (error) {
      setToast(toToastError(error));
      setItems(null);
      setTotalCount(null);
    } finally {
      setLoading(false);
    }
  }, [projectId, projectsLoaded]);

  useEffect(() => {
    void load();
  }, [load]);

  const empty = !loading && items !== null && items.length === 0;

  const columns: DataListColumn<DashboardExecutionItem>[] = [
    {
      id: "status",
      header: text.columns.status,
      // 状態の文字の長さで右の ID の開始位置がずれないよう、状態の列幅を固定する
      width: "7.5rem",
      hideLabelOnMobile: true,
      cell: (execution) => <StatusBadge status={execution.status} />,
    },
    {
      id: "execution",
      header: text.columns.execution,
      width: "minmax(0,1fr)",
      hideLabelOnMobile: true,
      cell: (execution) => (
        <Link
          href={`/executions/${encodeURIComponent(execution.displayId)}`}
          className="block truncate font-mono text-md-primary hover:underline"
          title={execution.displayId}
        >
          {execution.displayId}
        </Link>
      ),
    },
    {
      id: "updatedAt",
      header: text.columns.updatedAt,
      width: "10rem",
      align: "end",
      cell: (execution) => (
        <span className="whitespace-nowrap text-xs text-md-on-surface-variant">
          {formatDateTimeLocalized(execution.updatedAt ?? execution.startedAt, dateTimeLocale)}
        </span>
      ),
    },
  ];

  return (
    <PageShell
      title={text.title}
      description={text.descriptionRecent}
      primaryActions={
        <>
          {lastLoadedAt ? (
            <span className="text-xs text-md-on-surface-variant">
              {text.lastLoadedAt(lastLoadedAt.toLocaleTimeString(dateTimeLocale, { hour: "2-digit", minute: "2-digit" }))}
            </span>
          ) : null}
          <Button
            variant="outline"
            size="icon"
            aria-label={uiText.actions.reload}
            title={uiText.actions.reload}
            disabled={loading}
            onClick={() => void load()}
          >
            <RefreshCw aria-hidden="true" className="h-4 w-4" />
          </Button>
        </>
      }
    >
      <TenantMissingBanner />

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && <PageState state="loading" message={text.loadingRecent} />}

      {empty && <PageState state="empty" message={text.emptyStartFromDefinitionsOrExecutions} />}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label={text.aria.recentExecutionsList} className="flex flex-col gap-3">
          <div className="flex flex-wrap items-baseline justify-between gap-3">
            <div className="flex items-baseline gap-3">
              <h2 className="text-base font-semibold text-md-on-surface">{text.recentTitle}</h2>
              <p className="text-xs text-md-on-surface-variant">{text.totalCount(totalCount)}</p>
            </div>
            <Link href="/executions" className="text-sm text-md-primary hover:underline">
              {text.openExecutions}
            </Link>
          </div>
          <DataList
            ariaLabel={text.aria.recentExecutionsList}
            columns={columns}
            items={items}
            getRowKey={(execution) => execution.displayId}
          />
        </section>
      )}

      {!loading && items === null && !toast && (
        <PageState state="error" message={text.error.fetchFailed} onRetry={() => void load()} />
      )}
    </PageShell>
  );
}
