import { screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { renderWithUiText } from "../testUtils";

vi.mock("@/features/projects/ui/ProjectMembersPanel", () => ({
  ProjectMembersPanel: ({ projectId }: { projectId: string }) => <p>members-panel:{projectId}</p>,
}));

vi.mock("@/features/projects/projectContext", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/projects/projectContext")>();
  return { ...actual, useProject: vi.fn(actual.useProject) };
});

import { useProject, type ProjectContextValue } from "@/features/projects/projectContext";
import type { ProjectSummary } from "@/features/projects/types";
import { ProjectMembersPageClient } from "@/features/projects/ui/ProjectMembersPageClient";

/**
 * 所属一覧の 1 件。
 * @param role 実効ロール。
 * @returns 画面用の Project。
 */
function project(role: ProjectSummary["role"]): ProjectSummary {
  return {
    projectId: "project-1",
    slug: "default",
    displayName: "Default",
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role,
  };
}

describe("ProjectMembersPageClient", () => {
  it("所属一覧の取得前は読み込み中を出す", () => {
    // Arrange
    const loading: ProjectContextValue = {
      projects: [],
      selectedProject: null,
      loaded: false,
      selectProject: vi.fn(),
      roleOf: () => null,
      reload: () => Promise.resolve(),
    };
    vi.mocked(useProject).mockReturnValueOnce(loading);

    // Act
    renderWithUiText(<ProjectMembersPageClient />);

    // Assert
    expect(screen.getByText("ローディング...")).toBeInTheDocument();
    expect(screen.queryByText(/members-panel/)).not.toBeInTheDocument();
  });

  it("所属が無ければ未所属の案内を出す", () => {
    // Arrange & Act
    renderWithUiText(<ProjectMembersPageClient />, { projects: [] });

    // Assert
    expect(screen.getByText("所属しているプロジェクトがありません。")).toBeInTheDocument();
  });

  it("選択中 Project が admin 未満ならメンバー操作を出さない", () => {
    // Arrange & Act
    renderWithUiText(<ProjectMembersPageClient />, { projects: [project("publisher")] });

    // Assert
    expect(screen.getByText("メンバー管理にはプロジェクト管理者ロールが必要です。")).toBeInTheDocument();
    expect(screen.queryByText(/members-panel/)).not.toBeInTheDocument();
  });

  it("選択中 Project が admin ならその Project のメンバーパネルを出す", () => {
    // Arrange & Act
    renderWithUiText(<ProjectMembersPageClient />, { projects: [project("admin")] });

    // Assert
    expect(screen.getByText("members-panel:project-1")).toBeInTheDocument();
  });
});
