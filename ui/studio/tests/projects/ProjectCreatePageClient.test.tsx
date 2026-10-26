import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithUiText } from "../testUtils";

vi.mock("@/features/projects/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/projects/api")>();
  return { ...actual, createProject: vi.fn() };
});

vi.mock("@/features/projects/projectContext", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/features/projects/projectContext")>();
  return { ...actual, useProject: vi.fn(actual.useProject) };
});

import { createProject } from "@/features/projects/api";
import { useProject, type ProjectContextValue } from "@/features/projects/projectContext";
import type { ProjectSummary } from "@/features/projects/types";
import { ProjectCreatePageClient } from "@/features/projects/ui/ProjectCreatePageClient";

/** 作成応答の Project。 */
const createdProject: ProjectSummary = {
  projectId: "new-project",
  slug: "alpha",
  displayName: "Alpha",
  description: null,
  createdAt: "2026-10-26T00:00:00Z",
  role: "admin",
};

/**
 * 所属 0 件のプロジェクトコンテキスト。再取得の結果を差し替えられる。
 * @param reloadResult reload が返す値。
 * @returns コンテキストと操作のモック。
 */
function emptyProjectContext(reloadResult: boolean): ProjectContextValue {
  return {
    projects: [],
    selectedProject: null,
    loaded: true,
    selectProject: vi.fn(),
    roleOf: () => null,
    reload: vi.fn(() => Promise.resolve(reloadResult)),
  };
}

/**
 * slug と表示名を入力して送信する。
 * @param slug slug 欄の値。
 * @param displayName 表示名欄の値。
 */
function submitForm(slug: string, displayName: string): void {
  fireEvent.change(screen.getByLabelText("プロジェクトキー"), { target: { value: slug } });
  fireEvent.change(screen.getByLabelText("表示名"), { target: { value: displayName } });
  fireEvent.click(screen.getByRole("button", { name: "作成" }));
}

describe("ProjectCreatePageClient", () => {
  beforeEach(() => {
    vi.mocked(createProject).mockReset();
    vi.mocked(useProject).mockReset();
  });

  it("projects.create が無ければフォームを出さない", () => {
    // Arrange
    vi.mocked(useProject).mockReturnValue(emptyProjectContext(true));

    // Act
    renderWithUiText(<ProjectCreatePageClient />, { projects: [], permissions: ["definitions.read"] });

    // Assert
    expect(screen.getByText("プロジェクトの作成には projects.create 権限が必要です。")).toBeInTheDocument();
    expect(screen.queryByLabelText("プロジェクトキー")).not.toBeInTheDocument();
  });

  it("空白だけの入力は送信せず、両方の欄に必須エラーを出す", () => {
    // Arrange
    vi.mocked(useProject).mockReturnValue(emptyProjectContext(true));
    renderWithUiText(<ProjectCreatePageClient />, { projects: [], permissions: ["projects.create"] });

    // Act
    submitForm("  ", " ");

    // Assert
    expect(createProject).not.toHaveBeenCalled();
    expect(screen.getAllByText("入力してください。")).toHaveLength(2);
  });

  it("所属 0 件でも作成でき、所属を取り直して作った Project を選ぶ", async () => {
    // Arrange
    const context = emptyProjectContext(true);
    vi.mocked(useProject).mockReturnValue(context);
    vi.mocked(createProject).mockResolvedValue(createdProject);
    renderWithUiText(<ProjectCreatePageClient />, { projects: [], permissions: ["projects.create"] });

    // Act
    submitForm(" alpha ", "Alpha");

    // Assert
    expect(await screen.findByText("プロジェクト「Alpha」を作成し、選択しました。")).toBeInTheDocument();
    expect(createProject).toHaveBeenCalledWith({ slug: "alpha", displayName: "Alpha" });
    expect(context.reload).toHaveBeenCalledTimes(1);
    expect(context.selectProject).toHaveBeenCalledWith("new-project");
  });

  it("slug 重複の 422 は slug 欄に出し、選択は変えない", async () => {
    // Arrange
    const context = emptyProjectContext(true);
    vi.mocked(useProject).mockReturnValue(context);
    vi.mocked(createProject).mockRejectedValue({
      status: 422,
      error: { code: "VALIDATION_ERROR", message: "slug must be unique in the tenant.", details: { field: "slug" } },
    });
    renderWithUiText(<ProjectCreatePageClient />, { projects: [], permissions: ["projects.create"] });

    // Act
    submitForm("alpha", "Alpha");

    // Assert
    expect(await screen.findByText("このプロジェクトキーは既に使われています。")).toBeInTheDocument();
    expect(screen.getByLabelText("プロジェクトキー")).toHaveValue("alpha");
    expect(context.selectProject).not.toHaveBeenCalled();
  });

  it("作成後に所属の取得が失敗したら、作成済みと再読み込みを知らせて選択しない", async () => {
    // Arrange
    const context = emptyProjectContext(false);
    vi.mocked(useProject).mockReturnValue(context);
    vi.mocked(createProject).mockResolvedValue(createdProject);
    renderWithUiText(<ProjectCreatePageClient />, { projects: [], permissions: ["projects.create"] });

    // Act
    submitForm("alpha", "Alpha");

    // Assert
    expect(
      await screen.findByText(
        "プロジェクト「Alpha」を作成しました。一覧の取得に失敗したため、ページを再読み込みしてください。",
      ),
    ).toBeInTheDocument();
    expect(context.selectProject).not.toHaveBeenCalled();
    expect(createProject).toHaveBeenCalledTimes(1);
  });

  it("送信中は二重送信しない", async () => {
    // Arrange
    vi.mocked(useProject).mockReturnValue(emptyProjectContext(true));
    vi.mocked(createProject).mockReturnValue(new Promise<ProjectSummary>(() => undefined));
    renderWithUiText(<ProjectCreatePageClient />, { projects: [], permissions: ["projects.create"] });

    // Act
    submitForm("alpha", "Alpha");
    await waitFor(() => expect(screen.getByRole("button", { name: "作成中..." })).toBeDisabled());
    fireEvent.submit(screen.getByRole("button", { name: "作成中..." }).closest("form")!);

    // Assert
    expect(createProject).toHaveBeenCalledTimes(1);
  });
});
