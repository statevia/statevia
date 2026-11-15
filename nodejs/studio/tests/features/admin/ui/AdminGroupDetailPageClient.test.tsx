import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { AdminGroupDetailPageClient } from "@/features/admin/ui/AdminGroupDetailPageClient";
import { renderWithUiText } from "@/tests/testUtils";
import { uiText } from "@/shared/i18n/uiText";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  )
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPut: vi.fn()
  };
});

import { apiGet, apiPut } from "@/shared/api";

const groupDetail = {
  groupId: "group-1",
  name: "Operators",
  isSystem: false,
  memberUserIds: ["user-1"],
  permissionKeys: ["definitions.read"]
};

describe("AdminGroupDetailPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/groups/group-1") return groupDetail;
      if (path === "/admin/users") {
        return [
          {
            userId: "user-1",
            principalId: "principal-1",
            username: "member",
            email: "member@example.com",
            displayName: "Member",
            isTenantAdmin: false,
            isActive: true,
            groupIds: ["group-1"],
            createdAt: "2026-01-01T00:00:00Z"
          },
          {
            userId: "user-2",
            principalId: "principal-2",
            username: "inactive",
            email: "inactive@example.com",
            displayName: "Inactive",
            isTenantAdmin: false,
            isActive: false,
            groupIds: [],
            createdAt: "2026-01-01T00:00:00Z"
          }
        ];
      }
      if (path === "/admin/permissions") {
        return [
          {
            permissionKey: "definitions.read",
            displayLabel: "Read definitions",
            displayKey: "permissions.definitionsRead",
            isSystem: true,
            isDeprecated: false
          },
          {
            permissionKey: "tenant.admin",
            displayLabel: "Tenant admin",
            displayKey: "permissions.tenantAdmin",
            isSystem: true,
            isDeprecated: false
          }
        ];
      }
      throw new Error(`unexpected path ${path}`);
    });
    vi.mocked(apiPut).mockImplementation(async (path: string, body: unknown) => {
      if (path === "/admin/groups/group-1/members") {
        const userIds = (body as { userIds: string[] }).userIds;
        return { ...groupDetail, memberUserIds: userIds };
      }
      if (path === "/admin/groups/group-1/permissions") {
        const permissionKeys = (body as { permissionKeys: string[] }).permissionKeys;
        return { ...groupDetail, permissionKeys };
      }
      throw new Error(`unexpected put ${path}`);
    });
  });

  it("グループ詳細を読み込んでメンバーと権限を表示する", async () => {
    // Arrange / Act
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);

    // Assert
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/groups/group-1");
    });
    expect(await screen.findByRole("heading", { name: "Operators" })).toBeInTheDocument();
    expect(screen.getByLabelText(/^member/)).toBeChecked();
    expect(screen.getByLabelText(new RegExp(uiText.admin.permissions.names["permissions.definitionsRead"]))).toBeChecked();
    expect(screen.queryByText("tenant.admin")).toBeNull();
  });

  it("無効ユーザーには inactive 表記を付ける", async () => {
    // Arrange / Act
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);

    // Assert
    expect(await screen.findByText(`inactive (${uiText.admin.users.inactive})`)).toBeInTheDocument();
  });

  it("変更が無いあいだは保存と元に戻すを押せない", async () => {
    // Arrange / Act
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });

    // Assert
    expect(screen.getByRole("button", { name: "メンバーを保存" })).toBeDisabled();
    expect(screen.getAllByRole("button", { name: "元に戻す" })[0]).toBeDisabled();
  });

  it("メンバーを検索で絞り込める", async () => {
    // Arrange
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });

    // Act
    fireEvent.change(screen.getByLabelText("メンバーを検索"), { target: { value: "INACT" } });

    // Assert
    const list = screen.getByRole("list", { name: "メンバー" });
    expect(within(list).getAllByRole("listitem")).toHaveLength(1);
    expect(within(list).getByText(`inactive (${uiText.admin.users.inactive})`)).toBeInTheDocument();
  });

  it("メンバーを追加すると要約を出し、確認ダイアログで追加を確かめてから保存する", async () => {
    // Arrange
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });
    const inactiveLabel = `inactive (${uiText.admin.users.inactive})`;

    // Act
    fireEvent.click(screen.getByLabelText((content) => content.startsWith(inactiveLabel)));
    const summary = screen.getByText("追加 1 ・ 解除 0").textContent;
    fireEvent.click(screen.getByRole("button", { name: "メンバーを保存" }));
    const dialog = await screen.findByRole("dialog", { name: "メンバーの変更を保存しますか？" });
    const addedList = within(dialog).getByRole("list", { name: "追加（1）" });
    const addedText = addedList.textContent;
    fireEvent.click(within(dialog).getByRole("button", { name: "保存する" }));

    // Assert
    expect(summary).toBe("追加 1 ・ 解除 0");
    expect(addedText).toBe(inactiveLabel);
    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/groups/group-1/members", {
        userIds: ["user-1", "user-2"]
      });
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("権限を外すと確認ダイアログに解除を並べ、決定で保存する", async () => {
    // Arrange
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });
    const permissionName = uiText.admin.permissions.names["permissions.definitionsRead"];

    // Act
    fireEvent.click(screen.getByLabelText(new RegExp(permissionName)));
    fireEvent.click(screen.getByRole("button", { name: "権限を保存" }));
    const dialog = await screen.findByRole("dialog", { name: "権限の変更を保存しますか？" });
    const removedText = within(dialog).getByRole("list", { name: "解除（1）" }).textContent;
    fireEvent.click(within(dialog).getByRole("button", { name: "保存する" }));

    // Assert
    expect(removedText).toBe(permissionName);
    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/groups/group-1/permissions", {
        permissionKeys: []
      });
    });
  });

  it("元に戻すで選択を保存済みの状態へ戻す", async () => {
    // Arrange
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });
    fireEvent.click(screen.getByLabelText(/^member/));

    // Act
    fireEvent.click(screen.getAllByRole("button", { name: "元に戻す" })[0]);

    // Assert
    expect(screen.getByLabelText(/^member/)).toBeChecked();
    expect(screen.getByRole("button", { name: "メンバーを保存" })).toBeDisabled();
  });

  it("取得失敗時にエラー状態を表示する", async () => {
    // Arrange
    vi.mocked(apiGet).mockRejectedValue(new Error("network"));

    // Act
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);

    // Assert
    expect(await screen.findByRole("alert")).toHaveTextContent(uiText.pageState.error);
  });

  it("メンバー保存の失敗をトーストで示し、確認ダイアログは開いたままにする", async () => {
    // Arrange
    vi.mocked(apiPut).mockRejectedValue(new Error("network"));
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });
    fireEvent.click(screen.getByLabelText(/^member/));
    fireEvent.click(screen.getByRole("button", { name: "メンバーを保存" }));

    // Act
    fireEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "保存する" }));

    // Assert
    expect(await screen.findByText("UNKNOWN: Unknown error")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
