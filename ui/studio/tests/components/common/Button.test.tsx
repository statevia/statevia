import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Button } from "@/shared/ui/Button";

describe("Button", () => {
  it("primary はブランド CTA のトークンクラスを持つ", () => {
    // Arrange
    render(
      <Button variant="primary" type="submit">
        送る
      </Button>
    );

    // Act
    const button = screen.getByRole("button", { name: "送る" });

    // Assert
    expect(button).toHaveClass("border-brand-cta-border", "bg-brand-cta-bg", "text-brand-cta-fg");
    expect(button).toHaveAttribute("type", "submit");
  });

  it("secondary は枠線トークンのクラスを持つ", () => {
    // Arrange
    render(<Button variant="secondary">閉じる</Button>);

    // Act
    const button = screen.getByRole("button", { name: "閉じる" });

    // Assert
    expect(button).toHaveClass("border-md-outline-variant");
    expect(button).toHaveAttribute("type", "button");
  });

  it("追加 className をバリアントの後ろへ結合する", () => {
    // Arrange / Act
    render(
      <Button variant="secondary" className="w-full">
        閉じる
      </Button>
    );

    // Assert
    expect(screen.getByRole("button", { name: "閉じる" })).toHaveClass("border-md-outline-variant", "w-full");
  });
});
