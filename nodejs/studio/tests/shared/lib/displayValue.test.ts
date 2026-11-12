import { describe, expect, it } from "vitest";
import { toDisplayValue } from "@/shared/lib/displayValue";

describe("toDisplayValue", () => {
  it("実行の状態を大文字にする", () => {
    // Arrange
    const values = ["Running", "Completed", "Failed"];

    // Act
    const displayed = values.map(toDisplayValue);

    // Assert
    expect(displayed).toEqual(["RUNNING", "COMPLETED", "FAILED"]);
  });

  it("実行の Cancelled とノードの CANCELED を同じ CANCELED にそろえる", () => {
    // Arrange
    const values = ["Cancelled", "CANCELED", "cancelled"];

    // Act
    const displayed = values.map(toDisplayValue);

    // Assert
    expect(displayed).toEqual(["CANCELED", "CANCELED", "CANCELED"]);
  });

  it("ノードのタイプを大文字にする", () => {
    // Arrange
    const nodeTypes = ["Start", "Task", "Fork", "Join", "Wait", "End"];

    // Act
    const displayed = nodeTypes.map(toDisplayValue);

    // Assert
    expect(displayed).toEqual(["START", "TASK", "FORK", "JOIN", "WAIT", "END"]);
  });

  it("Object の継承プロパティ名でも大文字にするだけで別の値に変えない", () => {
    // Arrange
    const value = "constructor";

    // Act
    const displayed = toDisplayValue(value);

    // Assert
    expect(displayed).toBe("CONSTRUCTOR");
  });
});
