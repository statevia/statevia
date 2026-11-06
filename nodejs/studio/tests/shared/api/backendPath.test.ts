import { describe, expect, it } from "vitest";
import { pathForBackend } from "@/shared/api/backendPath";

describe("pathForBackend", () => {
  it("projects を v1 へ写す", () => {
    expect(pathForBackend(["projects", "abc", "members"])).toEqual([
      "v1",
      "projects",
      "abc",
      "members",
    ]);
  });

  it("定期実行の手動実行パスを v1/schedules へ写す", () => {
    // Arrange
    const pathParts = ["schedules", "sch-1", "run"];

    // Act
    const result = pathForBackend(pathParts);

    // Assert
    expect(result).toEqual(["v1", "schedules", "sch-1", "run"]);
  });

  it("admin projects を v1/admin へ写す", () => {
    expect(pathForBackend(["admin", "projects"])).toEqual(["v1", "admin", "projects"]);
  });
});
