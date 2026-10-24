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

  it("admin projects を v1/admin へ写す", () => {
    expect(pathForBackend(["admin", "projects"])).toEqual(["v1", "admin", "projects"]);
  });
});
