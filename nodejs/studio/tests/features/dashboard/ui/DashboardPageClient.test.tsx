import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { DashboardPageClient } from "@/features/dashboard/ui/DashboardPageClient";
import { renderWithUiText, testProjectId } from "@/tests/testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn() };
});

vi.mock("@/shared/project/projectContext", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/project/projectContext")>();
  return { ...actual, useProject: vi.fn(actual.useProject) };
});

import { apiGet } from "@/shared/api";
import { useProject, type ProjectContextValue } from "@/shared/project/projectContext";
import type { ProjectSummary } from "@/shared/project/types";
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

/** 件数の応答。 */
function counts(byStatus: Partial<Record<"ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED" | "UNKNOWN", number>>, waiting = 0) {
  return { byStatus: { ACTIVE: 0, COMPLETED: 0, FAILED: 0, CANCELED: 0, UNKNOWN: 0, ...byStatus }, waiting };
}

/**
 * パスごとに応答を返す apiGet のモック。
 * @param responses 直近・要対応・件数（期間なし）・件数（24 時間）の応答。
 */
function mockDashboardApi(responses: {
  recent?: unknown;
  attention?: unknown;
  countsAll?: unknown;
  countsRecent?: unknown;
}) {
  vi.mocked(apiGet).mockImplementation(async (path: string) => {
    if (path.startsWith("/executions/counts")) {
      const value = path.includes("startedFrom=") ? responses.countsRecent : responses.countsAll;
      if (value instanceof Error) throw value;
      return value ?? counts({});
    }
    const value = path.includes("needsAttention=true") ? responses.attention : responses.recent;
    if (value instanceof Error) throw value;
    return value ?? { items: [], totalCount: 0 };
  });
}

describe("DashboardPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    mockDashboardApi({});
    vi.mocked(useProject).mockReset();
  });

  it("選択中 Project の projectId を付けて、直近・要対応・件数（期間なしと 24 時間）を取得する", async () => {
    // Arrange & Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledTimes(4);
    });
    expect(apiGet).toHaveBeenCalledWith(`/executions?limit=10&offset=0&projectId=${testProjectId}`);
    expect(apiGet).toHaveBeenCalledWith(`/executions?limit=5&offset=0&needsAttention=true&projectId=${testProjectId}`);
    expect(apiGet).toHaveBeenCalledWith(`/executions/counts?projectId=${testProjectId}`);
    expect(apiGet).toHaveBeenCalledWith(expect.stringContaining(`/executions/counts?projectId=${testProjectId}&startedFrom=`));
  });

  it("件数のタイルに実行中・待機中・失敗と完了（24 時間）と成功率を出す", async () => {
    // Arrange
    mockDashboardApi({ countsAll: counts({ ACTIVE: 3 }, 2), countsRecent: counts({ FAILED: 1, COMPLETED: 3 }) });

    // Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    const tiles = await screen.findByRole("list", { name: "実行の件数" });
    expect(within(tiles).getAllByRole("listitem").map((tile) => tile.textContent)).toEqual([
      "実行中3",
      "待機中2イベントを待っています",
      "失敗（24 時間）1",
      "完了（24 時間）3成功率 75%",
    ]);
  });

  it("対応が必要な実行を並べ、すべて見るは実行一覧の要対応へリンクする", async () => {
    // Arrange
    mockDashboardApi({
      attention: { items: [{ displayId: "ExW1", status: "ACTIVE", startedAt: "2026-11-14T09:00:00Z", waiting: true }], totalCount: 1 },
    });

    // Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    const list = await screen.findByRole("list", { name: "対応が必要な実行" });
    expect(within(list).getByRole("link", { name: "ExW1" })).toHaveAttribute("href", "/executions/ExW1");
    expect(within(list).getByText("待機中")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "すべて見る" })).toHaveAttribute("href", "/executions?needsAttention=true");
  });

  it("件数と要対応の取得に失敗しても、直近の実行は出す", async () => {
    // Arrange
    mockDashboardApi({
      recent: { items: [{ displayId: "ExA1", status: "ACTIVE", startedAt: "2026-11-14T09:00:00Z", waiting: false }], totalCount: 1 },
      attention: new Error("attention"),
      countsAll: new Error("counts"),
      countsRecent: new Error("counts"),
    });

    // Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    expect(await screen.findByRole("link", { name: "ExA1" })).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "実行の件数" })).not.toBeInTheDocument();
    expect(screen.queryByText("対応が必要な実行")).not.toBeInTheDocument();
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
      expect(apiGet).toHaveBeenCalledWith("/executions?limit=10&offset=0&projectId=project-b");
    });
  });

  it("直近の実行は実行 ID から詳細へリンクする", async () => {
    // Arrange
    mockDashboardApi({
      recent: { items: [{ displayId: "ExA1", status: "ACTIVE", startedAt: "2026-11-14T09:00:00Z", waiting: false }], totalCount: 1 },
    });

    // Act
    renderWithUiText(<DashboardPageClient />);

    // Assert
    expect(await screen.findByRole("link", { name: "ExA1" })).toHaveAttribute("href", "/executions/ExA1");
  });

  it("再読み込みのボタンで直近実行を取り直す", async () => {
    // Arrange
    mockDashboardApi({
      recent: { items: [{ displayId: "ExA1", status: "ACTIVE", startedAt: "2026-11-14T09:00:00Z", waiting: false }], totalCount: 1 },
    });
    renderWithUiText(<DashboardPageClient />);
    await screen.findByRole("link", { name: "ExA1" });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "再読み込み" }));

    // Assert
    await waitFor(() => expect(apiGet).toHaveBeenCalledTimes(8));
  });
});
