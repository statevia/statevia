import { describe, expect, it } from "vitest";
import { buildActionBindingUpdateBody, parseAllowedActionIds } from "@/features/admin/actionBindings";

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
