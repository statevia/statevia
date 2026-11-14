import { describe, expect, it } from "vitest";
import { canAcceptEvents, listWaitTargets } from "@/features/executions/lib/waitTargets";
import type { ExecutionNodeDTO, ExecutionView } from "@/features/executions/types";

const node = (overrides: Partial<ExecutionNodeDTO>): ExecutionNodeDTO => ({
  nodeId: "n-1",
  nodeType: "WAIT",
  status: "WAITING",
  attempt: 1,
  workerId: null,
  waitKey: null,
  canceledByExecution: false,
  ...overrides
});

const execution = (overrides: Partial<ExecutionView> = {}): ExecutionView => ({
  displayId: "ex-1",
  resourceId: "r-1",
  graphId: "g-1",
  status: "ACTIVE",
  startedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false,
  nodes: [],
  ...overrides
});

describe("listWaitTargets", () => {
  it("待っていてイベント名があるノードだけを、名前とイベントと待ち始めつきで並べる", () => {
    // Arrange
    const target = execution({
      nodes: [
        node({ nodeId: "a", nodeName: "approve", allowedEvents: ["approval.granted"], startedAt: "2026-01-01T00:01:00Z" }),
        node({ nodeId: "b", nodeName: "ship", status: "IDLE" }),
        node({ nodeId: "c", nodeName: "no-event" }),
        node({ nodeId: "d", waitKey: "payment.confirmed" })
      ]
    });

    // Act
    const result = listWaitTargets(target);

    // Assert
    expect(result).toEqual([
      { nodeId: "a", nodeName: "approve", events: ["approval.granted"], waitingSince: "2026-01-01T00:01:00Z" },
      { nodeId: "d", nodeName: "d", events: ["payment.confirmed"], waitingSince: null }
    ]);
  });

  it("実行が未取得なら空", () => {
    // Arrange / Act / Assert
    expect(listWaitTargets(null)).toEqual([]);
  });
});

describe("canAcceptEvents", () => {
  it("ACTIVE でキャンセルを要求していなければ受け付ける", () => {
    // Arrange / Act / Assert
    expect(canAcceptEvents(execution())).toBe(true);
  });

  it("終了した実行とキャンセル要求済みの実行は受け付けない", () => {
    // Arrange / Act / Assert
    expect(canAcceptEvents(execution({ status: "COMPLETED" }))).toBe(false);
    expect(canAcceptEvents(execution({ cancelRequested: true }))).toBe(false);
    expect(canAcceptEvents(null)).toBe(false);
  });
});
