"use client";

import { toDisplayValue } from "@/shared/lib/displayValue";
import { getStatusStyle, type StatusLike } from "@/shared/lib/statusStyle";
import { StatusDot } from "@/shared/ui/StatusBadge";
import type { KnownAdminModuleStatus } from "../types";
import { isKnownModuleStatus } from "../modulesApi";

/**
 * ロード結果ごとに借りる状態バッジの見た目。色と点の形で区別する。
 * 読み込めた = 完了の緑の丸、失敗 = 失敗の赤のひし形、重複 = 待機の黄の四角、スキップ = 未着手の灰色の輪。
 */
const MODULE_STATUS_STYLE_KEY: Record<KnownAdminModuleStatus, StatusLike> = {
  Loaded: "COMPLETED",
  Failed: "FAILED",
  Duplicate: "WAITING",
  Skipped: "IDLE"
};

/**
 * Module のロード結果のピル。値は訳さず大文字にそろえて出す（状態の値を訳さない UI の決まりに合わせる）。
 * 契約に無い値でも落とさず、その文字列を灰色のピルで出す。
 * @param props `GET /v1/admin/modules` の `status`。
 * @returns ロード結果のピル。
 */
export function AdminModuleStatusPill({ status }: Readonly<{ status: string }>) {
  const style = getStatusStyle(isKnownModuleStatus(status) ? MODULE_STATUS_STYLE_KEY[status] : "IDLE");

  return (
    <span
      className={`inline-flex h-6 items-center gap-1.5 rounded-pill px-2.5 text-xs font-semibold ${style.bgClass} ${style.textClass}`}
    >
      <StatusDot shape={style.dotShape} accentClass={style.accentClass} />
      {toDisplayValue(status)}
    </span>
  );
}
