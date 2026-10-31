import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { DefinitionDetailClient } from "@/features/definitions/ui/DefinitionDetailClient";
import { renderWithUiText, testProjectId } from "@/tests/testUtils";

const pushMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock })
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn(), apiDelete: vi.fn() };
});

import { apiDelete, apiGet } from "@/shared/api";

describe("DefinitionDetailClient", () => {
  beforeEach(() => {
    pushMock.mockReset();
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiGet).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Demo",
      projectId: testProjectId,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });
    vi.mocked(apiDelete).mockResolvedValue(undefined);
  });

  it("定義詳細を読み込んで表示する", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });
    expect(apiGet).toHaveBeenCalledWith("/definitions/def-1");
  });

  it("削除確認後に apiDelete を呼び一覧へ遷移する", async () => {
    // Arrange
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />);
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "定義を削除" }));
    fireEvent.click(screen.getByRole("button", { name: "削除する" }));

    // Assert
    await waitFor(() => {
      expect(apiDelete).toHaveBeenCalledWith("/definitions/def-1");
    });
    expect(pushMock).toHaveBeenCalledWith("/definitions");
  });

  it("executions.write があれば新規実行ボタンを出す", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />);

    // Assert
    expect(await screen.findByRole("button", { name: "新規実行を開始" })).toBeInTheDocument();
  });

  it("Project ロールが足りても executions.write が無ければ新規実行ボタンを出さない", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />, {
      permissions: ["definitions.read", "executions.read"],
    });

    // Assert
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: "新規実行を開始" })).not.toBeInTheDocument();
  });

  it("Project ロールが足りても definitions.write が無ければ編集ボタンを出さない", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />, {
      permissions: ["definitions.read", "executions.read", "executions.write"],
    });

    // Assert
    expect(await screen.findByRole("button", { name: "新規実行を開始" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "定義の編集" })).not.toBeInTheDocument();
  });

  it("definitions.write が無ければ削除ボタンを出さない", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />, {
      permissions: ["definitions.read", "executions.read", "executions.write"],
    });

    // Assert
    expect(await screen.findByRole("button", { name: "新規実行を開始" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "定義を削除" })).not.toBeInTheDocument();
  });
});
