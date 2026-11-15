import { describe, expect, it, vi } from "vitest";
import { act, fireEvent, screen } from "@testing-library/react";
import { SendEventDialog, type SendEventDialogProps } from "@/features/executions/ui/SendEventDialog";
import type { WaitTarget } from "@/features/executions/lib/waitTargets";
import { renderWithUiText } from "@/tests/testUtils";

const approve: WaitTarget = { nodeId: "n-approve", nodeName: "approve", events: ["approval.granted"], waitingSince: null };
const payment: WaitTarget = {
  nodeId: "n-payment",
  nodeName: "await-payment",
  events: ["payment.confirmed", "payment.failed"],
  waitingSince: null
};

/**
 * 既定の入力に、テストで見たいものだけを上書きした props を作る。
 * @param overrides 上書きする入力。
 * @returns ダイアログの props。
 */
function dialogProps(overrides: Partial<SendEventDialogProps> = {}): SendEventDialogProps {
  return {
    executionDisplayId: "ex-1",
    waits: [approve, payment],
    initialNodeId: null,
    sending: false,
    onClose: vi.fn(),
    onSend: vi.fn().mockResolvedValue(null),
    ...overrides
  };
}

describe("SendEventDialog", () => {
  it("待機が 2 つなら説明に数を出し、先頭の待機を選んだ状態で開き、残る待機を案内する", () => {
    // Arrange / Act
    renderWithUiText(<SendEventDialog {...dialogProps()} />);

    // Assert
    expect(screen.getByText("ex-1 は 2 つのノードでイベントを待っています。届け先を選んでください。")).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /approve に approval.granted を届ける/ })).toBeChecked();
    expect(screen.getByText("送信すると approve が進みます。await-payment は待機のまま残ります。")).toBeInTheDocument();
  });

  it("行から開いたときはそのノードを選んでおき、イベントが複数なら選んだイベントで resume を送る", async () => {
    // Arrange
    const onSend = vi.fn().mockResolvedValue(null);
    renderWithUiText(<SendEventDialog {...dialogProps({ initialNodeId: "n-payment", onSend })} />);

    // Act
    fireEvent.change(screen.getByLabelText("await-payment に届けるイベント"), { target: { value: "payment.failed" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "送信" }));
    });

    // Assert
    expect(screen.getByRole("radio", { name: /await-payment にイベントを届ける/ })).toBeChecked();
    expect(onSend).toHaveBeenCalledWith({ kind: "node", nodeId: "n-payment", eventName: "payment.failed" });
  });

  it("イベント名を指定すると実行への送信になり、不正な名前は送れずエラーを出す", async () => {
    // Arrange
    const onSend = vi.fn().mockResolvedValue(null);
    renderWithUiText(<SendEventDialog {...dialogProps({ onSend })} />);
    const input = screen.getByLabelText("イベント名");

    // Act
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "1-bad" } });
    fireEvent.blur(input);
    const disabledWhileInvalid = screen.getByRole("button", { name: "送信" }).hasAttribute("disabled");
    fireEvent.change(input, { target: { value: "payment.confirmed" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "送信" }));
    });

    // Assert
    expect(screen.getByRole("radio", { name: /イベント名を指定して実行へ送る/ })).toBeChecked();
    expect(disabledWhileInvalid).toBe(true);
    expect(onSend).toHaveBeenCalledWith({ kind: "execution", eventName: "payment.confirmed" });
  });

  it("送信に失敗したら閉じずにエラーを中に出す", async () => {
    // Arrange
    const onClose = vi.fn();
    const onSend = vi.fn().mockResolvedValue("422 処理できません: WAIT_NOT_FOUND - no wait");
    renderWithUiText(<SendEventDialog {...dialogProps({ onClose, onSend })} />);

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "送信" }));
    });

    // Assert
    expect(screen.getByRole("alert")).toHaveTextContent("WAIT_NOT_FOUND - no wait");
    expect(onClose).not.toHaveBeenCalled();
  });

  it("選んでいたノードの待機が解けて候補から消えたら送れない", () => {
    // Arrange
    const { rerender } = renderWithUiText(<SendEventDialog {...dialogProps({ initialNodeId: "n-payment" })} />);

    // Act
    rerender(<SendEventDialog {...dialogProps({ initialNodeId: "n-payment", waits: [approve] })} />);

    // Assert
    expect(screen.getByRole("button", { name: "送信" })).toBeDisabled();
  });

  it("送信中は閉じる操作を出さない", () => {
    // Arrange / Act
    renderWithUiText(<SendEventDialog {...dialogProps({ sending: true })} />);

    // Assert
    expect(screen.queryByRole("button", { name: "閉じる" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "キャンセル" })).toBeDisabled();
  });
});
