import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminServiceAccountDetailPageClient } from "@/features/admin/ui/AdminServiceAccountDetailPageClient";
import type { AdminGroupListItem, AdminServiceAccount } from "@/features/admin/types";
import * as api from "@/shared/api";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("@/shared/api", () => ({
  apiGet: vi.fn(),
  apiPatch: vi.fn(),
  apiPost: vi.fn()
}));

const serviceAccount: AdminServiceAccount = {
  serviceAccountId: "sa-1",
  principalId: "pr-1",
  name: "nightly-runner",
  isActive: true,
  hasApiKey: false,
  groupIds: ["g-1"],
  createdAt: "2026-10-01T00:00:00Z"
};

/**
 * グループを作る。
 * @param groupId グループ ID。
 * @param name 表示名。
 * @returns グループ一覧の 1 件。
 */
function group(groupId: string, name: string): AdminGroupListItem {
  return { groupId, name, isSystem: false, memberCount: 0, permissionCount: 1, updatedAt: "2026-10-01T00:00:00Z" };
}

/**
 * パスごとに GET の応答を返すモックを入れる。
 * @param detail 詳細の応答。
 * @param groups グループ一覧の応答。
 */
function mockGets(
  detail: () => Promise<AdminServiceAccount>,
  groups: () => Promise<AdminGroupListItem[]> = async () => [group("g-1", "Operators"), group("g-2", "Auditors")]
): void {
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path === "/admin/service-accounts/sa-1") return detail();
    if (path === "/admin/groups") return groups();
    throw new Error(`unexpected path: ${path}`);
  });
}

/**
 * 詳細を描画し、フォームが出るまで待つ。
 */
async function renderReady(): Promise<void> {
  renderWithUiText(<AdminServiceAccountDetailPageClient serviceAccountId="sa-1" />);
  await screen.findByLabelText("Operators");
}

describe("AdminServiceAccountDetailPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("詳細から実行リソース許可の画面へ進むリンクを出す", async () => {
    // Arrange
    mockGets(async () => serviceAccount);

    // Act
    await renderReady();

    // Assert
    expect(screen.getByRole("link", { name: "実行リソース許可" })).toHaveAttribute(
      "href",
      "/admin/service-accounts/sa-1/resource-grants"
    );
  });

  it("無効化だけを変えると isActive だけを PATCH する", async () => {
    // Arrange
    mockGets(async () => serviceAccount);
    vi.mocked(api.apiPatch).mockResolvedValue({ ...serviceAccount, isActive: false });
    await renderReady();
    fireEvent.click(screen.getByLabelText("有効にする"));

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    await waitFor(() => {
      expect(api.apiPatch).toHaveBeenCalledWith("/admin/service-accounts/sa-1", { isActive: false });
    });
    expect(await screen.findByText("保存しました。")).toBeInTheDocument();
  });

  it("無効化の説明でスケジュールが自動では止まらないことと API キー失効との違いを出す", async () => {
    // Arrange
    mockGets(async () => serviceAccount);

    // Act
    await renderReady();

    // Assert
    expect(screen.getByText(/スケジュールは自動では止まらない/)).toBeInTheDocument();
    expect(screen.getByText(/API キーの失効とは別の操作です/)).toBeInTheDocument();
  });

  it("名前と所属を変えると displayName と groupIds を送り、変えていない isActive は送らない", async () => {
    // Arrange
    mockGets(async () => serviceAccount);
    vi.mocked(api.apiPatch).mockResolvedValue({ ...serviceAccount, name: "nightly-2", groupIds: ["g-1", "g-2"] });
    await renderReady();
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: " nightly-2 " } });
    fireEvent.click(screen.getByLabelText("Auditors"));

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    await waitFor(() => {
      expect(api.apiPatch).toHaveBeenCalledWith("/admin/service-accounts/sa-1", {
        displayName: "nightly-2",
        groupIds: ["g-1", "g-2"]
      });
    });
  });

  it("所属を 0 件にすると PATCH しない", async () => {
    // Arrange
    mockGets(async () => serviceAccount);
    await renderReady();
    fireEvent.click(screen.getByLabelText("Operators"));

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    expect(await screen.findByText("グループを 1 件以上選んでください。")).toBeInTheDocument();
    expect(api.apiPatch).not.toHaveBeenCalled();
  });

  it("何も変えずに保存すると PATCH せず、変更が無いと出す", async () => {
    // Arrange
    mockGets(async () => serviceAccount);
    await renderReady();

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    expect(await screen.findByText("変更はありません。")).toBeInTheDocument();
    expect(api.apiPatch).not.toHaveBeenCalled();
  });

  it("キー付きの行も同じフォームで編集でき、削除ボタンは無い", async () => {
    // Arrange
    mockGets(async () => ({ ...serviceAccount, hasApiKey: true }));

    // Act
    await renderReady();

    // Assert
    expect(screen.getByText(/キーの失効は API キー画面で行います/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "保存" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: /削除|失効/ })).not.toBeInTheDocument();
  });

  it("サーバーの 422 はフォームに出し、入力を残す", async () => {
    // Arrange
    mockGets(async () => serviceAccount);
    vi.mocked(api.apiPatch).mockRejectedValue({
      status: 422,
      error: { code: "VALIDATION_ERROR", message: "displayName is invalid." }
    });
    await renderReady();
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "nightly-2" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    expect(await screen.findByText(/VALIDATION_ERROR - displayName is invalid\./)).toBeInTheDocument();
    expect(screen.getByLabelText("名前")).toHaveValue("nightly-2");
  });

  it("404 なら見つからないと出し、一覧へ戻るリンクだけを出す", async () => {
    // Arrange
    mockGets(async () => {
      throw Object.assign(new Error("not found"), { status: 404 });
    });

    // Act
    renderWithUiText(<AdminServiceAccountDetailPageClient serviceAccountId="sa-1" />);

    // Assert
    expect(await screen.findByText(/この ServiceAccount は見つかりません/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ServiceAccount 一覧へ戻る" })).toHaveAttribute(
      "href",
      "/admin/service-accounts"
    );
    expect(screen.queryByRole("button", { name: "再試行" })).not.toBeInTheDocument();
  });

  it("404 以外の失敗は再試行で取り直す", async () => {
    // Arrange
    let detailCalls = 0;
    mockGets(async () => {
      detailCalls += 1;
      if (detailCalls === 1) throw new Error("network");
      return serviceAccount;
    });
    renderWithUiText(<AdminServiceAccountDetailPageClient serviceAccountId="sa-1" />);
    const retry = await screen.findByRole("button", { name: "再試行" });

    // Act
    fireEvent.click(retry);

    // Assert
    expect(await screen.findByRole("heading", { name: "nightly-runner" })).toBeInTheDocument();
  });

  it("グループ一覧を読めなくても有効と名前は保存でき、所属は送らない", async () => {
    // Arrange
    mockGets(
      async () => serviceAccount,
      async () => {
        throw new Error("network");
      }
    );
    vi.mocked(api.apiPatch).mockResolvedValue({ ...serviceAccount, name: "nightly-2" });
    renderWithUiText(<AdminServiceAccountDetailPageClient serviceAccountId="sa-1" />);
    await screen.findByText("グループの読み込みに失敗しました。");
    fireEvent.change(screen.getByLabelText("名前"), { target: { value: "nightly-2" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    await waitFor(() => {
      expect(api.apiPatch).toHaveBeenCalledWith("/admin/service-accounts/sa-1", { displayName: "nightly-2" });
    });
  });
});
