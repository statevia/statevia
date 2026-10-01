import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { NodeGraphView } from "../../../features/executions/ui/NodeGraphView";
import type { PositionedNode } from "@/shared/lib/graphLayout";
import type { MergedGraphNode } from "../../../features/executions/lib/mergeGraph";
import { renderWithUiText } from "../../testUtils";
import { uiText } from "@/shared/i18n/uiText";

const positionedNode = (overrides: Partial<PositionedNode<MergedGraphNode>> = {}): PositionedNode<MergedGraphNode> => ({
  name: "n-1",
  nodeId: "n-1",
  nodeName: "task",
  nodeType: "Task",
  label: "Task 1",
  status: "RUNNING",
  attempt: 1,
  workerId: null,
  waitKey: null,
  canceledByExecution: false,
  x: 0,
  y: 0,
  w: 200,
  h: 80,
  ...overrides
});

describe("NodeGraphView", () => {
  it("ノードを描画しクリックで選択できる", async () => {
    const onSelectNode = vi.fn();
    const onResumeNode = vi.fn();

    renderWithUiText(
      <NodeGraphView
        nodes={[positionedNode()]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={onSelectNode}
        onResumeNode={onResumeNode}
        getResumeDisabledReason={() => null}
        heightClassName="h-[320px]"
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Task 1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Task 1"));
    expect(onSelectNode).toHaveBeenCalledWith("n-1");
  });

  it("WAITING の説明は改行し、ノード高さはレイアウト枠で固定しない", async () => {
    // Arrange
    const waiting = positionedNode({
      nodeType: "Wait",
      status: "WAITING",
      label: "Wait 1",
      allowedEvents: ["statevia.event.subscribe.0"],
      h: 150,
      w: 240
    });

    // Act
    renderWithUiText(
      <NodeGraphView
        nodes={[waiting]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={vi.fn()}
        onResumeNode={vi.fn()}
        getResumeDisabledReason={() => null}
        heightClassName="h-[320px]"
      />
    );

    // Assert
    const hint = await screen.findByText(
      (_, element) => element?.tagName === "P" && element.textContent === uiText.nodeDetail.waiting.subscribeOnlyHint
    );
    expect(hint).toHaveClass("whitespace-pre-line");
    const flowNode = hint.closest(".react-flow__node");
    expect(flowNode).toBeInstanceOf(HTMLElement);
    if (!(flowNode instanceof HTMLElement)) return;
    expect(flowNode.style.height).not.toBe("150px");
    const card = hint.closest(".rounded-xl");
    expect(card?.parentElement?.className ?? "").not.toContain("h-full");
  });

  it("完了した Wait はレイアウト枠の高さで出辺の起点を固定しない", async () => {
    // Arrange
    const completedWait = positionedNode({
      nodeType: "Wait",
      status: "SUCCEEDED",
      label: "flow.wait.paid",
      h: 150,
      w: 240
    });

    // Act
    renderWithUiText(
      <NodeGraphView
        nodes={[completedWait]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={vi.fn()}
        onResumeNode={vi.fn()}
        getResumeDisabledReason={() => null}
        heightClassName="h-[320px]"
      />
    );

    // Assert
    const label = await screen.findByText("flow.wait.paid");
    const card = label.closest(".rounded-xl");
    expect(card?.parentElement?.className ?? "").not.toContain("h-full");
    const flowNode = label.closest(".react-flow__node");
    expect(flowNode).toBeInstanceOf(HTMLElement);
    if (!(flowNode instanceof HTMLElement)) return;
    expect(flowNode.style.height).not.toBe("150px");
  });
});
