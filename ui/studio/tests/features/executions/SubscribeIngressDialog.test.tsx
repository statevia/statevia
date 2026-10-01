import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { SubscribeIngressDialog } from "@/features/executions/ui/SubscribeIngressDialog";
import { renderWithUiText } from "../../testUtils";

vi.mock("@/features/executions/api", () => ({
  listEventSubscriptions: vi.fn(),
  publishTenantEvent: vi.fn()
}));

import { listEventSubscriptions, publishTenantEvent } from "@/features/executions/api";

describe("SubscribeIngressDialog", () => {
  beforeEach(() => {
    vi.mocked(listEventSubscriptions).mockReset();
    vi.mocked(publishTenantEvent).mockReset();
    vi.mocked(listEventSubscriptions).mockResolvedValue({ subscriptions: [] });
    vi.mocked(publishTenantEvent).mockResolvedValue(undefined);
  });

  it("送信・キャンセル・入力は共通部品のトークンクラスを使う", async () => {
    // Arrange
    renderWithUiText(
      <SubscribeIngressDialog open onClose={vi.fn()} onAccepted={vi.fn()} />
    );

    // Act
    const submit = screen.getByRole("button", { name: "送る" });
    const cancel = screen.getByRole("button", { name: "閉じる" });

    // Assert
    expect(submit).toHaveClass("bg-brand-cta-bg", "text-brand-cta-fg");
    expect(cancel).toHaveClass("border-md-outline-variant");
    expect(screen.getByLabelText("topic")).toHaveClass("border-md-outline-variant", "font-mono");
    await waitFor(() => {
      expect(listEventSubscriptions).toHaveBeenCalledTimes(1);
    });
  });

  it("閉じているときはダイアログを出さず候補も取らない", () => {
    // Arrange
    const onClose = vi.fn();
    const onAccepted = vi.fn();

    // Act
    renderWithUiText(
      <SubscribeIngressDialog open={false} onClose={onClose} onAccepted={onAccepted} />
    );

    // Assert
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(listEventSubscriptions).not.toHaveBeenCalled();
  });

  it("topic が識別子でないときエラーを出し送信しない", async () => {
    // Arrange
    renderWithUiText(
      <SubscribeIngressDialog open onClose={vi.fn()} onAccepted={vi.fn()} />
    );

    // Act
    fireEvent.change(screen.getByLabelText("topic"), { target: { value: "not valid" } });
    fireEvent.click(screen.getByRole("button", { name: "送る" }));

    // Assert
    expect(
      await screen.findByText("topic は英字で始まり、半角英数字と . _ - のみ、256 文字以内です。")
    ).toBeInTheDocument();
    expect(publishTenantEvent).not.toHaveBeenCalled();
  });

  it("候補取得に失敗しても手入力を残し失敗を知らせる", async () => {
    // Arrange
    vi.mocked(listEventSubscriptions).mockRejectedValue(new Error("unavailable"));

    // Act
    renderWithUiText(
      <SubscribeIngressDialog open onClose={vi.fn()} onAccepted={vi.fn()} />
    );

    // Assert
    expect(await screen.findByText("候補を読めませんでした。topic / key は手入力できます。")).toBeInTheDocument();
    expect(screen.getByLabelText("topic")).toBeInTheDocument();
    await waitFor(() => {
      expect(listEventSubscriptions).toHaveBeenCalledTimes(1);
    });
  });
});
