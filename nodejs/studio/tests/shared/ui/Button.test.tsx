import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Button, buttonClassName } from "@/shared/ui/Button";

describe("Button", () => {
  it("lead は主導線の緑のトークンクラスを持つ", () => {
    // Arrange
    render(
      <Button variant="lead" type="submit">
        保存
      </Button>
    );

    // Act
    const button = screen.getByRole("button", { name: "保存" });

    // Assert
    expect(button).toHaveClass("bg-brand-lead-bg", "text-brand-lead-fg");
    expect(button).toHaveAttribute("type", "submit");
  });

  it("standard はブランド CTA（紺）のトークンクラスを持つ", () => {
    // Arrange / Act
    render(<Button variant="standard">編集</Button>);

    // Assert
    expect(screen.getByRole("button", { name: "編集" })).toHaveClass("bg-brand-cta-bg", "text-brand-cta-fg");
  });

  it("outline は枠線トークンのクラスを持ち、type の既定は button", () => {
    // Arrange / Act
    render(<Button variant="outline">閉じる</Button>);

    // Assert
    const button = screen.getByRole("button", { name: "閉じる" });
    expect(button).toHaveClass("border-md-outline-variant");
    expect(button).toHaveAttribute("type", "button");
  });

  it("danger は赤枠、dangerSolid は赤の塗りになる", () => {
    // Arrange / Act
    render(
      <>
        <Button variant="danger">削除</Button>
        <Button variant="dangerSolid">削除する</Button>
      </>
    );

    // Assert
    expect(screen.getByRole("button", { name: "削除" })).toHaveClass("text-md-error");
    expect(screen.getByRole("button", { name: "削除する" })).toHaveClass("bg-md-error", "text-md-on-error");
  });

  it("loading のときは押せず、aria-busy を付ける", () => {
    // Arrange / Act
    render(
      <Button variant="lead" loading>
        保存中…
      </Button>
    );

    // Assert
    const button = screen.getByRole("button", { name: "保存中…" });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");
  });

  it("size の sm は一覧の行向けの高さになる", () => {
    // Arrange / Act
    render(
      <Button variant="outline" size="sm">
        詳細
      </Button>
    );

    // Assert
    expect(screen.getByRole("button", { name: "詳細" })).toHaveClass("h-7");
  });

  it("追加 className をバリアントの後ろへ結合する", () => {
    // Arrange / Act
    render(
      <Button variant="outline" className="w-full">
        閉じる
      </Button>
    );

    // Assert
    expect(screen.getByRole("button", { name: "閉じる" })).toHaveClass("border-md-outline-variant", "w-full");
  });

  it("buttonClassName はリンクにも同じ見た目のクラスを返す", () => {
    // Arrange / Act
    const className = buttonClassName("lead", "md", "ml-auto");

    // Assert
    expect(className).toContain("bg-brand-lead-bg");
    expect(className).toContain("h-9");
    expect(className.endsWith("ml-auto")).toBe(true);
  });
});
