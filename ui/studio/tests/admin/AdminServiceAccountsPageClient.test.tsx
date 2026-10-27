import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminServiceAccountsPageClient } from "@/features/admin/ui/AdminServiceAccountsPageClient";
import * as api from "@/shared/api";
import { renderWithUiText } from "../testUtils";

vi.mock("@/shared/api", () => ({
  apiGet: vi.fn(),
  apiPatch: vi.fn(),
  apiPost: vi.fn()
}));

describe("AdminServiceAccountsPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("一覧に名前・有効・キーの有無・グループ数と詳細へのリンクを出す", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockResolvedValue([
      {
        serviceAccountId: "sa-1",
        principalId: "pr-1",
        name: "nightly-runner",
        isActive: false,
        hasApiKey: true,
        groupIds: ["g-1", "g-2"],
        createdAt: "2026-10-01T00:00:00Z"
      }
    ]);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    expect(await screen.findByText("nightly-runner")).toBeInTheDocument();
    expect(screen.getByText(/無効 · API キーあり · グループ 2 件/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "詳細" })).toHaveAttribute("href", "/admin/service-accounts/sa-1");
    expect(api.apiGet).toHaveBeenCalledTimes(1);
    expect(api.apiGet).toHaveBeenCalledWith("/admin/service-accounts");
  });

  it("0 件なら空状態を出す", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockResolvedValue([]);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    expect(await screen.findByText("ServiceAccount はまだありません。")).toBeInTheDocument();
  });

  it("一覧に削除操作を出さない", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockResolvedValue([
      {
        serviceAccountId: "sa-1",
        principalId: "pr-1",
        name: "nightly-runner",
        isActive: true,
        hasApiKey: false,
        groupIds: ["g-1"],
        createdAt: "2026-10-01T00:00:00Z"
      }
    ]);

    // Act
    renderWithUiText(<AdminServiceAccountsPageClient />);

    // Assert
    await screen.findByText("nightly-runner");
    expect(screen.queryByRole("button", { name: /削除|失効/ })).not.toBeInTheDocument();
  });

  it("取得に失敗したら空一覧ではなくエラーを出し、再試行で取り直す", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockRejectedValueOnce(new Error("forbidden")).mockResolvedValueOnce([]);
    renderWithUiText(<AdminServiceAccountsPageClient />);
    const retry = await screen.findByRole("button", { name: "再試行" });

    // Act
    fireEvent.click(retry);

    // Assert
    expect(await screen.findByText("ServiceAccount はまだありません。")).toBeInTheDocument();
    await waitFor(() => {
      expect(api.apiGet).toHaveBeenCalledTimes(2);
    });
  });
});
