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
  useNodeCommands: () => ({ resumeNode: vi.fn(), cancelNode: vi.fn(), publishEvent: vi.fn() }),
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
        nodes: []
      },
      loading: false,
      canCancel: false,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution: vi.fn(),
      publishEvent: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });
  });

  it("実行 ID 付きで実行ダッシュボードを描画する", async () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText("ex-1")).toBeInTheDocument();
    });
  });

  it("見出しは日本語の画面で「実行詳細」である", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />);

    // Assert
    expect(screen.getByRole("heading", { level: 1, name: "実行詳細" })).toBeInTheDocument();
  });

  it("見出しは英語の画面で「Execution detail」である", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />, { locale: "en" });

    // Assert
    expect(screen.getByRole("heading", { level: 1, name: "Execution detail" })).toBeInTheDocument();
  });

  it("詳細と操作を行き来するナビを出さない", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionDetailPage />);

    // Assert
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
      expect(screen.getByText("ex-1")).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: "キャンセル" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "イベント送信" })).not.toBeInTheDocument();
    expect(screen.queryByText(/権限が必要です/)).not.toBeInTheDocument();
  });
});
