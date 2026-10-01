import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { ExecutionsPageClient } from "../../features/executions/ui/ExecutionsPageClient";
import { renderWithUiText } from "../testUtils";

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
  useRouter: () => ({ push: vi.fn(), replace }),
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

import { apiGet, apiPost } from "@/shared/api";

describe("ExecutionsPageClient", () => {
  beforeEach(() => {
    replace.mockClear();
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
});
