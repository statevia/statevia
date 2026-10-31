import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiDelete: vi.fn(),
    apiGet: vi.fn(),
    apiPost: vi.fn(),
    apiPut: vi.fn(),
  };
});

import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api";
import {
  createProject,
  listAdminProjects,
  listGrantCandidates,
  listProjectMembers,
  removeProjectMember,
  upsertProjectMember,
} from "@/features/projects/api";
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

describe("project api", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPut).mockReset();
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiPost).mockReset();
  });

  it("作成は本文をそのまま POST し、応答の role を既知の値へ狭める", async () => {
    vi.mocked(apiPost).mockResolvedValue(project("admin"));

    const created = await createProject({ slug: "default", displayName: "Default" });

    expect(apiPost).toHaveBeenCalledWith("/projects", { slug: "default", displayName: "Default" });
    expect(created.role).toBe("admin");
  });

  it("管理一覧は既知の role を残す", async () => {
    vi.mocked(apiGet).mockResolvedValue([project("reader")]);

    const rows = await listAdminProjects();

    expect(apiGet).toHaveBeenCalledWith("/admin/projects");
    expect(rows[0]?.role).toBe("reader");
  });

  it("メンバーは欠落行を捨て、表示名が文字でなければ空にする", async () => {
    vi.mocked(apiGet).mockResolvedValue([
      {
        principalId: "user-1",
        displayName: 1,
        kind: "user",
        role: "admin",
        createdAt: "2026-01-01T00:00:00Z",
      },
      { principalId: "user-2", kind: "nope", role: "reader", createdAt: "2026-01-01T00:00:00Z" },
      { displayName: "missing", kind: "user", role: "reader" },
    ]);

    const rows = await listProjectMembers("a/b");

    expect(apiGet).toHaveBeenCalledWith("/projects/a%2Fb/members");
    expect(rows).toEqual([
      {
        principalId: "user-1",
        displayName: "",
        kind: "user",
        role: "admin",
        createdAt: "2026-01-01T00:00:00Z",
      },
    ]);
  });

  it("候補は system と不正行を捨てる", async () => {
    vi.mocked(apiGet).mockResolvedValue([
      { principalId: "key-1", displayName: "module", kind: "serviceAccount" },
      { principalId: "sys-1", displayName: "system", kind: "system" },
      { displayName: "broken", kind: "user" },
    ]);

    const rows = await listGrantCandidates("p 1");

    expect(apiGet).toHaveBeenCalledWith("/projects/p%201/member-candidates");
    expect(rows).toEqual([
      { principalId: "key-1", displayName: "module", kind: "serviceAccount" },
    ]);
  });

  it("付与と剥奪はエンコードしたパスを使う", async () => {
    vi.mocked(apiPut).mockResolvedValue(null);
    vi.mocked(apiDelete).mockResolvedValue(undefined);

    await upsertProjectMember("a/b", "c d", "publisher");
    await removeProjectMember("a/b", "c d");

    expect(apiPut).toHaveBeenCalledWith("/projects/a%2Fb/members/c%20d", { role: "publisher" });
    expect(apiDelete).toHaveBeenCalledWith("/projects/a%2Fb/members/c%20d");
  });
});
