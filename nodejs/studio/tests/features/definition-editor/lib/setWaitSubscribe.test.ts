import { describe, expect, it } from "vitest";
import {
  addWaitSubscribeRow,
  connectWaitSubscribeTarget,
  removeWaitSubscribeRow,
  setWaitSubscribe,
  setWaitSubscribeTarget,
  switchWaitMode
} from "@/features/definition-editor/lib/setWaitSubscribe";
import type { DefinitionGraphDocument } from "@/features/definition-editor/lib/types";

function doc(nodes: DefinitionGraphDocument["nodes"]): DefinitionGraphDocument {
  return {
    version: 1,
    workflow: { name: "w" },
    nodes
  };
}

describe("setWaitSubscribe", () => {
  it("subscribe 設定時に events / event / next / edges をクリアする", () => {
    // Arrange
    const before = doc([
      {
        name: "w1",
        type: "wait",
        events: { approve: "end" },
        event: "resume",
        next: "end",
        edges: [{ to: "other" }]
      },
      { name: "end", type: "end" }
    ]);

    // Act
    const after = setWaitSubscribe(before, "w1", [
      { topic: "orders.created", key: "$.id", next: "end" }
    ]);

    // Assert
    expect(after.nodes.find((node) => node.name === "w1")).toEqual({
      name: "w1",
      type: "wait",
      subscribe: [{ topic: "orders.created", key: "$.id", next: "end" }]
    });
  });

  it("Wait 以外のノードは変更しない", () => {
    // Arrange
    const before = doc([{ name: "a", type: "action", action: "noop" }]);

    // Act
    const after = setWaitSubscribe(before, "a", [{ topic: "t", next: "e" }]);

    // Assert
    expect(after).toBe(before);
  });
});

describe("connectWaitSubscribeTarget", () => {
  it("空 next の先頭行を埋める", () => {
    // Arrange
    const before = doc([
      {
        name: "w1",
        type: "wait",
        subscribe: [
          { topic: "a", next: "" },
          { topic: "b", next: "" }
        ]
      },
      { name: "ok", type: "end" }
    ]);

    // Act
    const after = connectWaitSubscribeTarget(before, "w1", "ok");

    // Assert
    expect(after.nodes.find((node) => node.name === "w1")?.subscribe).toEqual([
      { topic: "a", next: "ok" },
      { topic: "b", next: "" }
    ]);
  });

  it("空 next が無ければ行を末尾追加する", () => {
    // Arrange
    const before = doc([
      {
        name: "w1",
        type: "wait",
        subscribe: [{ topic: "a", next: "ok" }]
      },
      { name: "ok", type: "end" }
    ]);

    // Act
    const after = connectWaitSubscribeTarget(before, "w1", "ok");

    // Assert
    expect(after.nodes.find((node) => node.name === "w1")?.subscribe).toEqual([
      { topic: "a", next: "ok" },
      { topic: "", next: "ok" }
    ]);
  });
});

describe("addWaitSubscribeRow / removeWaitSubscribeRow / setWaitSubscribeTarget", () => {
  it("行追加・削除・遷移先更新ができる", () => {
    // Arrange
    const before = doc([
      { name: "w1", type: "wait", subscribe: [{ topic: "a", next: "ok" }] },
      { name: "ok", type: "end" }
    ]);

    // Act
    const added = addWaitSubscribeRow(before, "w1");
    const updated = setWaitSubscribeTarget(added, "w1", 1, "ok");
    const removed = removeWaitSubscribeRow(updated, "w1", 0);

    // Assert
    expect(added.nodes[0]?.subscribe).toEqual([
      { topic: "a", next: "ok" },
      { topic: "", next: "" }
    ]);
    expect(updated.nodes[0]?.subscribe?.[1]).toEqual({ topic: "", next: "ok" });
    expect(removed.nodes[0]?.subscribe).toEqual([{ topic: "", next: "ok" }]);
  });
});

describe("switchWaitMode", () => {
  it("Signal から Subscribe へ切り替えると events を破棄する", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { approve: "ok" } }]);

    // Act
    const after = switchWaitMode(before, "w1", "subscribe");

    // Assert
    expect(after.nodes[0]).toEqual({
      name: "w1",
      type: "wait",
      subscribe: [{ topic: "", next: "" }]
    });
  });

  it("Subscribe から Signal へ切り替えると subscribe を破棄する", () => {
    // Arrange
    const before = doc([
      { name: "w1", type: "wait", subscribe: [{ topic: "orders", next: "ok" }] }
    ]);

    // Act
    const after = switchWaitMode(before, "w1", "events");

    // Assert
    expect(after.nodes[0]).toEqual({
      name: "w1",
      type: "wait",
      events: { resume: "" }
    });
  });
});
