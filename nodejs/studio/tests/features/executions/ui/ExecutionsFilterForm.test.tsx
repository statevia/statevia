import { fireEvent, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ExecutionsFilterForm, type ExecutionsFilterFormProps } from "@/features/executions/ui/ExecutionsFilterForm";
import { renderWithUiText } from "@/tests/testUtils";

/**
 * 既定の入力に、テストで見たいものだけを上書きした props を作る。
 * @param overrides 上書きする入力。
 * @returns フィルタ欄の props。
 */
function filterProps(overrides: Partial<ExecutionsFilterFormProps> = {}): ExecutionsFilterFormProps {
  return {
    status: "",
    sortBy: "updatedAt",
    sortOrder: "desc",
    nameDraft: "",
    definitionDraft: "",
    nameError: null,
    definitionError: null,
    loading: false,
    onSubmit: vi.fn(),
    onStatusChange: vi.fn(),
    onNameDraftChange: vi.fn(),
    onDefinitionDraftChange: vi.fn(),
    onSortChange: vi.fn(),
    ...overrides
  };
}

describe("ExecutionsFilterForm", () => {
  it("ステータスのピルは、値を API の値のままにし、表示だけ大文字にそろえる", () => {
    // Arrange
    const props = filterProps();

    // Act
    renderWithUiText(<ExecutionsFilterForm {...props} />);

    // Assert
    const pills = within(screen.getByRole("toolbar", { name: "ステータス" })).getAllByRole("button");
    expect(pills.map((pill) => pill.textContent)).toEqual(["すべて", "ACTIVE", "COMPLETED", "FAILED", "CANCELED"]);
    expect(screen.getByRole("button", { name: "すべて" })).toHaveAttribute("aria-pressed", "true");
  });

  it("ピルを押すと API の値で絞り込みを変える", () => {
    // Arrange
    const onStatusChange = vi.fn();
    renderWithUiText(<ExecutionsFilterForm {...filterProps({ onStatusChange })} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "CANCELED" }));

    // Assert
    expect(onStatusChange).toHaveBeenCalledWith("CANCELED");
  });

  it("並び順を選ぶと項目と順序を一度に渡す", () => {
    // Arrange
    const onSortChange = vi.fn();
    renderWithUiText(<ExecutionsFilterForm {...filterProps({ onSortChange })} />);

    // Act
    fireEvent.change(screen.getByLabelText("並び順"), { target: { value: "displayId:asc" } });

    // Assert
    expect(onSortChange).toHaveBeenCalledWith("displayId", "asc");
  });

  it("検証エラーは入力欄の下に出す", () => {
    // Arrange / Act
    renderWithUiText(<ExecutionsFilterForm {...filterProps({ nameError: "実行の ID が不正です" })} />);

    // Assert
    expect(screen.getByRole("alert")).toHaveTextContent("実行の ID が不正です");
    expect(screen.getByLabelText("実行の ID（部分一致）または UUID（完全一致）")).toHaveAttribute("aria-invalid", "true");
  });
});
