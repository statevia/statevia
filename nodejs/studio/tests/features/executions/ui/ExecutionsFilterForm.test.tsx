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
    loading: false,
    limit: 20,
    onSubmit: vi.fn(),
    onStatusChange: vi.fn(),
    onNameDraftChange: vi.fn(),
    onDefinitionDraftChange: vi.fn(),
    onSortByChange: vi.fn(),
    onSortOrderChange: vi.fn(),
    onClear: vi.fn(),
    ...overrides
  };
}

describe("ExecutionsFilterForm", () => {
  it("ステータスの選択肢は、値を API の値のままにし、表示だけ大文字にそろえる", () => {
    // Arrange
    const props = filterProps();

    // Act
    renderWithUiText(<ExecutionsFilterForm {...props} />);

    // Assert
    const options = within(screen.getByLabelText("ステータス")).getAllByRole<HTMLOptionElement>("option");
    expect(options.map((option) => [option.value, option.textContent])).toEqual([
      ["", "（すべて）"],
      ["Running", "RUNNING"],
      ["Completed", "COMPLETED"],
      ["Cancelled", "CANCELED"],
      ["Failed", "FAILED"]
    ]);
  });

  it("選択肢を選ぶと API の値で絞り込みを変える", () => {
    // Arrange
    const onStatusChange = vi.fn();
    renderWithUiText(<ExecutionsFilterForm {...filterProps({ onStatusChange })} />);

    // Act
    fireEvent.change(screen.getByLabelText("ステータス"), { target: { value: "Cancelled" } });

    // Assert
    expect(onStatusChange).toHaveBeenCalledWith("Cancelled");
  });
});
