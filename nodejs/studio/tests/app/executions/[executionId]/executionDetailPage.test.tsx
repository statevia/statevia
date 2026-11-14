import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import ExecutionDetailPage from "@/app/executions/[executionId]/page";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("next/navigation", () => ({
  useParams: () => ({ executionId: "ex-1" }),
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("@/features/executions/hooks/useExecution", () => ({
  useExecution: vi.fn()
}));

vi.mock("@/features/executions/hooks/useExecutionEvents", () => ({
  useExecutionEvents: () => ({ events: [], loading: false, error: null, loadMore: vi.fn(), hasMore: false })
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
  useGraphDefinition: () => ({ definition: null, loading: false, error: null })
}));

vi.mock("@/features/executions/hooks/useGraphData", () => ({
  useGraphData: () => ({ nodes: [], edges: [] }),
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

describe("ExecutionDetailPage", () => {
  beforeEach(() => {
    vi.mocked(useExecution).mockReturnValue({
      execution: {
        displayId: "ex-1",
        resourceId: "wf-1",
        graphId: "g-1",
        status: "ACTIVE",
        startedAt: "2026-01-01T00:00:00Z",
        cancelRequested: false,
        restartLost: false,
        waiting: false,
        nodes: []
      },
      loading: false,
      canCancel: false,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });
  });

  it("実行 ID 付きで実行ダッシュボードを描画する", async () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />);

    // Assert
    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1, name: "ex-1" })).toBeInTheDocument();
    });
  });

  it("見出しは実行 ID で、パンくずから実行一覧へ戻れる", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />);

    // Assert
    expect(screen.getByRole("heading", { level: 1, name: "ex-1" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "操作" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "実行一覧" })).toHaveAttribute("href", "/executions");
  });

  it("executions.write があればキャンセルボタンを出す", async () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />);

    // Assert
    expect(await screen.findByRole("button", { name: "キャンセル" })).toBeInTheDocument();
  });

  it("executions.write が無ければ操作も権限が無いことの案内も出さない", async () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />, { permissions: ["executions.read"] });

    // Assert
    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1, name: "ex-1" })).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: "キャンセル" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "イベントを送信" })).not.toBeInTheDocument();
    expect(screen.queryByText(/権限が必要です/)).not.toBeInTheDocument();
  });
});
