import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiDelete: vi.fn(),
    apiGet: vi.fn(),
    apiPatch: vi.fn(),
    apiPost: vi.fn()
  };
});

import { apiDelete, apiGet, apiPatch, apiPost } from "@/shared/api";
import {
  deleteSchedule,
  listRunAsCandidates,
  listSchedules,
  runSchedule,
  updateSchedule
} from "@/features/schedules/schedulesApi";
import type { ScheduleListItem } from "@/features/schedules/types";

const row: ScheduleListItem = {
  scheduleId: "sch-1",
  name: "nightly",
  definitionId: "def-1",
  definitionVersionId: null,
  runAsPrincipalId: "pr-1",
  cronExpression: "0 9 * * *",
  timeZone: "Asia/Tokyo",
  overlapPolicy: "skip",
  enabled: true,
  nextFireAt: "2026-11-07T00:00:00Z",
  createdAt: "2026-11-01T00:00:00Z",
  updatedAt: "2026-11-01T00:00:00Z"
};

describe("schedules api", () => {
  beforeEach(() => {
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiPatch).mockReset();
    vi.mocked(apiPost).mockReset();
  });

  it("一覧は 1 回の GET だけで、契約外の input を状態へ持ち込まない", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue([{ ...row, input: { secret: "x" } }]);

    // Act
    const rows = await listSchedules();

    // Assert
    expect(apiGet).toHaveBeenCalledTimes(1);
    expect(apiGet).toHaveBeenCalledWith("/schedules");
    expect(rows).toEqual([row]);
  });

  it("部分更新は ID をエンコードしたパスへ本文だけを PATCH する", async () => {
    // Arrange
    vi.mocked(apiPatch).mockResolvedValue({ ...row, createdByPrincipalId: "pr-admin", input: null });

    // Act
    await updateSchedule("sch/1", { enabled: false });

    // Assert
    expect(apiPatch).toHaveBeenCalledWith("/schedules/sch%2F1", { enabled: false });
  });

  it("削除は DELETE、手動実行は .../run へ POST する", async () => {
    // Arrange
    vi.mocked(apiPost).mockResolvedValue({ displayId: "Ex1", resourceId: "uuid" });

    // Act
    await deleteSchedule("sch-1");
    const result = await runSchedule("sch-1");

    // Assert
    expect(apiDelete).toHaveBeenCalledWith("/schedules/sch-1");
    expect(apiPost).toHaveBeenCalledWith("/schedules/sch-1/run", {});
    expect(result).toEqual({ displayId: "Ex1" });
  });

  it("run-as の候補は有効な ServiceAccount だけを名前順で返す", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue([
      { principalId: "pr-2", name: "zeta", isActive: true, hasApiKey: true },
      { principalId: "pr-3", name: "off", isActive: false, hasApiKey: false },
      { principalId: "pr-1", name: "alpha", isActive: true, hasApiKey: false }
    ]);

    // Act
    const candidates = await listRunAsCandidates();

    // Assert
    expect(apiGet).toHaveBeenCalledWith("/admin/service-accounts");
    expect(candidates).toEqual([
      { principalId: "pr-1", name: "alpha" },
      { principalId: "pr-2", name: "zeta" }
    ]);
  });
});
