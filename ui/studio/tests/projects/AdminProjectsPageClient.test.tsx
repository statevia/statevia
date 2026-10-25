import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithUiText } from "../testUtils";

vi.mock("@/features/projects/api", () => ({
  listAdminProjects: vi.fn(),
}));

vi.mock("@/features/projects/ui/ProjectMembersPanel", () => ({
  ProjectMembersPanel: ({ projectId }: { projectId: string }) => <p>members-panel:{projectId}</p>,
}));

import { listAdminProjects } from "@/features/projects/api";
import type { ProjectSummary } from "@/features/projects/types";
import { AdminProjectsPageClient } from "@/features/projects/ui/AdminProjectsPageClient";

/** API エラー以外の失敗を toToastError が表す文言。 */
const unknownErrorMessage = "UNKNOWN: Unknown error";

/**
 * 管理一覧の 1 件。未所属なら role は null。
 * @param projectId Project UUID。
 * @param displayName 表示名。
 * @returns 画面用の Project。
 */
function project(projectId: string, displayName: string): ProjectSummary {
  return {
    projectId,
    slug: projectId,
    displayName,
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role: null,
  };
}

describe("AdminProjectsPageClient", () => {
  beforeEach(() => {
    vi.mocked(listAdminProjects).mockReset();
  });

  it("取得前は読み込み中を出し、定義を開かない旨を常に示す", () => {
    // Arrange
    vi.mocked(listAdminProjects).mockReturnValue(new Promise<ProjectSummary[]>(() => undefined));

    // Act
    renderWithUiText(<AdminProjectsPageClient />);

    // Assert
    expect(screen.getByText("ローディング...")).toBeInTheDocument();
    expect(screen.getByText("未所属のプロジェクトでは定義の詳細とエディタを開きません。")).toBeInTheDocument();
  });

  it("全 Project を並べ、先頭の Project のメンバーパネルを出す", async () => {
    // Arrange
    vi.mocked(listAdminProjects).mockResolvedValue([project("alpha", "Alpha"), project("beta", "Beta")]);

    // Act
    renderWithUiText(<AdminProjectsPageClient />);

    // Assert
    expect(await screen.findByText("members-panel:alpha")).toBeInTheDocument();
    const options = Array.from(screen.getByRole("combobox").querySelectorAll("option")).map(
      (option) => option.textContent,
    );
    expect(options).toEqual(["Alpha", "Beta"]);
  });

  it("Project を切り替えるとそのメンバーパネルに替わる", async () => {
    // Arrange
    vi.mocked(listAdminProjects).mockResolvedValue([project("alpha", "Alpha"), project("beta", "Beta")]);
    renderWithUiText(<AdminProjectsPageClient />);
    await screen.findByText("members-panel:alpha");

    // Act
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "beta" } });

    // Assert
    expect(screen.getByText("members-panel:beta")).toBeInTheDocument();
    expect(screen.queryByText("members-panel:alpha")).not.toBeInTheDocument();
  });

  it("Project が無ければ空表示にする", async () => {
    // Arrange
    vi.mocked(listAdminProjects).mockResolvedValue([]);

    // Act
    renderWithUiText(<AdminProjectsPageClient />);

    // Assert
    expect(await screen.findByText("プロジェクトはありません。")).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("取得に失敗すると権限不足の案内とエラートーストを出し、空表示は出さない", async () => {
    // Arrange
    vi.mocked(listAdminProjects).mockRejectedValue(new Error("forbidden"));

    // Act
    renderWithUiText(<AdminProjectsPageClient />);

    // Assert
    expect(await screen.findByText("プロジェクト一覧にはテナント管理者権限が必要です。")).toBeInTheDocument();
    expect(screen.getByText(unknownErrorMessage)).toBeInTheDocument();
    expect(screen.queryByText("プロジェクトはありません。")).not.toBeInTheDocument();
  });

  it("エラートーストを閉じられる", async () => {
    // Arrange
    vi.mocked(listAdminProjects).mockRejectedValue(new Error("forbidden"));
    renderWithUiText(<AdminProjectsPageClient />);
    await screen.findByText(unknownErrorMessage);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "通知を閉じる" }));

    // Assert
    expect(screen.queryByText(unknownErrorMessage)).not.toBeInTheDocument();
  });

  it("取得完了前に画面を閉じたら結果を反映しない", async () => {
    // Arrange
    let rejectProjects: (reason: unknown) => void = () => undefined;
    vi.mocked(listAdminProjects).mockReturnValue(new Promise<ProjectSummary[]>((_resolve, reject) => {
      rejectProjects = reject;
    }));
    const { unmount } = renderWithUiText(<AdminProjectsPageClient />);

    // Act
    unmount();
    rejectProjects(new Error("late"));

    // Assert
    await waitFor(() => expect(screen.queryByText(unknownErrorMessage)).not.toBeInTheDocument());
  });
});
