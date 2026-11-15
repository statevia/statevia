import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen, within } from "@testing-library/react";
import { SearchableCheckList } from "@/shared/ui/SearchableCheckList";
import { renderWithUiText } from "@/tests/testUtils";

const items = [
  { value: "u-1", label: "alice", description: "Alice Smith" },
  { value: "u-2", label: "bob", description: "Bob Jones" }
];

describe("SearchableCheckList", () => {
  it("表示名と補足を大文字小文字を区別せずに部分一致で絞り込み、当てはまらなければその旨を出す", () => {
    // Arrange
    renderWithUiText(
      <SearchableCheckList ariaLabel="メンバー" items={items} selected={new Set()} initial={new Set()} onToggle={vi.fn()} />
    );
    const search = screen.getByLabelText("メンバーを検索");

    // Act
    fireEvent.change(search, { target: { value: "JONES" } });
    const filtered = within(screen.getByRole("list", { name: "メンバー" }))
      .getAllByRole("listitem")
      .map((row) => row.textContent);
    fireEvent.change(search, { target: { value: "zzz" } });

    // Assert
    expect(filtered).toEqual(["bobBob Jones"]);
    expect(screen.getByText("当てはまる項目はありません。")).toBeInTheDocument();
  });

  it("保存済みと違う行に追加と解除の印を出し、件数を出す", () => {
    // Arrange / Act
    renderWithUiText(
      <SearchableCheckList
        ariaLabel="メンバー"
        items={items}
        selected={new Set(["u-2"])}
        initial={new Set(["u-1"])}
        onToggle={vi.fn()}
      />
    );

    // Assert
    const rows = within(screen.getByRole("list", { name: "メンバー" })).getAllByRole("listitem");
    expect(rows[0]).toHaveTextContent("解除");
    expect(rows[1]).toHaveTextContent("追加");
    expect(screen.getByText("2 件中 1 件を選択")).toBeInTheDocument();
  });

  it("チェックボックスで onToggle を値つきで呼ぶ", () => {
    // Arrange
    const onToggle = vi.fn();
    renderWithUiText(
      <SearchableCheckList ariaLabel="メンバー" items={items} selected={new Set()} initial={new Set()} onToggle={onToggle} />
    );

    // Act
    fireEvent.click(screen.getByLabelText(/alice/));

    // Assert
    expect(onToggle).toHaveBeenCalledWith("u-1");
  });
});
