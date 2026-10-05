import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { NodeListView } from "../../../features/executions/ui/NodeListView";
import { renderWithUiText } from "../../testUtils";
import type { ExecutionNodeDTO } from "@/features/executions/types";

const node = (id: string, status: ExecutionNodeDTO["status"]): ExecutionNodeDTO => ({
  nodeId: id,
  nodeType: "Task",
  status,
  attempt: 1,
  workerId: null,
  waitKey: null,
  canceledByExecution: false,
  nodeName: `state-${id}`
});

describe("NodeListView", () => {
  it("ノード行クリックで選択コールバックを呼ぶ", () => {
    const onSelectNode = vi.fn();
    renderWithUiText(
      <NodeListView nodes={[node("n-1", "RUNNING")]} selectedNodeId={null} onSelectNode={onSelectNode} />
    );

    fireEvent.click(screen.getByText("n-1"));
    expect(onSelectNode).toHaveBeenCalledWith("n-1");
  });

  it("同じ状態の訪問は最初は閉じ、左端の印で開閉でき、行名は状態名と回数になる", () => {
    // Arrange
    const onSelectNode = vi.fn();
    const nodes: ExecutionNodeDTO[] = [
      { ...node("old", "SUCCEEDED"), nodeName: "cycle.work.a", attempt: 1 },
      { ...node("latest", "WAITING"), nodeName: "cycle.work.a", nodeType: "Wait", attempt: 2 }
    ];
    renderWithUiText(
      <NodeListView nodes={nodes} selectedNodeId="latest" onSelectNode={onSelectNode} />
    );

    // Act
    expect(screen.queryByText("cycle.work.a (1)")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "cycle.work.a の訪問を展開" }));

    // Assert
    expect(screen.getByText("cycle.work.a (1)")).toBeInTheDocument();
    fireEvent.click(screen.getByText("cycle.work.a (1)"));
    expect(onSelectNode).toHaveBeenCalledWith("old");
    fireEvent.click(screen.getByRole("button", { name: "cycle.work.a の訪問を畳む" }));
    expect(screen.queryByText("cycle.work.a (1)")).not.toBeInTheDocument();
  });
});
