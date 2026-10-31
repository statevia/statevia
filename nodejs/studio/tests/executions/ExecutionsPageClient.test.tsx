import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { ExecutionsPageClient } from "@/features/executions/ui/ExecutionsPageClient";
import { renderWithUiText } from "@/tests/testUtils";

const push = vi.fn();
const replace = vi.fn();
const searchParams = new URLSearchParams("limit=20&offset=0");

const runningExecution = {
  displayId: "ex-1",
  resourceId: "res-1",
  graphId: "g-1",
  status: "Running",
  startedAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false
};

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace }),
  useSearchParams: () => searchParams
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPost: vi.fn()
  };
});

vi.mock("@/features/executions/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/executions/api")>();
  return {
    ...actual,
    buildExecutionsListPath: vi.fn(() => "/executions?limit=20&offset=0")
  };
});

vi.mock("@/features/projects/projectContext", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/projects/projectContext")>();
  return { ...actual, useProject: vi.fn(actual.useProject) };
});

import { apiGet, apiPost } from "@/shared/api";
import { buildExecutionsListPath } from "@/features/executions/api";
import { useProject } from "@/features/projects/projectContext";
import { testProjectId } from "@/tests/testUtils";

describe("ExecutionsPageClient", () => {
  beforeEach(() => {
    replace.mockClear();
    vi.mocked(apiGet).mockClear();
    vi.mocked(buildExecutionsListPath).mockClear();
    vi.mocked(useProject).mockReset();
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path.startsWith("/event-subscriptions")) {
        return { subscriptions: [{ topic: "orders.updated", key: "sku" }] };
      }
      return { items: [], totalCount: 0 };
    });
    vi.mocked(apiPost).mockResolvedValue(null);
  });

  it("実行一覧を読み込む", async () => {
    renderWithUiText(<ExecutionsPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });
    expect(apiGet).toHaveBeenCalledWith("/executions?limit=20&offset=0");
  });

  it("選択中 Project の projectId で一覧を取得する", async () => {
    // Arrange & Act
    renderWithUiText(<ExecutionsPageClient />);

    // Assert
    await waitFor(() => expect(apiGet).toHaveBeenCalledWith("/executions?limit=20&offset=0"));
    expect(buildExecutionsListPath).toHaveBeenCalledWith(expect.objectContaining({ projectId: testProjectId }));
  });

  it("所属一覧の取得前は一覧を取得しない", () => {
    // Arrange
    vi.mocked(useProject).mockReturnValue({
      projects: [],
      selectedProject: null,
      loaded: false,
      selectProject: vi.fn(),
      roleOf: () => null,
      reload: () => Promise.resolve(true),
    });

    // Act
    renderWithUiText(<ExecutionsPageClient />);

    // Assert
    expect(buildExecutionsListPath).not.toHaveBeenCalled();
    expect(apiGet).not.toHaveBeenCalledWith("/executions?limit=20&offset=0");
  });

  it("所属 Project が無ければ一覧を取得せず空にする", async () => {
    // Arrange & Act
    renderWithUiText(<ExecutionsPageClient />, { projects: [] });

    // Assert
    await waitFor(() => expect(screen.queryByText("実行一覧を読み込み中です。")).not.toBeInTheDocument());
    expect(buildExecutionsListPath).not.toHaveBeenCalled();
    expect(apiGet).not.toHaveBeenCalledWith("/executions?limit=20&offset=0");
  });

  it("フィルタ送信で URL を更新する", async () => {
    renderWithUiText(<ExecutionsPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    const nameInput = screen.getByLabelText(/name（execution/);
    fireEvent.change(nameInput, { target: { value: "demo" } });
    const form = screen.getByRole("button", { name: "検索" }).closest("form");
    if (!(form instanceof HTMLFormElement)) {
      throw new TypeError("検索フォームが見つかりません");
    }
    fireEvent.submit(form);

    await waitFor(() => {
      expect(replace).toHaveBeenCalled();
    });
  });

  it("集合配送は候補も手入力も /events に送り、行には発行ボタンが無い", async () => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path.startsWith("/event-subscriptions")) {
        return {
          subscriptions: [
            { topic: "orders.updated", key: "sku" },
            { topic: "orders.updated", key: "" }
          ]
        };
      }
      return { items: [runningExecution], totalCount: 1 };
    });
    renderWithUiText(<ExecutionsPageClient />);
    await waitFor(() => expect(screen.getByRole("button", { name: "詳細" })).toBeInTheDocument());

    const ingressButton = screen.getByRole("button", { name: "イベントを送信" });
    expect(screen.getByRole("banner")).toContainElement(ingressButton);
    expect(screen.getByRole("listitem").querySelector("button")?.textContent).toBe("詳細");

    fireEvent.click(screen.getByRole("button", { name: "イベントを送信" }));
    const topic = await screen.findByLabelText("topic");
    fireEvent.change(topic, { target: { value: "orders.updated" } });
    fireEvent.change(screen.getByLabelText("key"), { target: { value: "sku" } });
    fireEvent.click(screen.getByRole("button", { name: "送る" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/events", { topic: "orders.updated", key: "sku" });
    });
    expect(vi.mocked(apiPost).mock.calls.some(([path]) => String(path).includes("resume"))).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "イベントを送信" }));
    fireEvent.change(await screen.findByLabelText("topic"), { target: { value: "manual.topic" } });
    fireEvent.change(screen.getByLabelText("key"), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "送る" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/events", { topic: "manual.topic" });
    });
    expect(screen.getByText("受理しました。一致する購読が 0 件でも成功です。")).toBeInTheDocument();
  });

  it("候補リストは入力の直下に出る", async () => {
    renderWithUiText(<ExecutionsPageClient />);
    await waitFor(() => expect(screen.getByRole("button", { name: "イベントを送信" })).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "イベントを送信" }));
    fireEvent.focus(await screen.findByLabelText("topic"));

    const suggestion = screen.getByRole("button", { name: "orders.updated" });
    const list = suggestion.closest("ul");
    expect(list?.className).toContain("left-0");
    expect(list?.className).toContain("top-full");
    expect(list?.className).toContain("w-full");

    fireEvent.mouseDown(suggestion);
    expect(screen.getByLabelText("topic")).toHaveValue("orders.updated");
  });

  it("一覧取得の失敗をトーストで示し、成功後はフィルタと詳細へ進める", async () => {
    vi.mocked(apiGet).mockRejectedValue(new Error("network"));
    const failed = renderWithUiText(<ExecutionsPageClient />);
    expect(await screen.findByText("UNKNOWN: Unknown error")).toBeInTheDocument();
    failed.unmount();

    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path.startsWith("/event-subscriptions")) {
        return { subscriptions: [] };
      }
      return { items: [runningExecution], totalCount: 40 };
    });
    renderWithUiText(<ExecutionsPageClient />);
    expect(await screen.findByRole("button", { name: "詳細" })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("ステータス"), { target: { value: "Failed" } });
    fireEvent.change(screen.getAllByRole("combobox")[1], { target: { value: "displayId" } });
    fireEvent.change(screen.getAllByRole("combobox")[2], { target: { value: "asc" } });
    fireEvent.change(screen.getByLabelText(/定義ID/), { target: { value: "def-1" } });
    fireEvent.click(screen.getAllByRole("button", { name: "次へ" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "クリア" }));
    fireEvent.change(screen.getByLabelText(/name（execution/), { target: { value: "名前" } });
    fireEvent.click(screen.getByRole("button", { name: "検索" }));
    expect(await screen.findByText(/name は半角英数字/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "詳細" }));
    expect(push).toHaveBeenCalledWith("/executions/ex-1");
  });

  it("executions.write が無ければイベント送信ボタンを出さない", async () => {
    // Arrange / Act
    renderWithUiText(<ExecutionsPageClient />, { permissions: ["executions.read"] });

    // Assert
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });
    expect(screen.queryByRole("button", { name: "イベントを送信" })).not.toBeInTheDocument();
  });
});
