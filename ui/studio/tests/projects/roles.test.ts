import { describe, expect, it } from "vitest";
import { canAdmin, canExecute, canPublish, isProjectMemberKind } from "@/features/projects/roles";

describe("project roles", () => {
  it("reader は実行も公開もできない", () => {
    expect(canExecute("reader")).toBe(false);
    expect(canPublish("reader")).toBe(false);
    expect(canAdmin("reader")).toBe(false);
  });

  it("executor は実行でき、公開はできない", () => {
    expect(canExecute("executor")).toBe(true);
    expect(canPublish("executor")).toBe(false);
  });

  it("publisher は実行と公開ができ、メンバー管理はできない", () => {
    expect(canExecute("publisher")).toBe(true);
    expect(canPublish("publisher")).toBe(true);
    expect(canAdmin("publisher")).toBe(false);
  });

  it("admin はメンバーを管理できる", () => {
    expect(canAdmin("admin")).toBe(true);
  });

  it("メンバー種別は user / serviceAccount / system だけ", () => {
    expect(isProjectMemberKind("user")).toBe(true);
    expect(isProjectMemberKind("serviceAccount")).toBe(true);
    expect(isProjectMemberKind("principal")).toBe(false);
  });

  it("廃止した apiKey はメンバー種別として受け取らない", () => {
    expect(isProjectMemberKind("apiKey")).toBe(false);
  });
});
