import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { uiText } from "@/shared/i18n/uiText";

describe("ConfirmDialog", () => {
  it("open が false のときは何も描かない", () => {
    // Arrange / Act
    const { container } = render(
      <ConfirmDialog open={false} title="削除しますか？" message="元に戻せません。" confirmLabel="削除する" onConfirm={vi.fn()} onCancel={vi.fn()} />
    );

    // Assert
    expect(container.firstChild).toBeNull();
  });

  it("決定を押すと onConfirm を呼ぶ", () => {
    // Arrange
    const onConfirm = vi.fn();
    render(<ConfirmDialog open title="削除しますか？" message="元に戻せません。" confirmLabel="削除する" onConfirm={onConfirm} onCancel={vi.fn()} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "削除する" }));

    // Assert
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog", { name: "削除しますか？" })).toBeInTheDocument();
  });

  it("Esc で onCancel を呼び、onConfirm は呼ばない", () => {
    // Arrange
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(<ConfirmDialog open title="削除しますか？" message="元に戻せません。" confirmLabel="削除する" onConfirm={onConfirm} onCancel={onCancel} />);

    // Act
    fireEvent.keyDown(document, { key: "Escape" });

    // Assert
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("requireTypedName のときは名前が一致するまで決定できない", () => {
    // Arrange
    render(
      <ConfirmDialog
        open
        title="定義を削除しますか？"
        message="元に戻せません。"
        confirmLabel="削除する"
        requireTypedName="invoice-sync"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    const confirmButton = screen.getByRole("button", { name: "削除する" });
    const input = screen.getByLabelText(uiText.dialog.typeNameToConfirm("invoice-sync"));

    // Act
    fireEvent.change(input, { target: { value: "invoice" } });
    const disabledWhilePartial = confirmButton.hasAttribute("disabled");
    fireEvent.change(input, { target: { value: "invoice-sync" } });

    // Assert
    expect(disabledWhilePartial).toBe(true);
    expect(confirmButton).toBeEnabled();
  });

  it("loading のあいだは Esc で閉じず、キャンセルも押せない", () => {
    // Arrange
    const onCancel = vi.fn();
    render(<ConfirmDialog open loading title="削除しますか？" message="元に戻せません。" confirmLabel="削除する" onConfirm={vi.fn()} onCancel={onCancel} />);

    // Act
    fireEvent.keyDown(document, { key: "Escape" });

    // Assert
    expect(onCancel).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: uiText.actions.cancel })).toBeDisabled();
  });
});
