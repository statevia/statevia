import { describe, expect, it } from "vitest";
import {
  buildNodeListGroups,
  nodeListPageForNode,
  pageNodeListGroups
} from "../../features/executions/lib/nodeListGroups";
import type { ExecutionNodeDTO } from "@/features/executions/types";

/**
 * 一覧グループのテスト用ノードを作る。
 * @param overrides 上書き項目。
 * @returns 実行ノード。
 */
function node(
  overrides: Partial<ExecutionNodeDTO> & Pick<ExecutionNodeDTO, "nodeId" | "status">
): ExecutionNodeDTO {
  return {
    nodeName: "cycle.decide",
    nodeType: "Wait",
    attempt: 1,
    workerId: null,
    waitKey: null,
    canceledByExecution: false,
    ...overrides
  };
}

describe("buildNodeListGroups", () => {
  it("同じ状態名の訪問を 1 行にまとめ、代表は待機中を優先する", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "decide-1", status: "SUCCEEDED", attempt: 1 }),
      node({ nodeId: "start-1", nodeName: "cycle.start", nodeType: "Start", status: "SUCCEEDED", attempt: 1 }),
      node({ nodeId: "decide-2", status: "WAITING", attempt: 2 })
    ];

    // Act
    const groups = buildNodeListGroups(nodes);

    // Assert
    const decide = groups.find((group) => group.nodeName === "cycle.decide");
    expect(decide?.representative.nodeId).toBe("decide-2");
    expect(decide?.visits.map((visit) => visit.nodeId)).toEqual(["decide-1", "decide-2"]);
    expect(groups.map((group) => group.nodeName)).toEqual(["cycle.decide", "cycle.start"]);
  });

  it("名前の無いノードは状態名でまとめない", () => {
    // Arrange
    const nodes = [node({ nodeId: "bare", nodeName: "  ", status: "RUNNING" })];

    // Act
    const groups = buildNodeListGroups(nodes);

    // Assert
    expect(groups).toHaveLength(1);
    expect(groups[0]?.key).toBe("id:bare");
    expect(groups[0]?.nodeName).toBe("");
  });
});

describe("pageNodeListGroups", () => {
  it("状態行をページサイズで切り、範囲外のページは最後のページへ寄せる", () => {
    // Arrange
    const groups = buildNodeListGroups([
      node({ nodeId: "a", nodeName: "a", status: "SUCCEEDED" }),
      node({ nodeId: "b", nodeName: "b", status: "SUCCEEDED" }),
      node({ nodeId: "c", nodeName: "c", status: "SUCCEEDED" })
    ]);

    // Act
    const page = pageNodeListGroups(groups, 9, 2);

    // Assert
    expect(page.pageCount).toBe(2);
    expect(page.pageIndex).toBe(2);
    expect(page.groups.map((group) => group.nodeName)).toEqual(["c"]);
    expect(nodeListPageForNode(groups, "b", 2)).toBe(1);
  });
});
