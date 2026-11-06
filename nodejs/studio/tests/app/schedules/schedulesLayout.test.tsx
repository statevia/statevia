import { beforeEach, describe, expect, it, vi } from "vitest";
import SchedulesLayout from "@/app/schedules/layout";
import { fetchAuthMeServer } from "@/shared/auth/serverAuthMe";
import { redirect } from "next/navigation";
import * as api from "@/shared/api";

vi.mock("@/shared/auth/serverAuthMe", () => ({
  fetchAuthMeServer: vi.fn()
}));

vi.mock("@/shared/api", () => ({
  apiGet: vi.fn()
}));

vi.mock("next/navigation", () => ({
  redirect: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  })
}));

/**
 * `/auth/me` の応答を作る。
 * @param isTenantAdmin テナント管理者か。
 * @returns 認証情報。
 */
function me(isTenantAdmin: boolean) {
  return {
    tenantId: "tenant",
    tenantKey: "default",
    principalId: "principal",
    username: "user",
    email: "user@example.com",
    isTenantAdmin,
    permissions: ["executions.read", "executions.write"]
  };
}

describe("SchedulesLayout", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("未認証時はログインへリダイレクトする", async () => {
    // Arrange
    vi.mocked(fetchAuthMeServer).mockResolvedValue(null);

    // Act
    const render = SchedulesLayout({ children: <div>content</div> });

    // Assert
    await expect(render).rejects.toThrow("REDIRECT:/login?from=/schedules");
  });

  it("実行の権限を持っていてもテナント管理者でなければダッシュボードへ戻し、schedules API を呼ばない", async () => {
    // Arrange
    vi.mocked(fetchAuthMeServer).mockResolvedValue(me(false));

    // Act
    const render = SchedulesLayout({ children: <div>content</div> });

    // Assert
    await expect(render).rejects.toThrow("REDIRECT:/dashboard");
    expect(api.apiGet).not.toHaveBeenCalled();
  });

  it("テナント管理者には children を描画する", async () => {
    // Arrange
    vi.mocked(fetchAuthMeServer).mockResolvedValue(me(true));

    // Act
    const result = await SchedulesLayout({ children: <div>content</div> });

    // Assert
    expect(redirect).not.toHaveBeenCalled();
    expect(result).toBeTruthy();
  });
});
