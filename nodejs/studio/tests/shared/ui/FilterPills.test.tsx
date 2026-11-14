import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { FilterPills } from "@/shared/ui/FilterPills";

const options = [
  { value: "", label: "すべて" },
  { value: "FAILED", label: "FAILED", count: 3 },
] as const;

describe("FilterPills", () => {
  it("選んでいるピルだけを aria-pressed=true にする", () => {
    // Arrange / Act
    render(<FilterPills ariaLabel="ステータス" options={options} value="FAILED" onChange={vi.fn()} />);

    // Assert
    expect(screen.getByRole("button", { name: /FAILED/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "すべて" })).toHaveAttribute("aria-pressed", "false");
  });

  it("件数があるときは文言の右に出す", () => {
    // Arrange / Act
    render(<FilterPills ariaLabel="ステータス" options={options} value="" onChange={vi.fn()} />);

    // Assert
    expect(screen.getByRole("button", { name: /FAILED/ })).toHaveTextContent("FAILED3");
  });

  it("ピルを押すとその値で onChange を呼ぶ", () => {
    // Arrange
    const onChange = vi.fn();
    render(<FilterPills ariaLabel="ステータス" options={options} value="FAILED" onChange={onChange} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "すべて" }));

    // Assert
    expect(onChange).toHaveBeenCalledWith("");
  });
});
