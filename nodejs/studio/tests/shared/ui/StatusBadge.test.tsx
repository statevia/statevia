import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatusBadge } from "@/shared/ui/StatusBadge";

describe("StatusBadge", () => {
  it("Execution ステータス（契約の値）をそのまま表示する", () => {
    // Arrange
    const props = { status: "ACTIVE" as const };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("ACTIVE")).toBeInTheDocument();
  });

  it("追加 className を結合して表示する", () => {
    // Arrange
    const props = { status: "COMPLETED" as const, className: "rounded-full" };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("COMPLETED")).toHaveClass("rounded-full");
  });

  it("実行の CANCELED はノードと同じ綴りで表示する", () => {
    // Arrange
    const props = { status: "CANCELED" as const };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("CANCELED")).toBeInTheDocument();
  });
});
