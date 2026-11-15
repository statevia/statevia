import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { NodeGraphView } from "@/features/executions/ui/NodeGraphView";
import type { PositionedNode } from "@/shared/lib/graphLayout";
import type { MergedGraphNode } from "@/features/executions/lib/mergeGraph";
import { renderWithUiText } from "@/tests/testUtils";
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
    const onSendToNode = vi.fn();

    renderWithUiText(
      <NodeGraphView
        nodes={[positionedNode()]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={onSelectNode}
        onSendToNode={onSendToNode}
        getResumeDisabledReason={() => null}
        showSendAction
        heightClassName="h-[320px]"
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Task 1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Task 1"));
    expect(onSelectNode).toHaveBeenCalledWith("n-1");
  });

  it("待機中のノードは「このノードへ送る」を出し、ノード高さはレイアウト枠で固定しない", async () => {
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
        onSendToNode={vi.fn()}
        getResumeDisabledReason={() => null}
        showSendAction
        heightClassName="h-[320px]"
      />
    );

    // Assert
    const hint = await screen.findByText(uiText.executionDashboard.sendEvent.sendToNode);
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
        onSendToNode={vi.fn()}
        getResumeDisabledReason={() => null}
        showSendAction
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

  it("showSendAction が false なら待機中のノードに送る操作と理由を出さない", async () => {
    // Arrange
    const waiting = positionedNode({
      nodeType: "Wait",
      status: "WAITING",
      label: "Wait 1",
      allowedEvents: ["approved"],
    });

    // Act
    renderWithUiText(
      <NodeGraphView
        nodes={[waiting]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={vi.fn()}
        onSendToNode={vi.fn()}
        getResumeDisabledReason={() => "理由"}
        showSendAction={false}
        heightClassName="h-[320px]"
      />
    );

    // Assert
    expect(await screen.findByText("Wait 1")).toBeInTheDocument();
    expect(screen.queryByText(uiText.executionDashboard.sendEvent.sendToNode)).not.toBeInTheDocument();
    expect(screen.queryByText("理由")).not.toBeInTheDocument();
  });

  it("「このノードへ送る」を押すと実行ノード ID を渡し、ノードの選択はしない", async () => {
    // Arrange
    const onSendToNode = vi.fn();
    const onSelectNode = vi.fn();
    const waiting = positionedNode({ nodeType: "Wait", status: "WAITING", label: "Wait 1", allowedEvents: ["approved"] });
    renderWithUiText(
      <NodeGraphView
        nodes={[waiting]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={onSelectNode}
        onSendToNode={onSendToNode}
        getResumeDisabledReason={() => null}
        showSendAction
        heightClassName="h-[320px]"
      />
    );

    // Act
    fireEvent.click(await screen.findByText(uiText.executionDashboard.sendEvent.sendToNode));

    // Assert
    expect(onSendToNode).toHaveBeenCalledWith(waiting.nodeId);
    expect(onSelectNode).not.toHaveBeenCalled();
  });
});
