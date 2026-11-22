import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminModulesPageClient } from "@/features/admin/ui/AdminModulesPageClient";
import type { AdminModule } from "@/features/admin/types";
import * as api from "@/shared/api";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("@/shared/api", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn()
}));

const failedModule: AdminModule = {
  moduleId: "acme.billing",
  name: "Acme Billing",
  version: "1.2.0",
  status: "Failed",
  sha256: "ab12cd34",
  sourceLabel: "oci:registry.example/acme/billing:1.2.0",
  loadedAtUtc: "2026-11-01T00:00:00Z",
  message: "Failed to load module assembly (BadImageFormatException)"
};

/**
 * 一覧 GET の応答を入れる。
 * @param modules 呼ばれるたびに評価する応答。
 */
function mockList(modules: () => Promise<AdminModule[]>): void {
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path === "/admin/modules") return modules();
    throw new Error(`unexpected path: ${path}`);
  });
}

/**
 * 一覧 GET の回数。
 * @returns `/admin/modules` の呼び出し回数。
 */
function listCallCount(): number {
  return vi.mocked(api.apiGet).mock.calls.filter(([path]) => path === "/admin/modules").length;
}

describe("AdminModulesPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("一覧に契約の全項目を出す", async () => {
    // Arrange
    mockList(async () => [failedModule]);

    // Act
    renderWithUiText(<AdminModulesPageClient />);

    // Assert
    expect(await screen.findByText("Acme Billing")).toBeInTheDocument();
    expect(screen.getByText("acme.billing")).toBeInTheDocument();
    expect(screen.getByText("1.2.0")).toBeInTheDocument();
    expect(screen.getByText("FAILED")).toBeInTheDocument();
    expect(screen.getByText("oci:registry.example/acme/billing:1.2.0")).toBeInTheDocument();
    expect(screen.getByText("Failed to load module assembly (BadImageFormatException)")).toBeInTheDocument();
    expect(screen.getByText("ab12cd34")).toBeInTheDocument();
    expect(screen.queryByText("entry DLL")).not.toBeInTheDocument();
  });

  it("契約に無い結果の値でも落ちずにその値を出す", async () => {
    // Arrange
    mockList(async () => [{ ...failedModule, status: "Quarantined", message: null }]);

    // Act
    renderWithUiText(<AdminModulesPageClient />);

    // Assert
    expect(await screen.findByText("QUARANTINED")).toBeInTheDocument();
    expect(screen.queryByText("メッセージ")).not.toBeInTheDocument();
  });

  it("0 件なら空状態を出す", async () => {
    // Arrange
    mockList(async () => []);

    // Act
    renderWithUiText(<AdminModulesPageClient />);

    // Assert
    expect(await screen.findByText("読み込んだアクションモジュールはありません。")).toBeInTheDocument();
  });

  it("取得に失敗したら 0 件の成功にせずエラーを出し、再試行で取り直す", async () => {
    // Arrange
    let listCalls = 0;
    mockList(async () => {
      listCalls += 1;
      if (listCalls === 1) throw new Error("forbidden");
      return [failedModule];
    });
    renderWithUiText(<AdminModulesPageClient />);
    const retry = await screen.findByRole("button", { name: "再試行" });
    expect(screen.queryByText("読み込んだアクションモジュールはありません。")).not.toBeInTheDocument();

    // Act
    fireEvent.click(retry);

    // Assert
    expect(await screen.findByText("Acme Billing")).toBeInTheDocument();
    expect(listCallCount()).toBe(2);
  });

  it("一覧を読み直しても GET だけで、Module の再読み込みは呼ばない", async () => {
    // Arrange
    mockList(async () => [failedModule]);
    renderWithUiText(<AdminModulesPageClient />);
    await screen.findByText("Acme Billing");

    // Act
    fireEvent.click(screen.getByRole("button", { name: "一覧を読み直す" }));

    // Assert
    await screen.findByText("Acme Billing");
    expect(listCallCount()).toBe(2);
    expect(api.apiPost).not.toHaveBeenCalled();
  });

  it("再読み込み・アップロード・削除の操作を置かない", async () => {
    // Arrange
    mockList(async () => [failedModule]);

    // Act
    renderWithUiText(<AdminModulesPageClient />);

    // Assert
    await screen.findByText("Acme Billing");
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["一覧を読み直す"]);
  });
});
