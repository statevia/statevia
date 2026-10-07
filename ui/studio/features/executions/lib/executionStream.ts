import type { ExecutionStreamEvent } from "../types";

/** SSE ペイロード文字列を実行イベントにパースする。 */
export function parseExecutionStreamEvent(payload: string): ExecutionStreamEvent | null {
  if (!payload) return null;

  let parsed: unknown;
  try {
    parsed = JSON.parse(payload);
  } catch {
    return null;
  }

  if (!parsed || typeof parsed !== "object") return null;
  const event = parsed as Record<string, unknown>;
  if (typeof event.type !== "string" || typeof event.executionId !== "string") return null;

  const type = event.type;
  if (type !== "GraphUpdated" && type !== "ExecutionStatusChanged" && type !== "NodeCancelled" && type !== "NodeFailed") {
    return null;
  }

  return event as ExecutionStreamEvent;
}
