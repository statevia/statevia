"use client";

import { DEFAULT_LOCALE, type Locale } from "@/shared/i18n/i18n";
import type { ExecutionNodeDTO, ExecutionView } from "../types";
import { getUiText } from "@/shared/i18n/uiTextLocale";
import { resolveWaitResumeEvents } from "../lib/waitResumeEvents";

const TERMINAL_STATUSES = new Set<string>(["COMPLETED", "CANCELED", "FAILED"]);

function isTerminalExecution(status: ExecutionView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

/**
 * ノードへイベントを送る（Resume）操作を無効化すべき理由を返す（操作可能なら null）。
 * 権限（executions.write）は見ない。権限が無い利用者には、呼び出し側が再開の操作そのものを出さない。
 *
 * @param execution 実行ビュー（未読込なら null）。
 * @param node 対象ノード（未選択なら null）。
 * @param locale UI 文言ロケール。
 * @returns 無効化理由。操作可能なら null。
 */
export function getResumeDisabledReason(
  execution: ExecutionView | null,
  node: ExecutionNodeDTO | null,
  locale: Locale = DEFAULT_LOCALE
): string | null {
  const uiText = getUiText(locale);
  if (!execution) return uiText.nodeCommands.resumeDisabledReason.executionNotLoaded;
  if (!node) return uiText.nodeCommands.resumeDisabledReason.nodeNotSelected;
  if (isTerminalExecution(execution.status)) return uiText.nodeCommands.resumeDisabledReason.executionTerminal;
  if (execution.cancelRequested)
    return uiText.nodeCommands.resumeDisabledReason.cancelRequested;
  if (node.status !== "WAITING") return uiText.nodeCommands.resumeDisabledReason.waitingOnly;
  if (resolveWaitResumeEvents(node).length === 0)
    return uiText.nodeCommands.resumeDisabledReason.noResumeEvent;
  return null;
}
