import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
  };
});

import { apiGet } from "@/shared/api";
import { listMembershipProjects } from "@/shared/project/api";
import type { ProjectSummary } from "@/shared/project/types";

/**
 * 一覧用の Project。
 * @param role 実効ロール。
 * @returns 画面用の Project。
 */
function project(role: ProjectSummary["role"]): ProjectSummary {
  return {
    projectId: "p1",
    slug: "default",
    displayName: "Default",
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role,
  };
}

describe("shared project api", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
  });

  it("所属一覧は未知の role を null にする", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue([project("owner" as ProjectSummary["role"])]);

    // Act
    const rows = await listMembershipProjects();

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/projects");
    expect(rows[0]?.role).toBeNull();
  });
});
