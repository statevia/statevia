import { describe, expect, it } from "vitest";
import { toDisplayValue } from "@/shared/lib/displayValue";

describe("toDisplayValue", () => {
  it("実行の状態（契約の値）はそのままの綴りで出す", () => {
    // Arrange
    const values = ["ACTIVE", "COMPLETED", "FAILED", "CANCELED", "UNKNOWN"];

    // Act
    const displayed = values.map(toDisplayValue);

    // Assert
    expect(displayed).toEqual(["ACTIVE", "COMPLETED", "FAILED", "CANCELED", "UNKNOWN"]);
  });

  it("綴りの読み替えはせず、大文字にするだけにする", () => {
    // Arrange
    const values = ["cancelled", "Running"];

    // Act
    const displayed = values.map(toDisplayValue);

    // Assert
    expect(displayed).toEqual(["CANCELLED", "RUNNING"]);
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
