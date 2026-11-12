import { describe, expect, it, vi } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { YamlCodeEditor } from "@/features/definition-editor/ui/YamlCodeEditor";

describe("YamlCodeEditor", () => {
  it("YAML 文字列を編集できる", async () => {
    const onChange = vi.fn();
    const { container } = render(
      <YamlCodeEditor value="version: 1" onChange={onChange} completionKeywords={["version"]} ariaLabel="YAML エディタ" />
    );

    await waitFor(() => {
      expect(container.querySelector(".cm-editor")).toBeTruthy();
    });
  });

  it("渡した aria-label をエディタに付ける", () => {
    // Arrange
    const ariaLabel = "YAML エディタ";

    // Act
    const { container } = render(
      <YamlCodeEditor value="version: 1" onChange={vi.fn()} completionKeywords={["version"]} ariaLabel={ariaLabel} />
    );

    // Assert
    expect(container.querySelector(`[aria-label="${ariaLabel}"]`)).toBeTruthy();
  });
});
