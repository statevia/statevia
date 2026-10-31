import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminServiceAccountsPageClient } from "@/features/admin/ui/AdminServiceAccountsPageClient";
import type { AdminGroupListItem, AdminServiceAccount } from "@/features/admin/types";
import * as api from "@/shared/api";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("@/shared/api", () => ({
  apiGet: vi.fn(),
  apiPatch: vi.fn(),
  apiPost: vi.fn()
}));

const serviceAccount: AdminServiceAccount = {
  serviceAccountId: "sa-1",
  principalId: "pr-1",
  name: "nightly-runner",
  isActive: false,
  hasApiKey: true,
  groupIds: ["g-1", "g-2"],
  createdAt: "2026-10-01T00:00:00Z"
};

const group: AdminGroupListItem = {
  groupId: "g-1",
  name: "Operators",
  isSystem: false,
  memberCount: 0,
  permissionCount: 1,
  updatedAt: "2026-10-01T00:00:00Z"
};

/**
 * パスごとに GET の応答を返すモックを入れる。
 * @param serviceAccounts 一覧の応答。関数なら呼ばれるたびに評価する。
 * @param groups グループ一覧の応答。
 */
function mockGets(
  serviceAccounts: () => Promise<AdminServiceAccount[]>,
  groups: () => Promise<AdminGroupListItem[]> = async () => [group]
): void {
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path === "/admin/service-accounts") return serviceAccounts();
    if (path === "/admin/groups") return groups();
    throw new Error(`unexpected path: ${path}`);
  });
}

/**
 * 一覧 GET の回数。
 * @returns `/admin/service-accounts` の呼び出し回数。
 */
function listCallCount(): number {
  return vi.mocked(api.apiGet).mock.calls.filter(([path]) => path === "/admin/service-accounts").length;
}

describe("AdminServiceAccountsPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("一覧に名前・有効・キーの有無・グループ数と詳細へのリンクを出す", async () => {
    // Arrange
    mockGets(async () => [serviceAccount]);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    expect(await screen.findByText("nightly-runner")).toBeInTheDocument();
    expect(screen.getByText(/無効 · API キーあり · グループ 2 件/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "詳細" })).toHaveAttribute("href", "/admin/service-accounts/sa-1");
    expect(listCallCount()).toBe(1);
  });

  it("0 件なら空状態を出す", async () => {
    // Arrange
    mockGets(async () => []);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    expect(await screen.findByText("ServiceAccount はまだありません。")).toBeInTheDocument();
  });

  it("一覧に削除操作を出さない", async () => {
    // Arrange
    mockGets(async () => [serviceAccount]);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    await screen.findByText("nightly-runner");
    expect(screen.queryByRole("button", { name: /削除|失効/ })).not.toBeInTheDocument();
  });

  it("取得に失敗したら空一覧ではなくエラーを出し、再試行で取り直す", async () => {
    // Arrange
    let listCalls = 0;
    mockGets(async () => {
      listCalls += 1;
      if (listCalls === 1) throw new Error("forbidden");
      return [];
    });
    renderWithUiText(<AdminServiceAccountsPageClient />);
    const retry = await screen.findByRole("button", { name: "再試行" });

    // Act
    fireEvent.click(retry);

    // Assert
    expect(await screen.findByText("ServiceAccount はまだありません。")).toBeInTheDocument();
    expect(listCallCount()).toBe(2);
  });

  it("作成フォームに API キーの入力を置かない", async () => {
    // Arrange
    mockGets(async () => []);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    expect(await screen.findByRole("heading", { name: "ServiceAccount を作成" })).toBeInTheDocument();
    expect(screen.getAllByRole("textbox")).toHaveLength(1);
    expect(screen.queryByLabelText(/API キー|有効期限|スコープ/)).not.toBeInTheDocument();
  });

  it("グループを選ばずに作成しても POST しない", async () => {
    // Arrange
    mockGets(async () => []);
    renderWithUiText(<AdminServiceAccountsPageClient />);
    await screen.findByLabelText("Operators");
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "nightly-runner" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    // Assert
    expect(await screen.findByText("グループを 1 件以上選んでください。")).toBeInTheDocument();
    expect(api.apiPost).not.toHaveBeenCalled();
  });

  it("名前が ASCII ラベルでなければ POST しない", async () => {
    // Arrange
    mockGets(async () => []);
    renderWithUiText(<AdminServiceAccountsPageClient />);
    fireEvent.click(await screen.findByLabelText("Operators"));
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "夜間ジョブ" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    // Assert
    expect(await screen.findByText(/名前は英数字のみです/)).toBeInTheDocument();
    expect(api.apiPost).not.toHaveBeenCalled();
  });

  it("名前とグループで作成すると name と groupIds だけを送り、一覧を取り直す", async () => {
    // Arrange
    mockGets(async () => []);
    vi.mocked(api.apiPost).mockResolvedValue({ ...serviceAccount, isActive: true, hasApiKey: false, groupIds: ["g-1"] });
    renderWithUiText(<AdminServiceAccountsPageClient />);
    fireEvent.click(await screen.findByLabelText("Operators"));
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: " nightly-runner " } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    // Assert
    await waitFor(() => {
      expect(api.apiPost).toHaveBeenCalledWith("/admin/service-accounts", { name: "nightly-runner", groupIds: ["g-1"] });
    });
    await waitFor(() => {
      expect(listCallCount()).toBe(2);
    });
    expect(screen.getByLabelText("名前")).toHaveValue("");
  });

  it("サーバーの 422 はフォームに出し、入力を残す", async () => {
    // Arrange
    mockGets(async () => []);
    vi.mocked(api.apiPost).mockRejectedValue({
      status: 422,
      error: { code: "VALIDATION_ERROR", message: "groupIds must contain at least one group." }
    });
    renderWithUiText(<AdminServiceAccountsPageClient />);
    fireEvent.click(await screen.findByLabelText("Operators"));
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "nightly-runner" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    // Assert
    expect(await screen.findByText(/VALIDATION_ERROR - groupIds must contain at least one group\./)).toBeInTheDocument();
    expect(screen.getByLabelText("名前")).toHaveValue("nightly-runner");
    expect(screen.getByLabelText("Operators")).toBeChecked();
  });

  it("グループ一覧を読めなければ作成ボタンを押せない", async () => {
    // Arrange
    mockGets(
      async () => [],
      async () => {
        throw new Error("network");
      }
    );

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    expect(await screen.findByText("グループの読み込みに失敗しました。")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "作成" })).toBeDisabled();
  });
});
