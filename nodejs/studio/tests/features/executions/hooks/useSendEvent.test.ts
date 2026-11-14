import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useSendEvent } from "@/features/executions/hooks/useSendEvent";
import * as api from "@/shared/api";

describe("useSendEvent", () => {
  beforeEach(() => {
    vi.spyOn(api, "apiPost").mockResolvedValue({
      executionId: "ex-1",
      command: "resume",
      accepted: true,
      idempotencyKey: "key-1"
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("ノードを届け先にすると resume をイベント名つきで呼び、受理を知らせる", async () => {
    // Arrange
    const onAccepted = vi.fn();
    const { result } = renderHook(() => useSendEvent("ex-1", { onAccepted }));

    // Act
    let error: string | null = "not-called";
    await act(async () => {
      error = await result.current.send({ kind: "node", nodeId: "n-1", eventName: " approve " });
    });

    // Assert
    expect(error).toBeNull();
    expect(api.apiPost).toHaveBeenCalledWith("/executions/ex-1/nodes/n-1/resume", { resumeKey: "approve" });
    expect(onAccepted).toHaveBeenCalledWith({ kind: "node", nodeId: "n-1", eventName: " approve " });
  });

  it("イベント名を指定すると実行の events を呼ぶ", async () => {
    // Arrange
    const { result } = renderHook(() => useSendEvent("ex-1"));

    // Act
    await act(async () => {
      await result.current.send({ kind: "execution", eventName: "payment.confirmed" });
    });

    // Assert
    expect(api.apiPost).toHaveBeenCalledWith("/executions/ex-1/events", { name: "payment.confirmed" });
  });

  it("失敗したら受理を知らせず、画面に出す文言を返す", async () => {
    // Arrange
    vi.mocked(api.apiPost).mockRejectedValueOnce({ status: 422, error: { code: "WAIT_NOT_FOUND", message: "no wait" } });
    const onAccepted = vi.fn();
    const { result } = renderHook(() => useSendEvent("ex-1", { onAccepted }));

    // Act
    let error: string | null = null;
    await act(async () => {
      error = await result.current.send({ kind: "execution", eventName: "approve" });
    });

    // Assert
    expect(error).toContain("WAIT_NOT_FOUND - no wait");
    expect(onAccepted).not.toHaveBeenCalled();
    expect(result.current.sending).toBe(false);
  });

  it("実行が未取得かイベント名が空なら API を呼ばない", async () => {
    // Arrange
    const { result: withoutExecution } = renderHook(() => useSendEvent(null));
    const { result: withExecution } = renderHook(() => useSendEvent("ex-1"));

    // Act
    await act(async () => {
      await withoutExecution.current.send({ kind: "execution", eventName: "approve" });
      await withExecution.current.send({ kind: "node", nodeId: "n-1", eventName: "   " });
    });

    // Assert
    expect(api.apiPost).not.toHaveBeenCalled();
  });
});
