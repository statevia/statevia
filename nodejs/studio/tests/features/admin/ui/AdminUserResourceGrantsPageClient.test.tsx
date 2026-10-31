import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminUserResourceGrantsPageClient } from "@/features/admin/ui/AdminUserResourceGrantsPageClient";
import * as api from "@/shared/api";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPut: vi.fn()
  };
});

describe("AdminUserResourceGrantsPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/users/user%2F1/resource-grants") return { projectIds: [], definitionIds: [] };
      if (path === "/admin/projects") return [];
      if (path.startsWith("/definitions?")) return { items: [], totalCount: 0, offset: 0, limit: 500, hasMore: false };
      throw new Error(`unexpected path: ${path}`);
    });
  });

  it("ユーザーの許可をユーザー用のパスで読み、一覧へ戻るリンクを出す", async () => {
    // Arrange
    renderWithUiText(<AdminUserResourceGrantsPageClient userId="user/1" />);

    // Act
    await screen.findByRole("button", { name: "保存" });

    // Assert
    expect(screen.getByRole("heading", { name: "実行リソース許可" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ユーザー一覧へ戻る" })).toHaveAttribute("href", "/admin/users");
    expect(api.apiGet).toHaveBeenCalledWith("/admin/users/user%2F1/resource-grants");
  });
});
