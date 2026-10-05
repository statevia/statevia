import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useExecutionDashboard } from "../../../features/executions/hooks/useExecutionDashboard";
import type { ExecutionDTO, ExecutionGraphDTO } from "@/features/executions/types";
import * as api from "@/shared/api";

/**
 * jsdom に EventSource がないため、ストリーム購読を空にする。
 */
class MinimalEventSource {
  close = vi.fn();
  addEventListener = vi.fn();
  constructor(public readonly url: string) {}
}

/**
 * 循環の 2 訪問を持つ実行グラフを作る。
 * @param includeOld 完了した 1 回目を含めるか。
 * @returns グラフ DTO。
 */
function decideGraph(includeOld: boolean): ExecutionGraphDTO {
  const latest = {
    nodeId: "latest",
    nodeName: "cycle.decide",
    nodeType: "Wait",
    attempt: 2,
    completedAt: null,
    allowedEvents: ["Again"]
  };
  const nodes = includeOld
    ? [
        {
          nodeId: "old",
          nodeName: "cycle.decide",
          nodeType: "Wait",
          attempt: 1,
          completedAt: "2026-01-01T00:00:01Z",
          allowedEvents: ["Again"]
        },
        latest
      ]
    : [latest];
  return { nodes, edges: [] };
}

const executionDto = (): ExecutionDTO => ({
  displayId: "ex-1",
  resourceId: "r-1",
  graphId: "g-1",
  status: "Running",
  startedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false
});

describe("useExecutionDashboard の訪問選択", () => {
  let graph = decideGraph(true);

  beforeEach(() => {
    graph = decideGraph(true);
    vi.stubGlobal("EventSource", MinimalEventSource);
    vi.spyOn(api, "apiGet").mockImplementation(async (path: string) => {
      if (path.includes("/events")) return { events: [], hasMore: false };
      if (path.startsWith("/graphs/")) {
        return {
          graphId: "g-1",
          nodes: [{ nodeName: "cycle.decide", nodeType: "Wait" }],
          edges: []
        };
      }
      if (path.endsWith("/graph")) return graph;
      return executionDto();
    });
    vi.spyOn(api, "apiPost").mockResolvedValue({
      executionId: "ex-1",
      command: "resume",
      accepted: true,
      idempotencyKey: "k-1"
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  /**
   * 実行を読み、待機中の最新訪問が選ばれるまで待つ。
   * @param result フック結果。
   */
  async function loadUntilLatest(
    result: { current: ReturnType<typeof useExecutionDashboard> }
  ): Promise<void> {
    act(() => {
      result.current.onLoadExecution();
    });
    await waitFor(() => {
      expect(result.current.selectedNodeId).toBe("latest");
    });
  }

  it("一覧で選んだ訪問から最新と最初へ移動できる", async () => {
    // Arrange
    const { result } = renderHook(() => useExecutionDashboard({ initialExecutionId: "ex-1" }));
    await loadUntilLatest(result);

    // Act
    act(() => {
      result.current.onSelectListedNode("old");
    });

    // Assert
    expect(result.current.selectedNodeId).toBe("old");
    act(() => {
      result.current.nodeVisitNavigation.onJumpToLatest();
    });
    expect(result.current.selectedNodeId).toBe("latest");
    act(() => {
      result.current.nodeVisitNavigation.onStepOlder();
      result.current.nodeVisitNavigation.onJumpToFirst();
    });
    expect(result.current.selectedNodeId).toBe("old");
    act(() => {
      result.current.nodeVisitNavigation.onStepNewer();
    });
    expect(result.current.selectedNodeId).toBe("latest");
  });

  it("実行が無いときは渡した選択キーをそのまま保持する", () => {
    // Arrange
    const { result } = renderHook(() => useExecutionDashboard({ initialExecutionId: "ex-1" }));

    // Act
    act(() => {
      result.current.onSelectNode("cycle.decide");
    });

    // Assert
    expect(result.current.selectedNodeId).toBe("cycle.decide");
  });

  it("キャンバスの状態名は最新訪問になり、解除すると未選択になる", async () => {
    // Arrange
    const { result } = renderHook(() => useExecutionDashboard({ initialExecutionId: "ex-1" }));
    await loadUntilLatest(result);

    // Act
    act(() => {
      result.current.onSelectNode("old");
    });

    // Assert
    expect(result.current.selectedNodeId).toBe("latest");
    act(() => {
      result.current.onSelectNode(null);
    });
    expect(result.current.selectedNodeId).toBeNull();
  });

  it("Resume は選択中の訪問を送り、残っていない ID は送らない", async () => {
    // Arrange
    const { result } = renderHook(() => useExecutionDashboard({ initialExecutionId: "ex-1" }));
    act(() => {
      result.current.onResumeNode("latest", "Again");
      result.current.onResumeSelectedNode("Again");
    });
    await loadUntilLatest(result);

    // Act
    act(() => {
      result.current.onResumeNode("missing", "Again");
      result.current.onResumeSelectedNode("Again");
    });

    // Assert
    await waitFor(() => {
      expect(vi.mocked(api.apiPost)).toHaveBeenCalledWith(
        "/executions/ex-1/nodes/latest/resume",
        { resumeKey: "Again" }
      );
    });
    expect(vi.mocked(api.apiPost).mock.calls.some((call) => String(call[0]).includes("missing"))).toBe(false);
  });

  it("消えた訪問は同じ状態の最新へ移す", async () => {
    // Arrange
    const { result } = renderHook(() => useExecutionDashboard({ initialExecutionId: "ex-1" }));
    await loadUntilLatest(result);
    act(() => {
      result.current.onSelectListedNode("old");
    });
    expect(result.current.selectedNodeId).toBe("old");
    graph = decideGraph(false);

    // Act
    act(() => {
      result.current.onLoadExecution();
    });

    // Assert
    await waitFor(() => {
      expect(result.current.selectedNodeId).toBe("latest");
    });
  });
});
