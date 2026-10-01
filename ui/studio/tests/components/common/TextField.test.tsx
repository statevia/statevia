import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { TextField } from "@/shared/ui/TextField";

describe("TextField", () => {
  it("ラベルが入力に結び付く", () => {
    // Arrange
    const onChange = vi.fn();
    render(<TextField label="topic" value="" onChange={onChange} />);

    // Act
    fireEvent.change(screen.getByLabelText("topic"), { target: { value: "orders" } });

    // Assert
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("入力は枠と背景のトークンクラスを持つ", () => {
    // Arrange
    render(<TextField label="topic" value="orders" onChange={vi.fn()} />);

    // Act
    const input = screen.getByLabelText("topic");

    // Assert
    expect(input).toHaveClass("border-md-outline-variant", "bg-md-surface-container");
    expect(input).toHaveValue("orders");
  });

  it("className は入力へ、children は入力の直下へ出す", () => {
    // Arrange
    render(
      <TextField label="key" value="" onChange={vi.fn()} className="font-mono">
        <span>候補</span>
      </TextField>
    );

    // Act
    const input = screen.getByRole("textbox");

    // Assert
    expect(input).toHaveClass("font-mono", "bg-md-surface-container");
    expect(screen.getByText("key")).toBeInTheDocument();
    expect(screen.getByText("候補")).toBeInTheDocument();
  });
});
