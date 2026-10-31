import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { WaitSubscribeEditor } from "@/features/definition-editor/ui/WaitSubscribeEditor";

const labels = {
  waitSubscribeSectionTitle: "subscribe",
  waitSubscribeTopicLabel: "topic",
  waitSubscribeKeyLabel: "key",
  waitSubscribeNextLabel: "next",
  waitSubscribeAdd: "Add",
  waitSubscribeRemove: "Remove"
};

describe("WaitSubscribeEditor", () => {
  it("購読の一部更新でも既存行の input DOM を維持する（key 安定）", () => {
    // Arrange
    const onEntriesChange = vi.fn();
    const { rerender } = render(
      <WaitSubscribeEditor
        entries={[
          { topic: "orders.created", key: "$.id", next: "ok" },
          { topic: "orders.cancelled", next: "ng" }
        ]}
        labels={labels}
        onEntriesChange={onEntriesChange}
      />
    );
    const topicInput = screen.getByDisplayValue("orders.created");

    // Act
    rerender(
      <WaitSubscribeEditor
        entries={[
          { topic: "orders.created", key: "$.id", next: "ok" },
          { topic: "orders.cancelled", next: "done" }
        ]}
        labels={labels}
        onEntriesChange={onEntriesChange}
      />
    );

    // Assert
    expect(screen.getByDisplayValue("orders.created")).toBe(topicInput);
    expect(screen.getByDisplayValue("done")).toBeInTheDocument();
  });

  it("行追加で onEntriesChange を呼ぶ", () => {
    // Arrange
    const onEntriesChange = vi.fn();
    render(
      <WaitSubscribeEditor
        entries={[{ topic: "orders", next: "ok" }]}
        labels={labels}
        onEntriesChange={onEntriesChange}
      />
    );

    // Act
    fireEvent.click(screen.getByRole("button", { name: labels.waitSubscribeAdd }));

    // Assert
    expect(onEntriesChange).toHaveBeenCalledWith([
      { topic: "orders", next: "ok" },
      { topic: "", next: "" }
    ]);
  });
});
