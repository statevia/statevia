import { describe, expect, it } from "vitest";
import { isApiNotFound } from "@/features/projects/httpStatus";

describe("isApiNotFound", () => {
  it("status 404 だけを未検出とみなす", () => {
    expect(isApiNotFound({ status: 404 })).toBe(true);
    expect(isApiNotFound({ status: 403 })).toBe(false);
    expect(isApiNotFound(null)).toBe(false);
    expect(isApiNotFound("404")).toBe(false);
  });
});
