import { describe, expect, it } from "vitest";
import { buildActionBindingUpdateBody, parseAllowedActionIds } from "@/features/admin/actionBindings";
import { actionBindingText } from "@/features/admin/i18n/actionBindingText";

describe("action binding write body", () => {
  it("シークレットの空欄は値を送らない", () => {
    const body = buildActionBindingUpdateBody("secret", "  ", ["acme.order.query"]);

    expect(body).toEqual({ kind: "secret", allowedActionIds: ["acme.order.query"] });
    expect(body).not.toHaveProperty("value");
  });

  it("変数は値を送り、空行の actionId は捨てる", () => {
    expect(parseAllowedActionIds(" acme.order.query \n\nacme.order.other")).toEqual([
      "acme.order.query",
      "acme.order.other"
    ]);
    expect(buildActionBindingUpdateBody("variable", "25", ["acme.order.query"])).toEqual({
      kind: "variable",
      allowedActionIds: ["acme.order.query"],
      value: "25"
    });
  });
});

describe("action binding ui text", () => {
  it("日英で同じ項目を返し、更新時刻の接頭辞だけが違う", () => {
    const japanese = actionBindingText("ja");
    const english = actionBindingText("en");

    expect(japanese.title).toBe("Action バインディング");
    expect(english.title).toBe("Action bindings");
    expect(japanese.updatedAt("2026-06-01T00:00:00Z")).toBe("更新: 2026-06-01T00:00:00Z");
    expect(english.updatedAt("2026-06-01T00:00:00Z")).toBe("Updated: 2026-06-01T00:00:00Z");
    expect(japanese.secretValueHint).not.toBe(english.secretValueHint);
  });
});
