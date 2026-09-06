import { describe, expect, it } from "vitest";
import { matchesPattern } from "@/shared/lib/validation/primitives";
import {
  ASCII_LABEL_PATTERN,
  DEFINITION_NAME_PATTERN,
  EVENT_NAME_PATTERN
} from "@/shared/lib/validation/formRules";

describe("formRules charset", () => {
  it("定義名は Identifier を許可し日本語を拒否する", () => {
    expect(matchesPattern("Order_v2.start", DEFINITION_NAME_PATTERN)).toBe(true);
    expect(matchesPattern("ユーザー定義", DEFINITION_NAME_PATTERN)).toBe(false);
  });

  it("イベント名は Identifier を許可し記号を拒否する", () => {
    expect(matchesPattern("Approved", EVENT_NAME_PATTERN)).toBe(true);
    expect(matchesPattern("<script>", EVENT_NAME_PATTERN)).toBe(false);
  });

  it("表示名は AsciiLabel を許可し日本語を拒否する", () => {
    expect(matchesPattern("Acme Corporation", ASCII_LABEL_PATTERN)).toBe(true);
    expect(matchesPattern("A", ASCII_LABEL_PATTERN)).toBe(true);
    expect(matchesPattern("運用者", ASCII_LABEL_PATTERN)).toBe(false);
    expect(matchesPattern("-ops", ASCII_LABEL_PATTERN)).toBe(false);
  });
});
