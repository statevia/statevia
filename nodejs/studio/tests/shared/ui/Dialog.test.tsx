import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Dialog } from "@/shared/ui/Dialog";
import { uiText } from "@/shared/i18n/uiText";

describe("Dialog", () => {
  it("開くと見出しで名前の付いた dialog になり、最初の操作にフォーカスする", () => {
    // Arrange / Act
    render(
      <Dialog open onClose={vi.fn()} title="ユーザーを作成" description="初期パスワードを伝えてください。" footer={<button type="button">作成</button>}>
        <input aria-label="ユーザー名" />
      </Dialog>
    );

    // Assert
    expect(screen.getByRole("dialog", { name: "ユーザーを作成" })).toHaveAttribute("aria-modal", "true");
    expect(screen.getByRole("button", { name: uiText.dialog.close })).toHaveFocus();
  });

  it("右上の閉じるで onClose を呼ぶ", () => {
    // Arrange
    const onClose = vi.fn();
    render(<Dialog open onClose={onClose} title="ユーザーを作成" />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.dialog.close }));

    // Assert
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("dismissible が false のときは閉じるを出さず、Esc でも閉じない", () => {
    // Arrange
    const onClose = vi.fn();
    render(<Dialog open dismissible={false} onClose={onClose} title="送信中" />);

    // Act
    fireEvent.keyDown(document, { key: "Escape" });

    // Assert
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: uiText.dialog.close })).toBeNull();
  });

  it("最後の要素で Tab を押すと最初の要素へ戻る", () => {
    // Arrange
    render(
      <Dialog open onClose={vi.fn()} title="ユーザーを作成" footer={<button type="button">作成</button>}>
        <input aria-label="ユーザー名" />
      </Dialog>
    );
    const lastButton = screen.getByRole("button", { name: "作成" });
    lastButton.focus();

    // Act
    fireEvent.keyDown(document, { key: "Tab" });

    // Assert
    expect(screen.getByRole("button", { name: uiText.dialog.close })).toHaveFocus();
  });
});
