import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { DashboardPageClient } from "@/features/dashboard/ui/DashboardPageClient";
import { renderWithUiText, testProjectId } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn() };
});

vi.mock("@/features/projects/projectContext", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/projects/projectContext")>();
  return { ...actual, useProject: vi.fn(actual.useProject) };
});

import { apiGet } from "@/shared/api";
import { useProject, type ProjectContextValue } from "@/features/projects/projectContext";
import type { ProjectSummary } from "@/features/projects/types";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";

/**
 * 選択中 Project を固定したコンテキスト。
 * @param selectedProject 選択中 Project。null は未選択。
 * @param loaded 所属一覧の取得が終わったか。
 * @returns コンテキスト値。
 */
function projectContext(selectedProject: ProjectSummary | null, loaded = true): ProjectContextValue {
  return {
    projects: selectedProject ? [selectedProject] : [],
    selectedProject,
    loaded,
    selectProject: vi.fn(),
    roleOf: () => null,
    reload: () => Promise.resolve(true),
  };
}

/**
 * 所属 Project の 1 件。
 * @param projectId Project UUID。
 * @returns 画面用の Project。
 */
function project(projectId: string): ProjectSummary {
  return {
    projectId,
    slug: projectId,
    displayName: projectId,
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role: "reader",
  };
}

describe("DashboardPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 0 });
    vi.mocked(useProject).mockReset();
  });

  it("選択中 Project の projectId を付けて直近実行を取得する", async () => {
    // Arrange & Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith(`/executions?limit=10&offset=0&projectId=${testProjectId}`);
    });
    expect(apiGet).toHaveBeenCalledTimes(1);
  });

  it("所属 Project が無ければ取得せず空表示にする", async () => {
    // Arrange & Act
    renderWithUiText(<DashboardPageClient />, { projects: [] });

    // Assert
    expect(await screen.findByText("定義一覧または実行一覧から操作を開始できます。")).toBeInTheDocument();
    expect(apiGet).not.toHaveBeenCalled();
  });

  it("所属一覧の取得前は直近実行を取得しない", () => {
    // Arrange
    vi.mocked(useProject).mockReturnValue(projectContext(null, false));

    // Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    expect(apiGet).not.toHaveBeenCalled();
  });

  it("Project を切り替えると新しい Project で取り直す", async () => {
    // Arrange
    vi.mocked(useProject).mockReturnValue(projectContext(project("project-a")));
    const wrapper = ({ children }: { children: ReactNode }) => <UiTextProvider locale="ja">{children}</UiTextProvider>;
    const { rerender } = render(<DashboardPageClient />, { wrapper });
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/executions?limit=10&offset=0&projectId=project-a");
    });

    // Act
    vi.mocked(useProject).mockReturnValue(projectContext(project("project-b")));
    rerender(<DashboardPageClient />);

    // Assert
    await waitFor(() => {
      expect(apiGet).toHaveBeenLastCalledWith("/executions?limit=10&offset=0&projectId=project-b");
    });
  });
});
