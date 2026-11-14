"use client";

import { RefreshCw } from "lucide-react";
import Link from "next/link";
import { Button } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
import { DataList, type DataListColumn } from "@/shared/ui/DataList";
import { StatusBadge } from "@/shared/ui/StatusBadge";
import { TenantMissingBanner } from "@/shared/ui/TenantMissingBanner";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { useDashboard } from "../hooks/useDashboard";
import type { DashboardExecutionItem } from "../types";
import { DashboardTiles } from "./DashboardTiles";

/**
 * ダッシュボード（モック 09）。件数のタイル、対応が必要な実行、直近の実行 10 件を並べる。
 * 一覧はサイドメニューで選択中の Project に絞る。所属一覧の取得前は取得せず、所属が無いときは空表示にする。
 * 見出しの右に最後に読み込んだ時刻と再読み込みを置く。
 */
export function DashboardPageClient() {
  const { uiText, locale } = useI18n();
  const text = uiText.dashboard;
  const dateTimeLocale = getDateTimeLocale(locale);
  const { recent, totalCount, attention, countsAll, countsRecent, loading, lastLoadedAt, toast, dismissToast, reload } =
    useDashboard();

  const empty = !loading && recent !== null && recent.length === 0;

  const columns: DataListColumn<DashboardExecutionItem>[] = [
    {
      id: "status",
      header: text.columns.status,
      // 状態の文字の長さと待機中の印で右の ID の開始位置がずれないよう、状態の列幅を固定する
      width: "11rem",
      hideLabelOnMobile: true,
      cell: (execution) => <StatusBadge status={execution.status} waiting={execution.waiting} />,
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
            onClick={() => void reload()}
          >
            <RefreshCw aria-hidden="true" className="h-4 w-4" />
          </Button>
        </>
      }
    >
      <TenantMissingBanner />

      <Toast toast={toast} onClose={dismissToast} />

      {loading && <PageState state="loading" message={text.loadingRecent} />}

      {!loading && <DashboardTiles countsAll={countsAll} countsRecent={countsRecent} />}

      {!loading && attention !== null && (
        <Card
          title={text.attention.title}
          description={text.attention.description}
          actions={
            <Link href="/executions?needsAttention=true" className="text-sm text-md-primary hover:underline">
              {text.attention.openAll}
            </Link>
          }
          padding="none"
        >
          {attention.length > 0 ? (
            <DataList
              ariaLabel={text.attention.title}
              columns={columns}
              items={attention}
              getRowKey={(execution) => execution.displayId}
            />
          ) : (
            <p className="px-5 py-4 text-sm text-md-on-surface-variant">{text.attention.empty}</p>
          )}
        </Card>
      )}

      {empty && <PageState state="empty" message={text.emptyStartFromDefinitionsOrExecutions} />}

      {!loading && recent !== null && recent.length > 0 && (
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
            items={recent}
            getRowKey={(execution) => execution.displayId}
          />
        </section>
      )}

      {!loading && recent === null && !toast && (
        <PageState state="error" message={text.error.fetchFailed} onRetry={() => void reload()} />
      )}
    </PageShell>
  );
}
