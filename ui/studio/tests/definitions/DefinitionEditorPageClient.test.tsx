import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DefinitionEditorPageClient } from "@/features/definition-editor/ui/DefinitionEditorPageClient";
import { defaultDefinitionYaml } from "@/features/definition-editor/lib/defaultDefinitionYaml";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import { renderWithUiText } from "../testUtils";

const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn() })
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn(), apiPost: vi.fn(), apiPut: vi.fn() };
});

import { apiGet, apiPost, apiPut } from "@/shared/api";

describe("DefinitionEditorPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/definitions/schema/nodes") {
        return {
          schemaVersion: "1",
          nodesVersion: 1,
          schema: { properties: { version: {}, workflow: { properties: { name: {} } }, nodes: { items: { properties: { id: {} } } } } }
        };
      }
      if (path === "/definitions/def-1") {
        return {
          displayId: "def-1",
          resourceId: "res-1",
          name: "Edited",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
          yaml: defaultDefinitionYaml
        };
      }
      throw new Error(`unexpected path: ${path}`);
    });
  });

  it("新規作成モードで YAML エディタを表示する", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/definitions/schema/nodes");
    });
    expect(screen.getByRole("button", { name: "保存" })).toBeInTheDocument();
  });

  it("編集モードで既存定義を読み込む", async () => {
    renderWithUiText(<DefinitionEditorPageClient definitionId="def-1" />);

    await waitFor(() => {
      expect(screen.getByDisplayValue("Edited")).toBeInTheDocument();
    });
    expect(apiGet).toHaveBeenCalledWith("/definitions/def-1");
  });

  it("ロケール変更では編集モードの定義を再取得しない", async () => {
    const { rerender } = render(
      <UiTextProvider locale="ja">
        <DefinitionEditorPageClient definitionId="def-1" />
      </UiTextProvider>
    );

    await waitFor(() => {
      expect(screen.getByDisplayValue("Edited")).toBeInTheDocument();
    });
    const definitionFetchCount = vi
      .mocked(apiGet)
      .mock.calls.filter((call) => call[0] === "/definitions/def-1").length;

    rerender(
      <UiTextProvider locale="en">
        <DefinitionEditorPageClient definitionId="def-1" />
      </UiTextProvider>
    );

    await waitFor(() => {
      expect(screen.getByDisplayValue("Edited")).toBeInTheDocument();
    });
    const afterLocaleChangeCount = vi
      .mocked(apiGet)
      .mock.calls.filter((call) => call[0] === "/definitions/def-1").length;
    expect(afterLocaleChangeCount).toBe(definitionFetchCount);
  });

  it("グラフモードに切り替えられる", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.click(screen.getByRole("button", { name: "Graph" }));

    await waitFor(() => {
      expect(screen.getByText("グラフ編集")).toBeInTheDocument();
    });
  });

  it("編集保存で PUT を呼ぶ", async () => {
    vi.mocked(apiPut).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Edited2",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-02T00:00:00Z"
    });

    renderWithUiText(<DefinitionEditorPageClient definitionId="def-1" />);
    await waitFor(() => expect(screen.getByDisplayValue("Edited")).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "Edited2" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/definitions/def-1", expect.objectContaining({ name: "Edited2" }));
    });
  });

  it("定義名が空のとき保存しない", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(apiPost).not.toHaveBeenCalled();
    expect(screen.getByText(/定義名を入力/)).toBeInTheDocument();
  });

  it("新規保存で POST を呼ぶ", async () => {
    vi.mocked(apiPost).mockResolvedValue({
      displayId: "def-new",
      resourceId: "res-new",
      name: "DemoFlow",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });

    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "DemoFlow" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/definitions", expect.objectContaining({ name: "DemoFlow" }));
    });
  });

  it("定義名の形式が違うときは保存しない", async () => {
    vi.mocked(apiPost).mockClear();
    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "1bad" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(await screen.findByText(/半角英字で開始/)).toBeInTheDocument();
    expect(apiPost).not.toHaveBeenCalled();
  });

  it("422 の詳細をヒントに出す", async () => {
    vi.mocked(apiPut).mockRejectedValue({
      status: 422,
      error: {
        message: "invalid",
        details: [
          { field: "name", message: "bad name" },
          { field: "yaml", message: "bad yaml", state: "start", jsonPath: "$.input", actionId: "noop" },
          "plain detail"
        ]
      }
    });

    renderWithUiText(<DefinitionEditorPageClient definitionId="def-1" />);
    await waitFor(() => expect(screen.getByDisplayValue("Edited")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(await screen.findByText("bad name")).toBeInTheDocument();
    expect(screen.getByText("bad yaml")).toBeInTheDocument();
  });

  it("保存後に詳細へ進める", async () => {
    vi.mocked(apiPost).mockResolvedValue({
      displayId: "def-new",
      resourceId: "res-new",
      name: "DemoFlow",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });

    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());
    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "DemoFlow" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));
    fireEvent.click(await screen.findByRole("button", { name: "新しい定義の詳細へ" }));

    expect(push).toHaveBeenCalledWith("/definitions/def-new");
  });

  it("編集前に戻す", async () => {
    renderWithUiText(<DefinitionEditorPageClient definitionId="def-1" />);
    await waitFor(() => expect(screen.getByDisplayValue("Edited")).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "Edited2" } });
    fireEvent.click(screen.getByRole("button", { name: "編集前に戻す" }));

    expect(screen.getByLabelText(/定義名/i)).toHaveValue("Edited");
  });
});
