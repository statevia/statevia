import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminServiceAccountResourceGrantsPageClient } from "@/features/admin/ui/AdminServiceAccountResourceGrantsPageClient";
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

describe("AdminServiceAccountResourceGrantsPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/service-accounts/sa%2F1/resource-grants") return { projectIds: [], definitionIds: [] };
      if (path === "/admin/projects") return [];
      if (path.startsWith("/definitions?")) return { items: [], totalCount: 0, offset: 0, limit: 500, hasMore: false };
      throw new Error(`unexpected path: ${path}`);
    });
  });

  it("ServiceAccount の許可を ServiceAccount 用のパスで読み、詳細へ戻るリンクを出す", async () => {
    // Arrange
    renderWithUiText(<AdminServiceAccountResourceGrantsPageClient serviceAccountId="sa/1" />);

    // Act
    await screen.findByRole("button", { name: "保存" });

    // Assert
    expect(screen.getByRole("heading", { name: "実行リソース許可" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ServiceAccount 詳細へ戻る" })).toHaveAttribute(
      "href",
      "/admin/service-accounts/sa%2F1"
    );
    expect(api.apiGet).toHaveBeenCalledWith("/admin/service-accounts/sa%2F1/resource-grants");
  });
});
