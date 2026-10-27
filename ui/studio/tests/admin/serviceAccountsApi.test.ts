import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPatch: vi.fn(),
    apiPost: vi.fn()
  };
});

import { apiGet, apiPatch, apiPost } from "@/shared/api";
import {
  createServiceAccount,
  getServiceAccount,
  listServiceAccounts,
  updateServiceAccount
} from "@/features/admin/serviceAccountsApi";
import type { AdminServiceAccount } from "@/features/admin/types";

const row: AdminServiceAccount = {
  serviceAccountId: "sa-1",
  principalId: "pr-1",
  name: "nightly-runner",
  isActive: true,
  hasApiKey: false,
  groupIds: ["g-1"],
  createdAt: "2026-10-01T00:00:00Z"
};

describe("service account api", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPatch).mockReset();
    vi.mocked(apiPost).mockReset();
  });

  it("一覧は契約外の項目を画面の状態へ持ち込まない", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue([{ ...row, plainKey: "stv_secret" }]);

    // Act
    const rows = await listServiceAccounts();

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/admin/service-accounts");
    expect(rows).toEqual([row]);
  });

  it("詳細は ID をエンコードしたパスで GET する", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue(row);

    // Act
    await getServiceAccount("sa/1");

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/admin/service-accounts/sa%2F1");
  });

  it("作成は name と groupIds の本文をそのまま POST する", async () => {
    // Arrange
    vi.mocked(apiPost).mockResolvedValue(row);

    // Act
    const created = await createServiceAccount({ name: "nightly-runner", groupIds: ["g-1"] });

    // Assert
    expect(apiPost).toHaveBeenCalledWith("/admin/service-accounts", { name: "nightly-runner", groupIds: ["g-1"] });
    expect(created).toEqual(row);
  });

  it("部分更新は変えた項目だけの本文を PATCH する", async () => {
    // Arrange
    vi.mocked(apiPatch).mockResolvedValue({ ...row, isActive: false });

    // Act
    const updated = await updateServiceAccount("sa-1", { isActive: false });

    // Assert
    expect(apiPatch).toHaveBeenCalledWith("/admin/service-accounts/sa-1", { isActive: false });
    expect(updated.isActive).toBe(false);
  });
});
