import { describe, expect, it } from "vitest";
import { parseDefinitionYaml } from "@/features/definition-editor/lib/parseDefinitionYaml";
import { serializeDefinitionYaml } from "@/features/definition-editor/lib/serializeDefinitionYaml";

const parseOpts = {
  rootObjectRequired: () => "root",
  nodesArrayRequired: () => "nodes"
};

describe("parseDefinitionYaml / serializeDefinitionYaml（ローダー整合）", () => {
  it("action の input を文字列で保持し、往復できる", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: noop
    input: "$.input.x"
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document).not.toBeNull();
    expect(r.document?.nodes.find((n) => n.name === "a")?.input).toBe("$.input.x");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "a")?.input).toBe("$.input.x");
  });

  it("action の bindings を編集フォームなしで往復する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: orders.query
    bindings:
      secrets:
        connection: order-db
      variables:
        pageSize: order-page-size
    input:
      customerId: $.vars.customerId
    next: e
  - name: e
    type: end
`;
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    const node = parsed.document?.nodes.find((candidate) => candidate.name === "a");
    expect(node?.bindings).toEqual({
      secrets: { connection: "order-db" },
      variables: { pageSize: "order-page-size" }
    });
    if (!parsed.document) {
      throw new Error("document should not be null");
    }
    const again = parseDefinitionYaml(serializeDefinitionYaml(parsed.document), parseOpts);
    expect(again.document?.nodes.find((candidate) => candidate.name === "a")?.bindings).toEqual(
      node?.bindings
    );
  });

  it("edges[].to がオブジェクトのとき name に正規化する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: noop
    edges:
      - to:
          name: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "a")?.edges?.[0]?.to).toBe("e");
  });

  it("action.error を保持し、{name} 形式を正規化する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: noop
    next: e
    error:
      name: ng
  - name: ng
    type: end
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "a")?.error).toBe("ng");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "a")?.error).toBe("ng");
  });

  it("workflow.id / description を保持し、往復で欠落しない", () => {
    const yaml = `version: 1
workflow:
  id: wf-1
  name: MyName
  description: "Hello"
nodes:
  - name: s
    type: start
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.workflow.name).toBe("MyName");
    expect(r.document?.workflow.id).toBe("wf-1");
    expect(r.document?.workflow.description).toBe("Hello");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.workflow.name).toBe("MyName");
    expect(again.document?.workflow.id).toBe("wf-1");
    expect(again.document?.workflow.description).toBe("Hello");
  });

  it("join に mode が無いときドキュメントへ注入しない", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: j
  - name: j
    type: join
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "j")?.mode).toBeUndefined();
  });

  it("join mode: all を保持する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: j
  - name: j
    type: join
    mode: all
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "j")?.mode).toBe("all");
  });

  it("wait.events を保持し往復できる", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: w1
  - name: w1
    type: wait
    events:
      approve: ok
      reject: ng
  - name: ok
    type: action
    action: noop
    next: e
  - name: ng
    type: action
    action: noop
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "w1")?.events).toEqual({
      approve: "ok",
      reject: "ng"
    });

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "w1")?.events).toEqual({
      approve: "ok",
      reject: "ng"
    });
  });

  it("wait.subscribe 2 件（key あり・なし）を保持し往復できる", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: w1
  - name: w1
    type: wait
    subscribe:
      - topic: orders.created
        key: "$.id"
        next: ok
      - topic: orders.cancelled
        next: ng
  - name: ok
    type: end
  - name: ng
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "w1")?.subscribe).toEqual([
      { topic: "orders.created", key: "$.id", next: "ok" },
      { topic: "orders.cancelled", next: "ng" }
    ]);
    expect(r.document?.nodes.find((n) => n.name === "w1")?.events).toBeUndefined();

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    expect(round).toContain("subscribe:");
    expect(round).not.toMatch(/key:\s*["']?\s*["']?\s*$/m);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "w1")?.subscribe).toEqual([
      { topic: "orders.created", key: "$.id", next: "ok" },
      { topic: "orders.cancelled", next: "ng" }
    ]);
    expect(again.document?.nodes.find((n) => n.name === "w1")?.events).toBeUndefined();
  });

  it("bindings が文字列マップでないときはノードへ残さない", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: bad
  - name: bad
    type: action
    action: noop
    bindings: []
    next: empty
  - name: empty
    type: action
    action: noop
    bindings:
      secrets:
        connection: 1
      variables: {}
    next: e
  - name: e
    type: end
`;
    const parsed = parseDefinitionYaml(yaml, parseOpts);

    expect(parsed.document?.nodes.find((node) => node.name === "bad")?.bindings).toBeUndefined();
    expect(parsed.document?.nodes.find((node) => node.name === "empty")?.bindings).toBeUndefined();
  });

  it("片側だけの bindings を往復し、空マップは YAML に出さない", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: orders.query
    bindings:
      secrets:
        connection: order-db
    next: e
  - name: e
    type: end
`;
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    const node = parsed.document?.nodes.find((candidate) => candidate.name === "a");

    expect(node?.bindings).toEqual({ secrets: { connection: "order-db" } });
    if (!parsed.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(parsed.document);
    expect(round).toContain("connection: order-db");
    expect(round).not.toContain("variables:");
    const omitted = serializeDefinitionYaml({
      version: 1,
      workflow: { name: "W" },
      nodes: [{ name: "a", type: "action", bindings: { secrets: {}, variables: {} } }]
    });
    expect(omitted).not.toContain("bindings:");
  });

  it("workflow.modules の別名と版レンジ付きの import 値を往復で残す", () => {
    // Arrange
    const yaml = `version: 1
workflow:
  name: W
  modules:
    notification: statevia.action.reference.notification
    orders: acme.orders@^1.2
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: notification.send
    next: e
  - name: e
    type: end
`;

    // Act
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    if (!parsed.document) {
      throw new Error("document should not be null");
    }
    const again = parseDefinitionYaml(serializeDefinitionYaml(parsed.document), parseOpts);

    // Assert
    const expected = {
      notification: "statevia.action.reference.notification",
      orders: "acme.orders@^1.2"
    };
    expect(parsed.document.workflow.modules).toEqual(expected);
    expect(again.document?.workflow.modules).toEqual(expected);
  });

  it("workflow.modules が無い YAML の保存に modules を足さない", () => {
    // Arrange
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: e
  - name: e
    type: end
`;
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    if (!parsed.document) {
      throw new Error("document should not be null");
    }

    // Act
    const round = serializeDefinitionYaml(parsed.document);

    // Assert
    expect(parsed.document.workflow.modules).toBeUndefined();
    expect(round).not.toContain("modules:");
  });

  it("action の retry と output を往復で残す", () => {
    // Arrange
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: orders.fetch
    retry:
      limit: 3
      backoff: exponential
      errors:
        - Timeout
        - Transient
    output: $.vars.order
    next: e
  - name: e
    type: end
`;

    // Act
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    if (!parsed.document) {
      throw new Error("document should not be null");
    }
    const again = parseDefinitionYaml(serializeDefinitionYaml(parsed.document), parseOpts);

    // Assert
    const node = parsed.document.nodes.find((candidate) => candidate.name === "a");
    const roundNode = again.document?.nodes.find((candidate) => candidate.name === "a");
    expect(node?.retry).toEqual({ limit: 3, backoff: "exponential", errors: ["Timeout", "Transient"] });
    expect(node?.output).toBe("$.vars.order");
    expect(roundNode?.retry).toEqual(node?.retry);
    expect(roundNode?.output).toBe("$.vars.order");
  });

  it("retry.limit が整数の文字列のときはローダーと同じく数として読む", () => {
    // Arrange
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: a
    type: action
    action: noop
    retry:
      limit: "2"
`;

    // Act
    const parsed = parseDefinitionYaml(yaml, parseOpts);

    // Assert
    expect(parsed.document?.nodes[0]?.retry).toEqual({ limit: 2 });
  });

  it("retry も output も無い action の保存にその 2 キーを足さない", () => {
    // Arrange
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: a
    type: action
    action: noop
    retry: {}
    output: ""
`;
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    if (!parsed.document) {
      throw new Error("document should not be null");
    }

    // Act
    const round = serializeDefinitionYaml(parsed.document);

    // Assert
    expect(parsed.document.nodes[0]?.retry).toBeUndefined();
    expect(parsed.document.nodes[0]?.output).toBeUndefined();
    expect(round).not.toContain("retry:");
    expect(round).not.toContain("output:");
  });

  it("action 以外のノードの retry と output は読まない", () => {
    // Arrange
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: w
    type: wait
    event: go
    retry:
      limit: 1
    output: $.vars.x
`;

    // Act
    const parsed = parseDefinitionYaml(yaml, parseOpts);

    // Assert
    expect(parsed.document?.nodes[0]?.retry).toBeUndefined();
    expect(parsed.document?.nodes[0]?.output).toBeUndefined();
  });
});
