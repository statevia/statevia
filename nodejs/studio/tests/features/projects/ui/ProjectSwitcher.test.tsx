import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderWithUiText } from "@/tests/testUtils";

import type { ProjectSummary } from "@/shared/project/types";
import { ProjectSwitcher } from "@/features/projects/ui/ProjectSwitcher";

/**
 * 所属一覧の 1 件。
 * @param projectId Project UUID。
 * @param displayName 表示名。
 * @param role 実効ロール。
 * @returns 画面用の Project。
 */
function project(projectId: string, displayName: string, role: ProjectSummary["role"]): ProjectSummary {
  return {
    projectId,
    slug: projectId,
    displayName,
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role,
  };
}

describe("ProjectSwitcher", () => {
  it("所属が無ければ何も描画しない", () => {
    // Arrange & Act
    const { container } = renderWithUiText(<ProjectSwitcher />, { projects: [] });

    // Assert
    expect(container).toBeEmptyDOMElement();
  });

  it("所属 Project を表示名とロールで並べ、先頭を選択する", () => {
    // Arrange & Act
    renderWithUiText(<ProjectSwitcher />, {
      projects: [project("alpha", "Alpha", "admin"), project("beta", "Beta", "reader")],
    });

    // Assert
    const select = screen.getByRole("combobox", { name: "プロジェクト" });
    const optionLabels = Array.from(select.querySelectorAll("option")).map((option) => option.textContent);
    expect(optionLabels).toEqual(["Alpha（管理）", "Beta（閲覧）"]);
    expect(select).toHaveValue("alpha");
  });

  it("別の Project を選ぶと選択が切り替わり、メンバーへのリンクは出さない", () => {
    // Arrange
    renderWithUiText(<ProjectSwitcher />, {
      projects: [project("alpha", "Alpha", "admin"), project("beta", "Beta", "reader")],
    });

    // Act
    fireEvent.change(screen.getByRole("combobox", { name: "プロジェクト" }), { target: { value: "beta" } });

    // Assert
    expect(screen.getByRole("combobox", { name: "プロジェクト" })).toHaveValue("beta");
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("ロールが未知の所属はロール名を空にして表示する", () => {
    // Arrange & Act
    renderWithUiText(<ProjectSwitcher />, { projects: [project("gamma", "Gamma", null)] });

    // Assert
    expect(screen.getByRole("option")).toHaveTextContent("Gamma（）");
  });
});
