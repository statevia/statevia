import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";

const options = [
  { value: "yaml", label: "YAML" },
  { value: "graph", label: "グラフ" },
] as const;

describe("SegmentedControl", () => {
  it("選んでいる項目だけを aria-pressed=true にする", () => {
    // Arrange / Act
    render(<SegmentedControl ariaLabel="編集の表示" options={options} value="graph" onChange={vi.fn()} />);

    // Assert
    expect(screen.getByRole("button", { name: "グラフ" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "YAML" })).toHaveAttribute("aria-pressed", "false");
  });

  it("項目を押すとその値で onChange を呼ぶ", () => {
    // Arrange
    const onChange = vi.fn();
    render(<SegmentedControl ariaLabel="編集の表示" options={options} value="yaml" onChange={onChange} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "グラフ" }));

    // Assert
    expect(onChange).toHaveBeenCalledWith("graph");
  });
});
