import { describe, expect, it } from "vitest";
import {
  buildNodeVisitNavigation,
  canvasNodeShowsSelection,
  listNodeVisits,
  resolveInitialSelectionNodeId,
  resolveResumeTargetNodeId,
  resolveVisitAfterRefresh
} from "../../features/executions/lib/nodeVisits";
import type { ExecutionNodeDTO } from "@/features/executions/types";

/**
 * 訪問ナビのテスト用ノードを作る。
 * @param overrides 上書き項目。
 * @returns 実行ノード。
 */
function node(
  overrides: Partial<ExecutionNodeDTO> & Pick<ExecutionNodeDTO, "nodeId" | "attempt">
): ExecutionNodeDTO {
  return {
    nodeName: "cycle.decide",
    nodeType: "Wait",
    status: "SUCCEEDED",
    workerId: null,
    waitKey: null,
    canceledByExecution: false,
    ...overrides
  };
}

describe("listNodeVisits", () => {
  it("状態名が空、または一致が無いときは空配列を返す", () => {
    // Arrange
    const nodes = [node({ nodeId: "a", attempt: 1 })];

    // Act
    const blank = listNodeVisits(nodes, "  ");
    const missing = listNodeVisits(nodes, "other");

    // Assert
    expect(blank).toEqual([]);
    expect(missing).toEqual([]);
  });

  it("1 件のときはその訪問だけを返す", () => {
    // Arrange
    const only = node({ nodeId: "only", attempt: 1 });

    // Act
    const visits = listNodeVisits([only, node({ nodeId: "other", nodeName: "cycle.end", attempt: 1 })], "cycle.decide");

    // Assert
    expect(visits.map((visit) => visit.nodeId)).toEqual(["only"]);
  });

  it("attempt 昇順にし、同じ attempt は配列の後ろを新しい側にする", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "second-early", attempt: 2 }),
      node({ nodeId: "first", attempt: 1 }),
      node({ nodeId: "second-late", attempt: 2 })
    ];

    // Act
    const visits = listNodeVisits(nodes, "Cycle.Decide");

    // Assert
    expect(visits.map((visit) => visit.nodeId)).toEqual(["first", "second-early", "second-late"]);
  });
});

describe("resolveInitialSelectionNodeId", () => {
  it("状態名でも実行ノード ID でも最新訪問を返す", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "old", attempt: 1 }),
      node({ nodeId: "new", attempt: 2, status: "WAITING" })
    ];

    // Act
    const byName = resolveInitialSelectionNodeId(nodes, "cycle.decide");
    const byId = resolveInitialSelectionNodeId(nodes, "old");

    // Assert
    expect(byName).toBe("new");
    expect(byId).toBe("new");
  });
});

describe("resolveVisitAfterRefresh", () => {
  it("選択中 ID が残っているときは WAITING があっても維持する", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "old", attempt: 1 }),
      node({ nodeId: "new", attempt: 2, status: "WAITING" })
    ];

    // Act
    const next = resolveVisitAfterRefresh(nodes, "old", "cycle.decide");

    // Assert
    expect(next).toEqual({ nodeId: "old", nodeName: "cycle.decide" });
  });

  it("選択中 ID が消えたときは同じ状態の最新へ移す", () => {
    // Arrange
    const nodes = [node({ nodeId: "new", attempt: 3, status: "WAITING" })];

    // Act
    const next = resolveVisitAfterRefresh(nodes, "gone", "cycle.decide");

    // Assert
    expect(next.nodeId).toBe("new");
  });

  it("同じ状態の訪問が残っていなければ選択を外す", () => {
    // Arrange
    const nodes = [node({ nodeId: "end", nodeName: "cycle.end", attempt: 1 })];

    // Act
    const next = resolveVisitAfterRefresh(nodes, "gone", "cycle.decide");

    // Assert
    expect(next).toEqual({ nodeId: null, nodeName: null });
  });
});

describe("resolveResumeTargetNodeId", () => {
  it("選択中の訪問が同じ状態ならその ID を返す", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "old", attempt: 1 }),
      node({ nodeId: "new", attempt: 2, status: "WAITING" })
    ];

    // Act
    const target = resolveResumeTargetNodeId(nodes, "new", "old");

    // Assert
    expect(target).toBe("old");
  });

  it("対象の訪問が残っていなければ null を返す", () => {
    // Arrange
    const nodes = [node({ nodeId: "end", nodeName: "cycle.end", attempt: 1 })];

    // Act
    const target = resolveResumeTargetNodeId(nodes, "gone", "gone");

    // Assert
    expect(target).toBeNull();
  });
});

describe("buildNodeVisitNavigation", () => {
  it("訪問が 1 件のときはナビを出さない", () => {
    // Arrange
    const visits = [node({ nodeId: "only", attempt: 1 })];

    // Act
    const navigation = buildNodeVisitNavigation(visits, "only");

    // Assert
    expect(navigation.visible).toBe(false);
  });

  it("最新では左の移動を止め、1 回目では右の移動を止める", () => {
    // Arrange
    const visits = [
      node({ nodeId: "first", attempt: 1 }),
      node({ nodeId: "middle", attempt: 2 }),
      node({ nodeId: "latest", attempt: 3, status: "WAITING" })
    ];

    // Act
    const atLatest = buildNodeVisitNavigation(visits, "latest");
    const atFirst = buildNodeVisitNavigation(visits, "first");
    const atMiddle = buildNodeVisitNavigation(visits, "middle");

    // Assert
    expect(atLatest.canJumpToLatest).toBe(false);
    expect(atLatest.canStepNewer).toBe(false);
    expect(atLatest.canStepOlder).toBe(true);
    expect(atLatest.canJumpToFirst).toBe(true);
    expect(atFirst.canJumpToFirst).toBe(false);
    expect(atFirst.canStepOlder).toBe(false);
    expect(atFirst.canStepNewer).toBe(true);
    expect(atMiddle.newerNodeId).toBe("latest");
    expect(atMiddle.olderNodeId).toBe("first");
    expect(atMiddle.attempt).toBe(2);
  });
});

describe("canvasNodeShowsSelection", () => {
  it("訪問が違っても同じ状態のキャンバスノードを選択表示する", () => {
    // Arrange
    const canvas = { name: "cycle.decide", nodeName: "cycle.decide", nodeId: "representative" };

    // Act
    const shown = canvasNodeShowsSelection(canvas, "older-visit", "cycle.decide");
    const hidden = canvasNodeShowsSelection(canvas, "other", "cycle.end");

    // Assert
    expect(shown).toBe(true);
    expect(hidden).toBe(false);
  });
});
