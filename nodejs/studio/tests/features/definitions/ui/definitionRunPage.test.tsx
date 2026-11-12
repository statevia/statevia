import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { DefinitionRunStartPage } from "@/features/definitions/ui/DefinitionRunStartPage";
import { renderWithUiText, testProjectId } from "@/tests/testUtils";

const routerPush = vi.hoisted(() => vi.fn());

vi.mock("next/navigation", () => ({
  useParams: () => ({ definitionId: "def-1" }),
  useRouter: () => ({ push: routerPush })
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn(), apiPost: vi.fn() };
});

import { apiGet, apiPost } from "@/shared/api";

describe("DefinitionRunStartPage", () => {
  beforeEach(() => {
    routerPush.mockClear();
    vi.mocked(apiGet).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Demo",
      projectId: testProjectId,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z",
    });
    vi.mocked(apiPost).mockResolvedValue({
      displayId: "ex-new",
      resourceId: "res-new",
      graphId: "g-1",
      status: "ACTIVE",
      startedAt: "2026-01-01T00:00:00Z",
      cancelRequested: false,
      restartLost: false
    });
  });

  it("実行開始ボタンで API を呼ぶ", async () => {
    renderWithUiText(<DefinitionRunStartPage />);

    fireEvent.click(await screen.findByRole("button", { name: "実行開始" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/executions", { definitionId: "def-1" });
    });
  });

  it("開始後は実行詳細へ移る", async () => {
    // Arrange
    renderWithUiText(<DefinitionRunStartPage />);

    // Act
    fireEvent.click(await screen.findByRole("button", { name: "実行開始" }));

    // Assert
    await waitFor(() => {
      expect(routerPush).toHaveBeenCalledWith("/executions/ex-new");
    });
  });

  it("executions.write が無いときは開始フォームを出さず理由を示す", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionRunStartPage />, { permissions: ["executions.read"] });

    // Assert
    expect(
      await screen.findByText("実行開始には、所属グループに executions.write 権限が必要です。テナント管理者に依頼してください。"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "実行開始" })).not.toBeInTheDocument();
  });
});
