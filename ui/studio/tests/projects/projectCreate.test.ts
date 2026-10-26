import { describe, expect, it } from "vitest";
import {
  buildCreateProjectBody,
  isSlugConflictError,
  PROJECT_DISPLAY_NAME_MAX_LENGTH,
  PROJECT_SLUG_MAX_LENGTH,
} from "@/features/projects/projectCreate";

describe("buildCreateProjectBody", () => {
  it("前後の空白を除き、空白だけの説明は本文に含めない", () => {
    // Arrange
    const input = { slug: "  alpha ", displayName: " Alpha ", description: "   " };

    // Act
    const result = buildCreateProjectBody(input);

    // Assert
    expect(result).toEqual({ ok: true, body: { slug: "alpha", displayName: "Alpha" } });
  });

  it("説明があれば trim して本文に含める", () => {
    // Arrange
    const input = { slug: "alpha", displayName: "Alpha", description: " 説明 " };

    // Act
    const result = buildCreateProjectBody(input);

    // Assert
    expect(result).toEqual({ ok: true, body: { slug: "alpha", displayName: "Alpha", description: "説明" } });
  });

  it("slug と表示名が空白だけなら両方を必須エラーにする", () => {
    // Arrange
    const input = { slug: "  ", displayName: "", description: "" };

    // Act
    const result = buildCreateProjectBody(input);

    // Assert
    expect(result).toEqual({ ok: false, errors: { slug: "required", displayName: "required" } });
  });

  it("trim 後に上限を超える slug と表示名は長さエラーにする", () => {
    // Arrange
    const input = {
      slug: "s".repeat(PROJECT_SLUG_MAX_LENGTH + 1),
      displayName: "d".repeat(PROJECT_DISPLAY_NAME_MAX_LENGTH + 1),
      description: "",
    };

    // Act
    const result = buildCreateProjectBody(input);

    // Assert
    expect(result).toEqual({ ok: false, errors: { slug: "tooLong", displayName: "tooLong" } });
  });

  it("上限ちょうどの長さは受け付ける", () => {
    // Arrange
    const input = {
      slug: ` ${"s".repeat(PROJECT_SLUG_MAX_LENGTH)} `,
      displayName: "d".repeat(PROJECT_DISPLAY_NAME_MAX_LENGTH),
      description: "",
    };

    // Act
    const result = buildCreateProjectBody(input);

    // Assert
    expect(result.ok).toBe(true);
  });
});

describe("isSlugConflictError", () => {
  it("422 で details.field が slug なら true", () => {
    // Arrange
    const error = { status: 422, error: { code: "VALIDATION_ERROR", message: "dup", details: { field: "slug" } } };

    // Act & Assert
    expect(isSlugConflictError(error)).toBe(true);
  });

  it("422 でも別の欄なら false", () => {
    // Arrange
    const error = { status: 422, error: { code: "VALIDATION_ERROR", message: "x", details: { field: "displayName" } } };

    // Act & Assert
    expect(isSlugConflictError(error)).toBe(false);
  });

  it("403 は false", () => {
    // Arrange
    const error = { status: 403, error: { code: "FORBIDDEN", message: "no", details: { field: "slug" } } };

    // Act & Assert
    expect(isSlugConflictError(error)).toBe(false);
  });

  it("API エラーでない値は false", () => {
    // Arrange & Act & Assert
    expect(isSlugConflictError(new Error("network"))).toBe(false);
  });
});
