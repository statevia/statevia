import { describe, expect, it } from "vitest";
import { parseExecutionStreamEvent } from "../../features/executions/lib/executionStream";

describe("parseExecutionStreamEvent", () => {
  it("GraphUpdated イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "RUNNING" }] },
      at: "2026-01-01T00:00:00Z"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("GraphUpdated");
    expect(result?.executionId).toBe("ex-1");
  });

  it("ExecutionStatusChanged イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "COMPLETED",
      at: "2026-01-01T00:00:00Z"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("ExecutionStatusChanged");
    expect((result as { to: string }).to).toBe("COMPLETED");
  });

  it("空 payload のとき null を返す", () => {
    // Arrange
    const raw = "";

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("不正な JSON のとき null を返す", () => {
    // Arrange
    const raw = "not json";

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("未対応の event type のとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "EXECUTION_CREATED",
      executionId: "ex-1"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("executionId が無いとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify({ type: "GraphUpdated", patch: { nodes: [] } });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("NodeCancelled イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "NodeCancelled",
      executionId: "ex-1",
      nodeId: "n-1",
      cancel: { reason: "user" },
      at: "2026-01-01T00:00:00Z"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("NodeCancelled");
    expect((result as { nodeId: string }).nodeId).toBe("n-1");
  });

  it("NodeFailed イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "NodeFailed",
      executionId: "ex-1",
      nodeId: "n-1",
      error: { message: "timeout" }
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("NodeFailed");
  });

  it("type が無いとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify({ executionId: "ex-1" });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("パース結果がオブジェクトでないとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify("string");

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });
});
