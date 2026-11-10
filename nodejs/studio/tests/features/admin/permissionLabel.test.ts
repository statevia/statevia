import { describe, expect, it } from "vitest";
import { adminUiTextEn } from "@/features/admin/i18n/en";
import { adminUiTextJa } from "@/features/admin/i18n/ja";
import { resolvePermissionLabel } from "@/features/admin/permissionLabel";

const names = { "permissions.definitionsRead": "定義の閲覧" };

describe("resolvePermissionLabel", () => {
  it("displayKey が辞書にあるときは辞書の語を返す", () => {
    // Arrange
    const permission = { displayKey: "permissions.definitionsRead", displayLabel: "Read definitions" };

    // Act
    const label = resolvePermissionLabel(permission, names);

    // Assert
    expect(label).toBe("定義の閲覧");
  });

  it("displayKey が辞書に無いときは displayLabel を返し、辞書キーを出さない", () => {
    // Arrange
    const permission = { displayKey: "permissions.unknownFuture", displayLabel: "Future permission" };

    // Act
    const label = resolvePermissionLabel(permission, names);

    // Assert
    expect(label).toBe("Future permission");
  });

  it("displayKey が null のときは displayLabel を返す", () => {
    // Arrange
    const permission = { displayKey: null, displayLabel: "Custom permission" };

    // Act
    const label = resolvePermissionLabel(permission, names);

    // Assert
    expect(label).toBe("Custom permission");
  });

  it("Object の継承プロパティ名の displayKey でも displayLabel を返す", () => {
    // Arrange
    const permission = { displayKey: "constructor", displayLabel: "Odd permission" };

    // Act
    const label = resolvePermissionLabel(permission, names);

    // Assert
    expect(label).toBe("Odd permission");
  });
});

describe("権限名の辞書", () => {
  it("ja と en が同じ displayKey を持つ", () => {
    // Arrange
    const jaKeys = Object.keys(adminUiTextJa.admin.permissions.names).sort((a, b) => a.localeCompare(b));
    const enKeys = Object.keys(adminUiTextEn.admin.permissions.names).sort((a, b) => a.localeCompare(b));

    // Act & Assert
    expect(jaKeys).toEqual(enKeys);
  });
});
