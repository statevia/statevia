import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { NodeDetail } from "@/features/executions/ui/NodeDetail";
import { uiText } from "@/shared/i18n/uiText";
import type { ExecutionNodeDTO, ExecutionView } from "@/features/executions/types";

const baseExecution: ExecutionView = {
  displayId: "ex-1",
  resourceId: "res-1",
  status: "ACTIVE",
  startedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false,
  graphId: "g-1",
  nodes: []
};

const baseNode: ExecutionNodeDTO = {
  nodeId: "n-1",
  nodeType: "Task",
  status: "RUNNING",
  attempt: 1,
  workerId: "w-1",
  waitKey: null,
  canceledByExecution: false
};

const defaultProps = {
  execution: baseExecution,
  node: baseNode,
  loading: false,
  onSendToNode: vi.fn(),
  resumeDisabledReason: null as string | null
};

describe("NodeDetail", () => {
  it("execution が null のとき実行の読み込み案内を表示する", () => {
    render(
      <NodeDetail
        {...defaultProps}
        execution={null}
        node={baseNode}
      />
    );
    expect(screen.getByText(uiText.nodeDetail.prompts.loadExecution(uiText.entities.execution))).toBeInTheDocument();
  });

  it("node が null のときノード選択案内を表示する", () => {
    render(
      <NodeDetail
        {...defaultProps}
        execution={baseExecution}
        node={null}
      />
    );
    expect(screen.getByText(uiText.nodeDetail.prompts.selectNode(uiText.entities.node))).toBeInTheDocument();
  });

  it("node が選択されているとき詳細見出しとノード ID を表示する", () => {
    render(<NodeDetail {...defaultProps} />);
    expect(screen.getByText(uiText.nodeDetail.title(uiText.entities.node))).toBeInTheDocument();
    expect(screen.getByText(uiText.nodeDetail.meta.nodeId("n-1"))).toBeInTheDocument();
    expect(screen.getByText("RUNNING")).toBeInTheDocument();
  });

  it("startedAt・completedAt があるときトレース欄に開始・終了・実行時間を表示する", () => {
    const node: ExecutionNodeDTO = {
      ...baseNode,
      status: "SUCCEEDED",
      startedAt: "2026-01-15T10:00:00.123Z",
      completedAt: "2026-01-15T10:00:01.456Z"
    };
    render(<NodeDetail {...defaultProps} node={node} />);
    expect(screen.getByText(/開始:/)).toBeInTheDocument();
    expect(screen.getByText(/終了:/)).toBeInTheDocument();
    expect(screen.getByText(/実行時間:/)).toBeInTheDocument();
    expect(screen.getByText(/123/)).toBeInTheDocument();
    expect(screen.getByText(/456/)).toBeInTheDocument();
  });

  it("output を渡すと出力見出しと内容を表示する", () => {
    const node: ExecutionNodeDTO = { ...baseNode, output: { x: 1 } };
    render(<NodeDetail {...defaultProps} node={node} />);
    fireEvent.click(screen.getByText(uiText.nodeDetail.trace.outputHeading));
    expect(screen.getByText(uiText.nodeDetail.trace.outputHeading)).toBeInTheDocument();
    expect(screen.getByText(/"x": 1/, { exact: false })).toBeInTheDocument();
  });

  it("nodeName があるときメタ情報にノード名を表示する", () => {
    const node: ExecutionNodeDTO = { ...baseNode, nodeName: "MyState" };
    render(<NodeDetail {...defaultProps} node={node} />);
    expect(screen.getByText(uiText.nodeDetail.meta.nodeName("MyState"))).toBeInTheDocument();
  });

  it("input を渡すとトレース欄に入力見出しと内容を表示する", () => {
    const node: ExecutionNodeDTO = { ...baseNode, input: { seed: true } };
    render(<NodeDetail {...defaultProps} node={node} />);
    fireEvent.click(screen.getByText(uiText.nodeDetail.trace.inputHeading));
    expect(screen.getByText(uiText.nodeDetail.trace.inputHeading)).toBeInTheDocument();
    expect(screen.getByText(/"seed": true/, { exact: false })).toBeInTheDocument();
  });

  describe("Wait / Resume 詳細", () => {
    it("status が WAITING のとき「待機の詳細」と理由を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.waiting.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.waiting.reasonWaitByWaitKeyAndResumeWait)).toBeInTheDocument();
    });

    it("status が WAITING で resumeEventName を渡すと「再開イベント名」を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} resumeEventName="DoneC" />);
      expect(screen.getByText(uiText.nodeDetail.waiting.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.waiting.resumeEventName("DoneC"))).toBeInTheDocument();
    });

    it("status が WAITING で resumeEventName が空のときイベント名行を表示しない", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} resumeEventName="" />);
      expect(screen.getByText(uiText.nodeDetail.waiting.title)).toBeInTheDocument();
      expect(screen.queryByText(new RegExp(`^${uiText.nodeDetail.waiting.resumeEventName("")}`))).not.toBeInTheDocument();
    });

    it("status が RUNNING のとき「待機の詳細」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText(uiText.nodeDetail.waiting.title)).not.toBeInTheDocument();
    });
  });

  describe("Cancel 詳細", () => {
    it("status が CANCELED で cancelReason ありのとき「キャンセル 詳細」と reason を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "CANCELED",
        canceledByExecution: false,
        cancelReason: "user"
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel))).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.cancel.reason("user"))).toBeInTheDocument();
    });

    it("status が CANCELED で canceledByExecution が true のとき「実行のキャンセルで止まりました。」を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "CANCELED",
        canceledByExecution: true
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel))).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.cancel.convergedByExecutionCancel)).toBeInTheDocument();
    });

    it("status が RUNNING のとき「キャンセル 詳細」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText(uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel))).not.toBeInTheDocument();
    });
  });

  describe("失敗情報", () => {
    it("status が FAILED で error.message ありのとき「失敗情報」とメッセージを表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "FAILED",
        error: { message: "Something went wrong" }
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.failure.title)).toBeInTheDocument();
      expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    });

    it("status が FAILED で error.message が空のとき「（メッセージなし）」を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "FAILED",
        error: { message: "" }
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.failure.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.failure.noMessage)).toBeInTheDocument();
    });

    it("status が FAILED で error がないとき「（メッセージなし）」を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "FAILED" };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.failure.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.failure.noMessage)).toBeInTheDocument();
    });

    it("status が RUNNING のとき「失敗情報」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText(uiText.nodeDetail.failure.title)).not.toBeInTheDocument();
    });
  });

  describe("このノードへ送る", () => {
    it("待機中のノードでは「このノードへ送る」を出し、押すと onSendToNode を呼ぶ", () => {
      // Arrange
      const onSendToNode = vi.fn();
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", allowedEvents: ["approve", "reject"] };
      render(<NodeDetail {...defaultProps} node={node} onSendToNode={onSendToNode} />);

      // Act
      fireEvent.click(screen.getByRole("button", { name: uiText.executionDashboard.sendEvent.sendToNode }));

      // Assert
      expect(onSendToNode).toHaveBeenCalledTimes(1);
    });

    it("送れない理由があるときは理由を出し、ボタンを押せなくする", () => {
      // Arrange
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };

      // Act
      render(<NodeDetail {...defaultProps} node={node} resumeDisabledReason="リプレイ表示中は実行できません" />);

      // Assert
      expect(screen.getByText("リプレイ表示中は実行できません")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: uiText.executionDashboard.sendEvent.sendToNode })).toBeDisabled();
    });

    it("待機中でないノードと、送る操作を出さない利用者には出さない", () => {
      // Arrange
      const waiting: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      const { rerender } = render(<NodeDetail {...defaultProps} />);
      const runningHasButton = screen.queryByRole("button", { name: uiText.executionDashboard.sendEvent.sendToNode });

      // Act
      rerender(<NodeDetail {...defaultProps} node={waiting} showSendAction={false} />);

      // Assert
      expect(runningHasButton).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: uiText.executionDashboard.sendEvent.sendToNode })).not.toBeInTheDocument();
    });
  });

  it("複数訪問のときだけ移動ボタンを出し、押した先を呼ぶ", () => {
    // Arrange
    const onJumpToLatest = vi.fn();
    const onStepNewer = vi.fn();
    const onStepOlder = vi.fn();
    const onJumpToFirst = vi.fn();
    const navigation = {
      visible: true,
      attempt: 2,
      canJumpToLatest: false,
      canStepNewer: false,
      canStepOlder: true,
      canJumpToFirst: true,
      onJumpToLatest,
      onStepNewer,
      onStepOlder,
      onJumpToFirst
    };

    // Act
    const { rerender } = render(<NodeDetail {...defaultProps} nodeVisitNavigation={navigation} />);

    // Assert
    expect(screen.getByRole("button", { name: uiText.nodeDetail.visitNavigation.jumpToLatest })).toBeDisabled();
    expect(screen.getByRole("button", { name: uiText.nodeDetail.visitNavigation.stepNewer })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: uiText.nodeDetail.visitNavigation.stepOlder }));
    fireEvent.click(screen.getByRole("button", { name: uiText.nodeDetail.visitNavigation.jumpToFirst }));
    expect(onStepOlder).toHaveBeenCalledTimes(1);
    expect(onJumpToFirst).toHaveBeenCalledTimes(1);
    expect(screen.getAllByText(uiText.nodeDetail.meta.attempt(2))).toHaveLength(1);
    expect(screen.getAllByText(uiText.nodeDetail.meta.attempt(1))).toHaveLength(1);

    rerender(<NodeDetail {...defaultProps} />);
    expect(screen.queryByRole("button", { name: uiText.nodeDetail.visitNavigation.jumpToLatest })).not.toBeInTheDocument();
  });
});
