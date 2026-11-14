"use client";

import { useI18n } from "@/shared/i18n/uiTextContext";
import { getStatusStyle } from "@/shared/lib/statusStyle";
import { StatusDot } from "@/shared/ui/StatusBadge";
import type { DashboardExecutionCounts } from "../types";

/** 件数のタイルの props。 */
export type DashboardTilesProps = {
  /** 期間で区切らない件数（実行中・待機中）。null ならそのタイルを出さない。 */
  countsAll: DashboardExecutionCounts | null;
  /** 直近 24 時間に始まった実行の件数（失敗・完了）。null ならそのタイルを出さない。 */
  countsRecent: DashboardExecutionCounts | null;
};

type Tile = {
  id: string;
  /** 点の形と色に使う状態。 */
  status: string;
  label: string;
  value: number;
  note?: string;
  /** 枠の色。待機中と失敗だけ目立たせる。 */
  frameClass: string;
};

/**
 * ダッシュボードの件数のタイル（モック 09）。実行中・待機中・失敗（24 時間）・完了（24 時間）を並べる。
 * @remarks 完了のタイルの成功率は、直近 24 時間に始まった実行の完了 ÷（完了 ＋ 失敗）。どちらも 0 なら出さない。
 */
export function DashboardTiles({ countsAll, countsRecent }: Readonly<DashboardTilesProps>) {
  const { uiText } = useI18n();
  const text = uiText.dashboard.tiles;
  const tiles: Tile[] = [];
  if (countsAll) {
    tiles.push(
      { id: "active", status: "ACTIVE", label: text.active, value: countsAll.byStatus.ACTIVE, frameClass: "border-md-outline" },
      {
        id: "waiting",
        status: "WAITING",
        label: text.waiting,
        value: countsAll.waiting,
        note: text.waitingNote,
        frameClass: countsAll.waiting > 0 ? "border-md-warning/60" : "border-md-outline"
      }
    );
  }
  if (countsRecent) {
    const completed = countsRecent.byStatus.COMPLETED;
    const failed = countsRecent.byStatus.FAILED;
    const finished = completed + failed;
    tiles.push(
      {
        id: "failed",
        status: "FAILED",
        label: text.failed24h,
        value: failed,
        frameClass: failed > 0 ? "border-md-error/60" : "border-md-outline"
      },
      {
        id: "completed",
        status: "COMPLETED",
        label: text.completed24h,
        value: completed,
        note: finished > 0 ? text.successRate(Math.round((completed / finished) * 1000) / 10) : undefined,
        frameClass: "border-md-outline"
      }
    );
  }
  if (tiles.length === 0) return null;

  return (
    <ul aria-label={text.ariaLabel} className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {tiles.map((tile) => {
        const style = getStatusStyle(tile.status);
        return (
          <li key={tile.id} className={`flex flex-col gap-1 rounded-card border bg-md-surface px-5 py-4 ${tile.frameClass}`}>
            <span className={`inline-flex items-center gap-1.5 text-[13px] font-medium ${style.textClass}`}>
              <StatusDot shape={style.dotShape} accentClass={style.accentClass} />
              {tile.label}
            </span>
            <span className="text-3xl font-bold text-md-on-surface tabular-nums">{tile.value}</span>
            {tile.note ? <span className="text-xs text-md-on-surface-variant">{tile.note}</span> : null}
          </li>
        );
      })}
    </ul>
  );
}
