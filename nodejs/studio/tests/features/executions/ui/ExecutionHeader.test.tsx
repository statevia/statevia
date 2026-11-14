import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { ExecutionHeader, type ExecutionHeaderProps } from "@/features/executions/ui/ExecutionHeader";
import { renderWithUiText } from "@/tests/testUtils";
import type { ExecutionView } from "@/features/executions/types";

function executionViewFixture(overrides: Partial<ExecutionView> = {}): ExecutionView {
  return {
    displayId: "ex-1",
    resourceId: "wf-1",
    graphId: "g-1",
    status: "ACTIVE",
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    waiting: false,
    nodes: [],
    ...overrides
  };
}

/**
 * 既定の入力に、テストで見たいものだけを上書きした props を作る。
 * @param overrides 上書きする入力。
 * @returns 見出しの props。
 */
function headerProps(overrides: Partial<ExecutionHeaderProps> = {}): ExecutionHeaderProps {
  return {
    executionId: "ex-1",
    execution: executionViewFixture(),
    waiting: false,
    loading: false,
    canCancel: true,
    showCancelAction: true,
    onCancel: vi.fn(),
    showCompareAction: true,
    compareMode: false,
    onCompareModeChange: vi.fn(),
    showSendEventAction: true,
    onOpenSendEvent: vi.fn(),
    ...overrides
  };
}

describe("ExecutionHeader", () => {
  it("実行 ID を見出しにし、右にキャンセル・比較・イベントを送信の順で並べる", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionHeader {...headerProps()} />);

    // Assert
    expect(screen.getByRole("heading", { level: 1, name: "ex-1" })).toBeInTheDocument();
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual([
      "キャンセル",
      "比較",
      "イベントを送信"
    ]);
  });

  it("待機があるときは状態の横に待機中の印を出す", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionHeader {...headerProps({ waiting: true })} />);

    // Assert
    expect(screen.getByText("ACTIVE")).toBeInTheDocument();
    expect(screen.getByText("待機中")).toBeInTheDocument();
  });

  it("キャンセル要求済みは見出しの下に書く", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionHeader {...headerProps({ execution: executionViewFixture({ cancelRequested: true }) })} />);

    // Assert
    expect(screen.getByText(/キャンセル要求あり/)).toBeInTheDocument();
  });

  it("各ボタンでキャンセル・比較の切り替え・ダイアログを開くを呼ぶ", () => {
    // Arrange
    const onCancel = vi.fn();
    const onCompareModeChange = vi.fn();
    const onOpenSendEvent = vi.fn();
    renderWithUiText(<ExecutionHeader {...headerProps({ onCancel, onCompareModeChange, onOpenSendEvent })} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "キャンセル" }));
    fireEvent.click(screen.getByRole("button", { name: "比較" }));
    fireEvent.click(screen.getByRole("button", { name: "イベントを送信" }));

    // Assert
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onCompareModeChange).toHaveBeenCalledWith(true);
    expect(onOpenSendEvent).toHaveBeenCalledTimes(1);
  });

  it("出さない指定の操作は描画しない", () => {
    // Arrange / Act
    renderWithUiText(
      <ExecutionHeader {...headerProps({ showCancelAction: false, showCompareAction: false, showSendEventAction: false })} />
    );

    // Assert
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });
});
