import { describe, expect, it, vi } from "vitest";
import { renderHook } from "@testing-library/react";
import {
  expandWaitingNodeLayout,
  getNodeWithFallback,
  useGraphData,
  WAITING_RESUME_LAYOUT_HEIGHT
} from "../../../features/executions/hooks/useGraphData";
import { layoutGraph } from "@/shared/lib/graphLayout";
import type { ExecutionNodeDTO, ExecutionView } from "@/features/executions/types";
import { getGraphDefinition } from "@/features/executions/graphs/registry";
import type { GraphDefinition } from "@/features/executions/graphs/types";

vi.mock("@/shared/lib/graphLayout", async () => {
  const actual = await vi.importActual<typeof import("@/shared/lib/graphLayout")>("@/shared/lib/graphLayout");
  return {
    ...actual,
    layoutGraph: vi.fn(actual.layoutGraph)
  };
});

function execution(nodes: ExecutionNodeDTO[], graphId = "hello"): ExecutionView {
  return {
    displayId: "ex-1",
    resourceId: "res-1",
    status: "Running",
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    graphId,
    nodes
  };
}

describe("useGraphData", () => {
  it("execution が null のとき null を返す", () => {
    // Arrange
    const def = getGraphDefinition("hello");

    // Act
    const { result } = renderHook(() => useGraphData(null, def));

    // Assert
    expect(result.current).toBeNull();
  });

  it("execution と definition があるとき GraphData を返す", () => {
    // Arrange
    const exec = execution([], "hello");
    const def = getGraphDefinition("hello");

    // Act
    const { result } = renderHook(() => useGraphData(exec, def));

    // Assert
    expect(result.current).not.toBeNull();
    expect(result.current?.graphId).toBe("hello");
    expect(result.current?.definitionBased).toBe(true);
    expect(result.current?.mergedNodes.length).toBeGreaterThan(0);
    expect(result.current?.nodes.length).toBe(result.current?.mergedNodes.length);
    expect(result.current?.edges.length).toBeGreaterThan(0);
    expect(result.current?.groups).toBeDefined();
  });

  it("definition が null のとき definitionBased は false", () => {
    // Arrange
    const exec = execution([
      { nodeId: "n-1", nodeType: "TASK", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false }
    ]);

    // Act
    const { result } = renderHook(() => useGraphData(exec, null));

    // Assert
    expect(result.current).not.toBeNull();
    expect(result.current?.definitionBased).toBe(false);
    expect(result.current?.mergedNodes).toHaveLength(1);
    expect(result.current?.edges).toHaveLength(0);
  });

  it("状態だけが変わった再計算では dagre を呼ばない", () => {
    // Arrange
    vi.mocked(layoutGraph).mockClear();
    const idle = execution([
      { nodeId: "n-1", nodeName: "n-1", nodeType: "Task", status: "RUNNING", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false }
    ]);
    const { result, rerender } = renderHook(({ current }) => useGraphData(current, null), { initialProps: { current: idle } });
    const firstX = result.current?.nodes[0]?.x;
    vi.mocked(layoutGraph).mockClear();

    // Act
    rerender({
      current: execution([
        { nodeId: "n-1", nodeName: "n-1", nodeType: "Task", status: "SUCCEEDED", attempt: 2, workerId: null, waitKey: null, canceledByExecution: false }
      ])
    });

    // Assert
    expect(layoutGraph).not.toHaveBeenCalled();
    expect(result.current?.nodes[0]?.status).toBe("SUCCEEDED");
    expect(result.current?.nodes[0]?.x).toBe(firstX);
  });

  it("WAITING の集合が変わると dagre を呼び直す", () => {
    // Arrange
    const running = execution([
      { nodeId: "n-1", nodeName: "n-1", nodeType: "Wait", status: "RUNNING", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false }
    ]);
    const { rerender } = renderHook(({ current }) => useGraphData(current, null), { initialProps: { current: running } });
    vi.mocked(layoutGraph).mockClear();

    // Act
    rerender({
      current: execution([
        { nodeId: "n-1", nodeName: "n-1", nodeType: "Wait", status: "WAITING", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false }
      ])
    });

    // Assert
    expect(layoutGraph).toHaveBeenCalledTimes(1);
  });

  it("保存座標があるとき dagre を呼ばない", () => {
    // Arrange
    vi.mocked(layoutGraph).mockClear();
    const exec = execution([
      { nodeId: "rt-1", nodeName: "a", nodeType: "Task", status: "RUNNING", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false }
    ], "saved");
    const def: GraphDefinition = {
      graphId: "saved",
      nodes: [{ nodeName: "a", nodeType: "Task" }],
      edges: [],
      meta: { layout: { a: { x: 12, y: 34 } } }
    };

    // Act
    const { result } = renderHook(() => useGraphData(exec, def));

    // Assert
    expect(layoutGraph).not.toHaveBeenCalled();
    expect(result.current?.nodes[0]?.x).toBe(12);
    expect(result.current?.nodes[0]?.y).toBe(34);
  });
});

describe("getNodeWithFallback", () => {
  it("定義名で選択したとき同名の完了 Wait より WAITING を返す", () => {
    // Arrange
    const def: GraphDefinition = {
      graphId: "cyclic-wait",
      nodes: [{ nodeName: "cycle.decide", nodeType: "Wait" }],
      edges: []
    };
    const exec = execution(
      [
        {
          nodeId: "decide-old",
          nodeName: "cycle.decide",
          nodeType: "Wait",
          status: "SUCCEEDED",
          attempt: 1,
          workerId: null,
          waitKey: null,
          allowedEvents: ["Again", "Finish"],
          canceledByExecution: false
        },
        {
          nodeId: "decide-new",
          nodeName: "cycle.decide",
          nodeType: "Wait",
          status: "WAITING",
          attempt: 2,
          workerId: null,
          waitKey: null,
          allowedEvents: ["Again", "Finish"],
          canceledByExecution: false
        }
      ],
      "cyclic-wait"
    );
    const { result } = renderHook(() => useGraphData(exec, def));

    // Act
    const resolved = getNodeWithFallback(exec, result.current, "cycle.decide");

    // Assert
    expect(resolved?.nodeId).toBe("decide-new");
    expect(resolved?.status).toBe("WAITING");
  });
});

describe("expandWaitingNodeLayout", () => {
  it("WAITING の下にあるノードだけを、枠の不足分だけ下げる", () => {
    // Arrange
    const nodes = [
      { name: "wait", status: "WAITING", y: 0, h: 150 },
      { name: "next", status: "IDLE", y: 240, h: 120 },
      { name: "beside", status: "IDLE", y: 0, h: 120 }
    ];

    // Act
    const expanded = expandWaitingNodeLayout(nodes);

    // Assert
    const extra = WAITING_RESUME_LAYOUT_HEIGHT - 150;
    expect(expanded.find((node) => node.name === "wait")).toMatchObject({ y: 0, h: WAITING_RESUME_LAYOUT_HEIGHT });
    expect(expanded.find((node) => node.name === "next")).toMatchObject({ y: 240 + extra, h: 120 });
    expect(expanded.find((node) => node.name === "beside")).toMatchObject({ y: 0, h: 120 });
  });
});
