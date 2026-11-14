import type { ExecutionView } from "../types";
import { resolveWaitResumeEvents } from "./waitResumeEvents";

const TERMINAL_EXECUTION_STATUSES = new Set(["COMPLETED", "CANCELED", "FAILED"]);

/** イベントを待っている 1 つのノード（イベント送信の届け先の候補）。 */
export type WaitTarget = {
  /** 実行ノード ID。`POST .../nodes/{nodeId}/resume` に使う。 */
  nodeId: string;
  /** 画面に出すノード名。名前が無ければノード ID。 */
  nodeName: string;
  /** 受け付けるイベント名（`allowedEvents`、無ければ `waitKey`）。1 件以上。 */
  events: string[];
  /** 待ち始めた日時（ISO 8601）。分からなければ null。 */
  waitingSince: string | null;
};

/**
 * 実行の中でイベントを待っているノードを、届け先の候補として並べる。
 * @remarks 受け付けるイベント名が 1 つも無い待機は、どのイベントでも再開できないため含めない。
 * 並び順は実行のノードの順のまま。
 * @param execution 実行。未取得なら null。
 * @returns 待っているノード。無ければ空。
 */
export function listWaitTargets(execution: ExecutionView | null): WaitTarget[] {
  if (!execution) return [];
  return execution.nodes
    .filter((node) => node.status === "WAITING")
    .map((node) => ({
      nodeId: node.nodeId,
      nodeName: node.nodeName?.trim() ? node.nodeName : node.nodeId,
      events: resolveWaitResumeEvents(node),
      waitingSince: node.startedAt ?? null
    }))
    .filter((target) => target.events.length > 0);
}

/**
 * 実行がイベントを受け付ける状態か（終了しておらず、キャンセルを要求していない）。
 * @param execution 実行。未取得なら null。
 * @returns 受け付けるとき true。
 */
export function canAcceptEvents(execution: ExecutionView | null): boolean {
  if (!execution) return false;
  if (execution.cancelRequested) return false;
  return !TERMINAL_EXECUTION_STATUSES.has(execution.status);
}
