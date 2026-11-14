"use client";

import { useState } from "react";
import { apiPost } from "@/shared/api";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError } from "@/shared/lib/errors";
import type { CommandAccepted } from "../types";

/**
 * イベントの届け先。
 * - `node`: 待っているノードだけへ届ける（`POST /v1/executions/{id}/nodes/{nodeId}/resume`。画面の語では「再開」）。
 * - `execution`: 実行へイベント名で送る（`POST /v1/executions/{id}/events`）。
 */
export type SendEventTarget =
  | { kind: "node"; nodeId: string; eventName: string }
  | { kind: "execution"; eventName: string };

/** useSendEvent のオプション。 */
export type UseSendEventOptions = {
  /**
   * API が受理したときに呼ぶ。再取得とトーストは呼び出し側で行う。
   * @param target 送った届け先。
   */
  onAccepted?: (target: SendEventTarget) => void;
};

/**
 * 実行の詳細のイベント送信ダイアログから、待機中の実行へイベントを届けるフック。
 * @remarks 失敗はトーストではなく戻り値で返し、ダイアログの中に出す（送信の失敗でダイアログを閉じないため）。
 * 権限（executions.write）は見ない。権限が無い利用者には、呼び出し側が送信の操作そのものを出さない。認可の正本は Service API。
 * @param executionDisplayId 実行の表示用 ID。未取得なら null。
 * @param options 受理したときの処理。
 * @returns 送信する関数と送信中か。
 */
export function useSendEvent(executionDisplayId: string | null, options: UseSendEventOptions = {}) {
  const { onAccepted } = options;
  const { locale } = useI18n();
  const [sending, setSending] = useState(false);

  /**
   * 届け先へイベントを送る。
   * @param target 届け先とイベント名。
   * @returns 受理されたら null。失敗したら画面に出すエラーの文言。
   */
  async function send(target: SendEventTarget): Promise<string | null> {
    const eventName = target.eventName.trim();
    if (!executionDisplayId || eventName.length === 0) return null;
    const executionPath = `/executions/${encodeURIComponent(executionDisplayId)}`;
    setSending(true);
    try {
      if (target.kind === "node") {
        await apiPost<CommandAccepted>(`${executionPath}/nodes/${encodeURIComponent(target.nodeId)}/resume`, {
          resumeKey: eventName
        });
      } else {
        await apiPost<CommandAccepted>(`${executionPath}/events`, { name: eventName });
      }
      onAccepted?.(target);
      return null;
    } catch (error) {
      return toToastError(error, locale).message;
    } finally {
      setSending(false);
    }
  }

  return { send, sending };
}
