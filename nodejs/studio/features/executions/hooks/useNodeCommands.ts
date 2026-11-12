"use client";

import { useState } from "react";
import { apiPost } from "@/shared/api";
import { DEFAULT_LOCALE, type Locale } from "@/shared/i18n/i18n";
import type { CommandAccepted, ExecutionNodeDTO, ExecutionView } from "../types";
import { getUiText } from "@/shared/i18n/uiTextLocale";
import { resolveWaitResumeEvents } from "../lib/waitResumeEvents";

const TERMINAL_STATUSES = new Set<string>(["COMPLETED", "CANCELED", "FAILED"]);

function isTerminalExecution(status: ExecutionView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

/**
 * Resume ボタンを無効化すべき理由を返す（操作可能なら null）。
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

/** useNodeCommands のオプション。 */
export type UseNodeCommandsOptions = {
  /** 操作に成功したときに呼ぶ。 */
  onSuccess?: () => void;
  /** 操作に失敗したときに呼ぶ。 */
  onError?: (error: unknown) => void;
  /** 操作を有効にするか。executions.write を持つときだけ true にする。既定は true。 */
  commandsEnabled?: boolean;
};

/** ノードへの Resume 等コマンドを発行するフック。 */
export function useNodeCommands(
  execution: ExecutionView | null,
  options: UseNodeCommandsOptions = {}
) {
  const { onSuccess, onError, commandsEnabled = true } = options;
  const [loading, setLoading] = useState(false);

  /**
   * Wait ノードを指定イベント名で Resume する。
   *
   * @param nodeId 実行ノード短名 UUID（`nodeId`）。
   * @param resumeKey 再開イベント名。Subscribe では内部イベント名であり、topic は載せない。
   */
  async function resumeNode(nodeId: string, resumeKey: string) {
    if (!commandsEnabled) return;
    if (!execution) return;
    const eventName = resumeKey.trim();
    if (eventName.length === 0) return;
    const node = execution.nodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    setLoading(true);
    try {
      await apiPost<CommandAccepted>(
        `/executions/${execution.displayId}/nodes/${node.nodeId}/resume`,
        { resumeKey: eventName }
      );
      onSuccess?.();
    } catch (error) {
      onError?.(error);
    } finally {
      setLoading(false);
    }
  }

  return { resumeNode, loading };
}
