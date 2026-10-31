import { describe, expect, it } from "vitest";
import { isProjectMemberKind } from "@/features/projects/memberKind";

describe("project member kind", () => {
  it("メンバー種別は user / serviceAccount / system だけ", () => {
    expect(isProjectMemberKind("user")).toBe(true);
    expect(isProjectMemberKind("serviceAccount")).toBe(true);
    expect(isProjectMemberKind("principal")).toBe(false);
  });

  it("廃止した apiKey はメンバー種別として受け取らない", () => {
    expect(isProjectMemberKind("apiKey")).toBe(false);
  });
});
