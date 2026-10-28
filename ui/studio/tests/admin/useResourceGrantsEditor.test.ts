import { act, renderHook, waitFor } from "@testing-library/react";
import type { FormEvent } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPut: vi.fn()
  };
});

import { useResourceGrantsEditor } from "@/features/admin/hooks/useResourceGrantsEditor";
import type { PrincipalResourceGrants } from "@/features/admin/resourceGrants";
import type { ProjectSummary } from "@/features/projects/types";
import { apiGet, apiPut } from "@/shared/api";

const subject = { kind: "user", userId: "u-1" } as const;
const grantsPath = "/admin/users/u-1/resource-grants";

const projects: ProjectSummary[] = [
  {
    projectId: "p-1",
    slug: "billing",
    displayName: "Billing",
    description: null,
    createdAt: "2026-10-01T00:00:00Z",
    role: null
  },
  {
    projectId: "p-2",
    slug: "ops",
    displayName: "Ops",
    description: null,
    createdAt: "2026-10-01T00:00:00Z",
    role: "admin"
  }
];

const definitionsPage = {
  items: [
    {
      displayId: "orders",
      resourceId: "d-1",
      name: "orders",
      projectId: "p-1",
      createdAt: "2026-10-01T00:00:00Z",
      updatedAt: "2026-10-01T00:00:00Z"
    }
  ],
  totalCount: 1,
  offset: 0,
  limit: 500,
  hasMore: false
};

/** submit に渡すフォームイベント。preventDefault だけを使う。 */
const submitEvent = { preventDefault: vi.fn() } as unknown as FormEvent<HTMLFormElement>;

/**
 * パスごとに GET の応答を返すモックを入れる。
 * @param grants 許可の応答。呼ぶたびに評価する。
 * @param candidatesFail true なら Project 一覧の取得を失敗させる。
 */
function mockGets(grants: () => Promise<PrincipalResourceGrants>, candidatesFail = false): void {
  vi.mocked(apiGet).mockImplementation(async (path: string) => {
    if (path === grantsPath) return grants();
    if (path === "/admin/projects") {
      if (candidatesFail) throw Object.assign(new Error("unavailable"), { status: 503 });
      return projects;
    }
    if (path.startsWith("/definitions?")) return definitionsPage;
    throw new Error(`unexpected path: ${path}`);
  });
}

/**
 * フックを描画し、許可と候補の読込が終わるまで待つ。
 * @returns renderHook の結果。
 */
async function renderLoaded() {
  const rendered = renderHook(() => useResourceGrantsEditor(subject));
  await waitFor(() => {
    expect(rendered.result.current.loadState).not.toBe("loading");
    expect(
      rendered.result.current.projectCandidates !== null || rendered.result.current.candidatesLoadFailed
    ).toBe(true);
  });
  return rendered;
}

describe("useResourceGrantsEditor", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPut).mockReset();
  });

  it("読込後は付与済みが選択になり、候補が揃って保存できる", async () => {
    // Arrange
    mockGets(async () => ({ projectIds: ["p-1"], definitionIds: ["d-1"] }));

    // Act
    const { result } = await renderLoaded();

    // Assert
    expect(result.current.loadState).toBe("ready");
    expect([...result.current.selectedProjectIds]).toEqual(["p-1"]);
    expect([...result.current.selectedDefinitionIds]).toEqual(["d-1"]);
    expect(result.current.projectCandidates?.map((project) => project.projectId)).toEqual(["p-1", "p-2"]);
    expect(result.current.definitionCandidates).toEqual([
      { resourceId: "d-1", name: "orders", displayId: "orders", projectId: "p-1" }
    ]);
    expect(result.current.canSave).toBe(true);
  });

  it("project を外して保存しても、候補に無い付与済み定義を含む定義 ID は本文に残る", async () => {
    // Arrange
    mockGets(async () => ({ projectIds: ["p-1", "p-2"], definitionIds: ["d-1", "d-unknown"] }));
    vi.mocked(apiPut).mockImplementation(async (_path: string, body: unknown) => body);
    const { result } = await renderLoaded();

    // Act
    act(() => result.current.toggleProject("p-2"));
    await act(() => result.current.submit(submitEvent));

    // Assert
    expect(apiPut).toHaveBeenCalledWith(grantsPath, { projectIds: ["p-1"], definitionIds: ["d-1", "d-unknown"] });
    expect(result.current.toast?.tone).toBe("success");
  });

  it("候補を読めないときは保存できず、PUT も送らない", async () => {
    // Arrange
    mockGets(async () => ({ projectIds: ["p-1"], definitionIds: [] }), true);
    const { result } = await renderLoaded();

    // Act
    await act(() => result.current.submit(submitEvent));

    // Assert
    expect(result.current.candidatesLoadFailed).toBe(true);
    expect(result.current.canSave).toBe(false);
    expect(apiPut).not.toHaveBeenCalled();
  });

  it("422 のときは成功にせず、許可を読み直して選択を保存済みの値へ戻す", async () => {
    // Arrange
    mockGets(async () => ({ projectIds: [], definitionIds: [] }));
    vi.mocked(apiPut).mockRejectedValue({
      status: 422,
      error: { code: "VALIDATION_ERROR", message: "projectIds is invalid." }
    });
    const { result } = await renderLoaded();
    act(() => result.current.toggleProject("p-1"));

    // Act
    await act(() => result.current.submit(submitEvent));

    // Assert
    expect(result.current.submitError).not.toBeNull();
    expect(result.current.toast).toBeNull();
    expect([...result.current.selectedProjectIds]).toEqual([]);
    expect(vi.mocked(apiGet).mock.calls.filter(([path]) => path === grantsPath)).toHaveLength(2);
  });

  it("上限を超える選択は PUT せず、種別の上限超過を返す", async () => {
    // Arrange
    const tooManyProjects = Array.from({ length: 33 }, (_, index) => `p-${index}`);
    mockGets(async () => ({ projectIds: tooManyProjects, definitionIds: [] }));
    const { result } = await renderLoaded();

    // Act
    await act(() => result.current.submit(submitEvent));

    // Assert
    expect(result.current.limitErrors).toEqual({ projectIds: "tooMany" });
    expect(apiPut).not.toHaveBeenCalled();
  });

  it("主体が見つからないときは notFound にする", async () => {
    // Arrange
    mockGets(async () => {
      throw Object.assign(new Error("not found"), { status: 404 });
    });

    // Act
    const { result } = await renderLoaded();

    // Assert
    expect(result.current.loadState).toBe("notFound");
    expect(result.current.savedGrants).toBeNull();
  });
});
