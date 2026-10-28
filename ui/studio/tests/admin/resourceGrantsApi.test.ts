import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPut: vi.fn()
  };
});

import { apiGet, apiPut } from "@/shared/api";
import {
  getResourceGrants,
  listDefinitionCandidates,
  replaceResourceGrants
} from "@/features/admin/resourceGrantsApi";

const userSubject = { kind: "user", userId: "u-1" } as const;

describe("resource grants api", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPut).mockReset();
  });

  it("GET は両配列だけを写し、契約外の項目を持ち込まない", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue({ projectIds: ["p-1"], definitionIds: ["d-1"], extra: "x" });

    // Act
    const grants = await getResourceGrants(userSubject);

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/admin/users/u-1/resource-grants");
    expect(grants).toEqual({ projectIds: ["p-1"], definitionIds: ["d-1"] });
  });

  it("ServiceAccount の PUT は両配列を本文に載せて送る", async () => {
    // Arrange
    const body = { projectIds: [], definitionIds: ["d-1"] };
    vi.mocked(apiPut).mockResolvedValue(body);

    // Act
    await replaceResourceGrants({ kind: "serviceAccount", serviceAccountId: "sa-1" }, body);

    // Assert
    expect(apiPut).toHaveBeenCalledWith("/admin/service-accounts/sa-1/resource-grants", {
      projectIds: [],
      definitionIds: ["d-1"]
    });
  });

  it("定義候補は未削除を名前順に 1 ページだけ読み、所属 Project を残す", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue({
      items: [
        {
          displayId: "orders",
          resourceId: "d-1",
          name: "orders",
          projectId: "p-1",
          createdAt: "2026-10-01T00:00:00Z",
          updatedAt: "2026-10-01T00:00:00Z",
          yaml: "name: orders"
        }
      ],
      totalCount: 1,
      offset: 0,
      limit: 500,
      hasMore: false
    });

    // Act
    const page = await listDefinitionCandidates();

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/definitions?limit=500&offset=0&sortBy=name&sortOrder=asc");
    expect(page).toEqual({
      candidates: [{ resourceId: "d-1", name: "orders", displayId: "orders", projectId: "p-1" }],
      hasMore: false
    });
  });

  it("定義候補が 1 ページに収まらないときは欠けていると返す", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 600, offset: 0, limit: 500, hasMore: true });

    // Act
    const page = await listDefinitionCandidates();

    // Assert
    expect(page.hasMore).toBe(true);
  });
});
