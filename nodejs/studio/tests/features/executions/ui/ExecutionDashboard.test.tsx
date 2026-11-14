import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { ExecutionDashboard } from "@/features/executions/ui/ExecutionDashboard";
import { renderWithUiText } from "@/tests/testUtils";
import type { ExecutionView } from "@/features/executions/types";
import { apiGet } from "@/shared/api";

const executionViewFixture = (): ExecutionView => ({
  displayId: "ex-1",
  resourceId: "wf-1",
  graphId: "g-1",
  status: "ACTIVE",
  startedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false,
  waiting: false,
  nodes: []
});

vi.mock("@/features/executions/hooks/useExecution", () => ({
  useExecution: vi.fn()
}));

vi.mock("@/features/executions/hooks/useExecutionEvents", () => ({
  useExecutionEvents: () => ({
    events: [],
    loading: false,
    error: null,
    loadMore: vi.fn(),
    hasMore: false
  })
}));

vi.mock("@/features/executions/hooks/useExecutionStateAtSeq", () => ({
  useExecutionStateAtSeq: () => ({
    replayExecution: null,
    replayLoading: false,
    replayError: null,
    clearReplay: vi.fn(),
    loadStateAtSeq: vi.fn()
  })
}));

vi.mock("@/features/executions/hooks/useGraphDefinition", () => ({
  useGraphDefinition: () => ({
    definition: null,
    loading: false,
    error: null
  })
}));

const useGraphDataMock = vi.fn();

vi.mock("@/features/executions/hooks/useGraphData", () => ({
  useGraphData: (...args: unknown[]) => useGraphDataMock(...args),
  getNodeWithFallback: vi.fn()
}));

vi.mock("@/features/executions/hooks/useNodeCommands", () => ({
  getResumeDisabledReason: () => null
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { useExecution } from "@/features/executions/hooks/useExecution";

describe("ExecutionDashboard", () => {
  beforeEach(() => {
    useGraphDataMock.mockReturnValue({
      nodes: [
        {
          name: "n-1",
          nodeId: "n-1",
          nodeName: "task",
          nodeType: "Task",
          label: "Task node",
          status: "RUNNING",
          attempt: 1,
          workerId: null,
          waitKey: null,
          canceledByExecution: false,
          x: 0,
          y: 0,
          w: 200,
          h: 80
        }
      ],
      edges: [],
      groups: [],
      mergedNodes: [],
      graphId: "g-1",
      definitionBased: false
    });
    vi.mocked(useExecution).mockReturnValue({
      execution: executionViewFixture(),
      loading: false,
      canCancel: true,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });
  });

  it("実行 ID を見出しにし、パンくずから実行一覧へ戻れる", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    // Assert
    expect(screen.getByRole("heading", { level: 1, name: "ex-1" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "実行一覧" })).toHaveAttribute("href", "/executions");
  });

  it("待機があるときだけ「イベントを送信」を出し、押すとダイアログを開く", async () => {
    // Arrange
    vi.mocked(useExecution).mockReturnValue({
      execution: {
        ...executionViewFixture(),
        nodes: [
          {
            nodeId: "w-1",
            nodeName: "approve",
            nodeType: "Wait",
            status: "WAITING",
            attempt: 1,
            workerId: null,
            waitKey: null,
            allowedEvents: ["approval.granted"],
            canceledByExecution: false
          }
        ]
      },
      loading: false,
      canCancel: true,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "イベントを送信" }));

    // Assert
    expect(screen.getByText("approve でイベントを待っています")).toBeInTheDocument();
    expect(await screen.findByRole("dialog", { name: "イベントを送信" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /approve に approval.granted を届ける/ })).toBeChecked();
  });

  it("待機が無ければ「イベントを送信」を出さない", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    // Assert
    expect(screen.queryByRole("button", { name: "イベントを送信" })).not.toBeInTheDocument();
  });

  it("Cancel ボタンで cancelExecution を呼ぶ", async () => {
    const cancelExecution = vi.fn();
    vi.mocked(useExecution).mockReturnValue({
      execution: executionViewFixture(),
      loading: false,
      canCancel: true,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution,
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });

    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    fireEvent.click(await screen.findByRole("button", { name: /キャンセル/i }));
    expect(cancelExecution).toHaveBeenCalled();
  });

  it("グラフ表示に切り替えられる", async () => {
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    fireEvent.click(screen.getByRole("button", { name: "グラフ" }));

    await waitFor(() => {
      expect(screen.getByText("Task node")).toBeInTheDocument();
    });
  });

  it("全画面と比較対象の読み込みを操作できる", async () => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path.endsWith("/graph")) {
        throw new Error("no graph");
      }
      return {
        displayId: "ex-2",
        resourceId: "res-2",
        graphId: "g-2",
        status: "COMPLETED",
        startedAt: "2026-01-01T00:00:00Z",
        cancelRequested: false,
        restartLost: false,
        waiting: false,
        nodes: []
      };
    });
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    fireEvent.click(screen.getByRole("button", { name: "グラフ" }));
    fireEvent.click(await screen.findByRole("button", { name: "全画面表示" }));
    fireEvent.keyDown(window, { key: "Escape" });
    fireEvent.click(screen.getByRole("button", { name: "比較" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "リアルタイム更新" }));
    fireEvent.change(screen.getByLabelText(/B$/), { target: { value: "ex-2" } });
    const loadButtons = screen.getAllByRole("button", { name: "読み込む" });
    fireEvent.click(loadButtons[loadButtons.length - 1]);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/executions/ex-2");
    });
  });
});
