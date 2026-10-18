import type { TenantActionBinding } from "@/features/admin/actionBindings";
import { AdminActionBindingsPageClient } from "@/features/admin/ui/AdminActionBindingsPageClient";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithUiText } from "../testUtils";

vi.mock("@/shared/api/client", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiDelete: vi.fn()
}));

import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";

const secretBinding: TenantActionBinding = {
  name: "order-db",
  kind: "secret",
  allowedActionIds: ["acme.order.query"],
  updatedAt: "2026-06-01T00:00:00Z",
  valueConfigured: true
};

const variableBinding: TenantActionBinding = {
  name: "page-size",
  kind: "variable",
  allowedActionIds: ["acme.order.query", "acme.order.other"],
  updatedAt: "2026-06-02T00:00:00Z",
  value: "25"
};

/**
 * 見出しを含む form を返す。
 * @param name 見出しの表示名。
 * @returns その見出しを含む form。
 */
function formByHeading(name: string): HTMLElement {
  const form = screen.getByRole("heading", { name }).closest("form");
  if (!(form instanceof HTMLElement)) {
    throw new Error(`form not found: ${name}`);
  }
  return form;
}

describe("AdminActionBindingsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPost).mockReset();
    vi.mocked(apiPut).mockReset();
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiGet).mockResolvedValue([]);
  });

  it("空の一覧と作成フォームを表示する", async () => {
    renderWithUiText(<AdminActionBindingsPageClient />);

    expect(await screen.findByRole("heading", { name: "Action バインディング" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "バインディングを作成" })).toBeInTheDocument();
    expect(screen.getByText("バインディングはまだありません。")).toBeInTheDocument();
    expect(apiGet).toHaveBeenCalledWith("/admin/action-bindings");
  });

  it("読み込みに失敗するとメッセージを出す", async () => {
    vi.mocked(apiGet).mockRejectedValueOnce(new Error("load failed"));

    renderWithUiText(<AdminActionBindingsPageClient />);

    expect(await screen.findByText("load failed")).toBeInTheDocument();
  });

  it("読み込みが Error 以外で失敗すると定型メッセージを出す", async () => {
    vi.mocked(apiGet).mockRejectedValueOnce("down");

    renderWithUiText(<AdminActionBindingsPageClient />);

    expect(await screen.findByText("Failed to load action bindings.")).toBeInTheDocument();
  });

  it("種別が変数のとき値はテキスト、それ以外はパスワードにする", async () => {
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByText("バインディングはまだありません。");
    const value = screen.getByLabelText("値");

    fireEvent.change(screen.getByLabelText("種別"), { target: { value: "variable" } });
    expect(value).toHaveAttribute("type", "text");

    fireEvent.change(screen.getByLabelText("種別"), { target: { value: "other" } });
    expect(value).toHaveAttribute("type", "password");
  });

  it("バインディングを作成し、送信中は作成中と表示する", async () => {
    const listed: TenantActionBinding[] = [];
    vi.mocked(apiGet).mockImplementation(async () => [...listed]);
    let resolvePost: (binding: TenantActionBinding) => void = () => undefined;
    vi.mocked(apiPost).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolvePost = (binding) => {
            listed.push(binding);
            resolve(binding);
          };
        })
    );
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByText("バインディングはまだありません。");

    fireEvent.change(screen.getByLabelText("名前"), { target: { value: " order-db " } });
    fireEvent.change(screen.getByLabelText("値"), { target: { value: "secret-value" } });
    fireEvent.change(screen.getByLabelText(/^許可する Action/), {
      target: { value: " acme.order.query \n\nacme.order.other" }
    });
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    expect(await screen.findByRole("button", { name: "作成中…" })).toBeDisabled();
    resolvePost(secretBinding);

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/admin/action-bindings", {
        name: "order-db",
        kind: "secret",
        value: "secret-value",
        allowedActionIds: ["acme.order.query", "acme.order.other"]
      });
    });
    expect(await screen.findByRole("heading", { name: "order-db" })).toBeInTheDocument();
    expect(screen.getByText("設定済み")).toBeInTheDocument();
    expect(screen.getByText("更新: 2026-06-01T00:00:00Z")).toBeInTheDocument();
    expect(screen.getByLabelText("名前")).toHaveValue("");
  });

  it("作成に失敗するとメッセージを残す", async () => {
    vi.mocked(apiPost).mockRejectedValueOnce(new Error("create failed"));
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByText("バインディングはまだありません。");

    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "order-db" } });
    fireEvent.change(screen.getByLabelText("値"), { target: { value: "secret-value" } });
    fireEvent.change(screen.getByLabelText(/^許可する Action/), { target: { value: "acme.order.query" } });
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    expect(await screen.findByText("create failed")).toBeInTheDocument();
    expect(screen.getByLabelText("名前")).toHaveValue("order-db");
  });

  it("作成が Error 以外で失敗すると定型メッセージを出す", async () => {
    vi.mocked(apiPost).mockRejectedValueOnce("down");
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByText("バインディングはまだありません。");

    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "order-db" } });
    fireEvent.change(screen.getByLabelText("値"), { target: { value: "secret-value" } });
    fireEvent.change(screen.getByLabelText(/^許可する Action/), { target: { value: "acme.order.query" } });
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    expect(await screen.findByText("Failed to create action binding.")).toBeInTheDocument();
  });

  it("変数は保存済みの値を送り、シークレットの空欄は値を省く", async () => {
    vi.mocked(apiGet).mockResolvedValue([variableBinding, secretBinding]);
    vi.mocked(apiPut).mockResolvedValue(variableBinding);
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "page-size" });

    const variableForm = formByHeading("page-size");
    expect(within(variableForm).getByLabelText("値")).toHaveValue("25");
    expect(within(variableForm).queryByText("設定済み")).not.toBeInTheDocument();
    fireEvent.click(within(variableForm).getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/action-bindings/page-size", {
        kind: "variable",
        allowedActionIds: ["acme.order.query", "acme.order.other"],
        value: "25"
      });
    });

    const secretForm = formByHeading("order-db");
    expect(within(secretForm).getByLabelText(/^値/)).toHaveValue("");
    expect(within(secretForm).getByText("シークレットの値は保存後に再表示しません。空のまま更新すると、今の値を維持します。")).toBeInTheDocument();
    fireEvent.click(within(secretForm).getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/action-bindings/order-db", {
        kind: "secret",
        allowedActionIds: ["acme.order.query"]
      });
    });
  });

  it("入力した下書きを保存し、変数の値を空にできる", async () => {
    vi.mocked(apiGet).mockResolvedValue([variableBinding, secretBinding]);
    vi.mocked(apiPut).mockResolvedValue(secretBinding);
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "order-db" });

    const variableForm = formByHeading("page-size");
    fireEvent.change(within(variableForm).getByLabelText("値"), { target: { value: "" } });
    fireEvent.change(within(variableForm).getByLabelText("許可する Action"), {
      target: { value: "acme.order.query" }
    });
    fireEvent.click(within(variableForm).getByRole("button", { name: "保存" }));
    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/action-bindings/page-size", {
        kind: "variable",
        allowedActionIds: ["acme.order.query"],
        value: ""
      });
    });

    const secretForm = formByHeading("order-db");
    fireEvent.change(within(secretForm).getByLabelText(/^値/), { target: { value: "next-secret" } });
    fireEvent.click(within(secretForm).getByRole("button", { name: "保存" }));
    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/action-bindings/order-db", {
        kind: "secret",
        allowedActionIds: ["acme.order.query"],
        value: "next-secret"
      });
    });
  });

  it("更新に失敗するとメッセージを出す", async () => {
    vi.mocked(apiGet).mockResolvedValue([secretBinding]);
    vi.mocked(apiPut).mockRejectedValueOnce(new Error("update failed"));
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "order-db" });

    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(await screen.findByText("update failed")).toBeInTheDocument();
  });

  it("更新が Error 以外で失敗すると定型メッセージを出す", async () => {
    vi.mocked(apiGet).mockResolvedValue([secretBinding]);
    vi.mocked(apiPut).mockRejectedValueOnce("down");
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "order-db" });

    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(await screen.findByText("Failed to update action binding.")).toBeInTheDocument();
  });

  it("バインディングを削除し、削除中は削除中と表示する", async () => {
    const listed: TenantActionBinding[] = [secretBinding];
    vi.mocked(apiGet).mockImplementation(async () => [...listed]);
    let resolveDelete: () => void = () => undefined;
    vi.mocked(apiDelete).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveDelete = () => {
            listed.splice(0, listed.length);
            resolve();
          };
        })
    );
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "order-db" });

    fireEvent.click(screen.getByRole("button", { name: "削除" }));

    expect(await screen.findByRole("button", { name: "削除中…" })).toBeDisabled();
    resolveDelete();

    await waitFor(() => {
      expect(apiDelete).toHaveBeenCalledWith("/admin/action-bindings/order-db");
    });
    expect(await screen.findByText("バインディングはまだありません。")).toBeInTheDocument();
  });

  it("削除に失敗するとメッセージを出す", async () => {
    vi.mocked(apiGet).mockResolvedValue([secretBinding]);
    vi.mocked(apiDelete).mockRejectedValueOnce(new Error("delete failed"));
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "order-db" });

    fireEvent.click(screen.getByRole("button", { name: "削除" }));

    expect(await screen.findByText("delete failed")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "order-db" })).toBeInTheDocument();
  });

  it("削除が Error 以外で失敗すると定型メッセージを出す", async () => {
    vi.mocked(apiGet).mockResolvedValue([secretBinding]);
    vi.mocked(apiDelete).mockRejectedValueOnce("down");
    renderWithUiText(<AdminActionBindingsPageClient />);
    await screen.findByRole("heading", { name: "order-db" });

    fireEvent.click(screen.getByRole("button", { name: "削除" }));

    expect(await screen.findByText("Failed to delete action binding.")).toBeInTheDocument();
  });
});
