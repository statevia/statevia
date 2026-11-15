import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen, within } from "@testing-library/react";
import { ExecutionWaitNotice } from "@/features/executions/ui/ExecutionWaitNotice";
import type { WaitTarget } from "@/features/executions/lib/waitTargets";
import { renderWithUiText } from "@/tests/testUtils";

const approve: WaitTarget = { nodeId: "n-approve", nodeName: "approve", events: ["approval.granted"], waitingSince: null };
const payment: WaitTarget = { nodeId: "n-payment", nodeName: "await-payment", events: ["payment.confirmed"], waitingSince: null };

describe("ExecutionWaitNotice", () => {
  it("待機が無ければ何も出さない", () => {
    // Arrange / Act
    const { container } = renderWithUiText(<ExecutionWaitNotice waits={[]} />);

    // Assert
    expect(container).toBeEmptyDOMElement();
  });

  it("待機が 1 つなら、待っているノードとイベント名を文で出す", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionWaitNotice waits={[approve]} onSendTo={vi.fn()} />);

    // Assert
    expect(screen.getByText("approve でイベントを待っています")).toBeInTheDocument();
    expect(screen.getByText("イベント approval.granted が届くと先へ進みます。")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("待機が 2 つなら 1 件ずつ並べ、行の操作でそのノードを渡す", () => {
    // Arrange
    const onSendTo = vi.fn();
    renderWithUiText(<ExecutionWaitNotice waits={[approve, payment]} onSendTo={onSendTo} />);
    const list = screen.getByRole("list", { name: "待機の一覧" });

    // Act
    fireEvent.click(within(list).getByRole("button", { name: "このイベントを送る: await-payment" }));

    // Assert
    expect(screen.getByText("2 つのノードがイベントを待っています")).toBeInTheDocument();
    expect(within(list).getAllByRole("listitem")).toHaveLength(2);
    expect(onSendTo).toHaveBeenCalledWith("n-payment");
  });

  it("送れないときは行の操作を出さない", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionWaitNotice waits={[approve, payment]} />);

    // Assert
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
