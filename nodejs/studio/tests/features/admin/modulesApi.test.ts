import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPost: vi.fn()
  };
});

import { apiGet, apiPost } from "@/shared/api";
import { isKnownModuleStatus, listAdminModules } from "@/features/admin/modulesApi";
import type { AdminModule } from "@/features/admin/types";

const row: AdminModule = {
  moduleId: "acme.billing",
  name: "Acme Billing",
  version: "1.2.0",
  status: "Failed",
  sha256: "ab12",
  sourceLabel: "oci:registry.example/acme/billing:1.2.0",
  loadedAtUtc: "2026-11-01T00:00:00Z",
  message: "Failed to load module assembly (BadImageFormatException)"
};

describe("modules api", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPost).mockReset();
  });

  it("一覧は message を含む全項目を残し、契約外の項目（パスなど）を持ち込まない", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue([{ ...row, entryAssemblyPath: "/app/modules/acme/Acme.Billing.dll" }]);

    // Act
    const rows = await listAdminModules();

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/admin/modules");
    expect(rows).toEqual([row]);
  });

  it("sourceLabel と message が無い行は null にそろえる", async () => {
    // Arrange
    const { sourceLabel: _sourceLabel, message: _message, ...withoutOptional } = row;
    vi.mocked(apiGet).mockResolvedValue([withoutOptional]);

    // Act
    const rows = await listAdminModules();

    // Assert
    expect(rows[0].sourceLabel).toBeNull();
    expect(rows[0].message).toBeNull();
  });

  it("一覧の取得は内部の再ロードを呼ばない", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue([row]);

    // Act
    await listAdminModules();

    // Assert
    expect(apiPost).not.toHaveBeenCalled();
    expect(vi.mocked(apiGet).mock.calls.every(([path]) => !String(path).includes("reload"))).toBe(true);
  });

  it("取得の失敗は空の一覧にせず投げ直す", async () => {
    // Arrange
    vi.mocked(apiGet).mockRejectedValue(new Error("403"));

    // Act
    const promise = listAdminModules();

    // Assert
    await expect(promise).rejects.toThrow("403");
  });

  it("既知の 4 値だけを既知の状態とみなす", () => {
    // Arrange
    const values = ["Loaded", "Failed", "Skipped", "Duplicate", "Quarantined", "loaded"];

    // Act
    const known = values.filter(isKnownModuleStatus);

    // Assert
    expect(known).toEqual(["Loaded", "Failed", "Skipped", "Duplicate"]);
  });
});
