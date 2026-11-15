import { describe, expect, it } from "vitest";
import { diffSelection, hasSelectionChanges } from "@/shared/lib/selectionDiff";

describe("diffSelection", () => {
  it("今の選択にだけある値を追加、保存済みにだけある値を解除にする", () => {
    // Arrange
    const initial = new Set(["a", "b"]);
    const selected = new Set(["b", "c"]);

    // Act
    const diff = diffSelection(initial, selected);

    // Assert
    expect(diff).toEqual({ added: ["c"], removed: ["a"] });
    expect(hasSelectionChanges(diff)).toBe(true);
  });

  it("同じ選択なら変更は無い", () => {
    // Arrange / Act
    const diff = diffSelection(new Set(["a"]), new Set(["a"]));

    // Assert
    expect(hasSelectionChanges(diff)).toBe(false);
  });
});
