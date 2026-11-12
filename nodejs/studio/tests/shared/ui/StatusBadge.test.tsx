import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatusBadge } from "@/shared/ui/StatusBadge";

describe("StatusBadge", () => {
  it("Execution ステータスを大文字にして表示する", () => {
    // Arrange
    const props = { status: "Running" as const };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("RUNNING")).toBeInTheDocument();
  });

  it("追加 className を結合して表示する", () => {
    // Arrange
    const props = { status: "Completed" as const, className: "rounded-full" };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("COMPLETED")).toHaveClass("rounded-full");
  });

  it("実行の Cancelled はノードと同じ CANCELED で表示する", () => {
    // Arrange
    const props = { status: "Cancelled" as const };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("CANCELED")).toBeInTheDocument();
  });
});
