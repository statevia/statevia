import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Card } from "@/shared/ui/Card";
import { EmptyState } from "@/shared/ui/EmptyState";
import { Notice } from "@/shared/ui/Notice";

describe("Card", () => {
  it("見出し・説明・操作・本文を描く", () => {
    // Arrange / Act
    render(
      <Card title="グループのメンバー" description="追加できます。" actions={<button type="button">追加</button>}>
        <p>sato</p>
      </Card>
    );

    // Assert
    expect(screen.getByRole("heading", { level: 2, name: "グループのメンバー" })).toBeInTheDocument();
    expect(screen.getByText("追加できます。")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "追加" })).toBeInTheDocument();
    expect(screen.getByText("sato")).toBeInTheDocument();
  });

  it("見出しも操作も無いときは見出しの帯を出さない", () => {
    // Arrange / Act
    render(<Card>本文だけ</Card>);

    // Assert
    expect(screen.queryByRole("heading")).toBeNull();
  });
});

describe("Notice", () => {
  it("error は alert として読み上げる", () => {
    // Arrange / Act
    render(<Notice tone="error" title="読み込めませんでした">通信を確かめてください。</Notice>);

    // Assert
    expect(screen.getByRole("alert")).toHaveTextContent("読み込めませんでした");
  });

  it("error 以外は output（status）として読み上げ、トークンの色を使う", () => {
    // Arrange / Act
    render(<Notice tone="warning" title="プロジェクトが選ばれていません" />);

    // Assert
    const notice = screen.getByRole("status");
    expect(notice).toHaveClass("bg-md-warning-container", "text-md-on-warning-container");
  });
});

describe("EmptyState", () => {
  it("見出し・説明・次の操作を描く", () => {
    // Arrange / Act
    render(<EmptyState title="まだ定義がありません" description="作成すると実行を始められます。" action={<button type="button">新しい定義を作成</button>} />);

    // Assert
    expect(screen.getByText("まだ定義がありません")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "新しい定義を作成" })).toBeInTheDocument();
  });
});

describe("Breadcrumbs", () => {
  it("途中の段はリンク、最後の段は aria-current にする", () => {
    // Arrange / Act
    render(<Breadcrumbs items={[{ label: "ワークフロー" }, { label: "定義", href: "/definitions" }, { label: "daily-report" }]} />);

    // Assert
    expect(screen.getByRole("link", { name: "定義" })).toHaveAttribute("href", "/definitions");
    expect(screen.queryByRole("link", { name: "ワークフロー" })).toBeNull();
    expect(screen.getByText("daily-report")).toHaveAttribute("aria-current", "page");
  });
});
