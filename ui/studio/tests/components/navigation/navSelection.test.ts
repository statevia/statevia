import { LayoutDashboard } from "lucide-react";
import { describe, expect, it } from "vitest";
import { NAV_GROUPS, type NavGroup } from "@/shared/ui/navigation/navItems";
import { resolveActiveItemId, selectVisibleGroups } from "@/shared/ui/navigation/navSelection";

describe("selectVisibleGroups", () => {
  it("一般利用者にはワークフローグループだけを返す", () => {
    // Arrange
    const context = { isTenantAdmin: false, isProjectAdmin: false, canCreateProject: false };

    // Act
    const groups = selectVisibleGroups(NAV_GROUPS, context);

    // Assert
    expect(groups.map((group) => group.id)).toEqual(["workflow"]);
  });

  it("Project 管理者にはプロジェクトグループのメンバーも返す", () => {
    // Arrange
    const context = { isTenantAdmin: false, isProjectAdmin: true, canCreateProject: false };

    // Act
    const groups = selectVisibleGroups(NAV_GROUPS, context);

    // Assert
    expect(groups.map((group) => group.id)).toEqual(["workflow", "project"]);
    expect(groups[1]?.items.map((item) => item.id)).toEqual(["projectMembers"]);
  });

  it("テナント管理者には管理系 2 グループを定義順で返す", () => {
    // Arrange
    const context = { isTenantAdmin: true, isProjectAdmin: false, canCreateProject: false };

    // Act
    const groups = selectVisibleGroups(NAV_GROUPS, context);

    // Assert
    expect(groups.map((group) => group.id)).toEqual(["workflow", "tenantAccess", "tenantSettings"]);
  });

  it("テナント管理者のアクセス管理グループは API キーの次に ServiceAccount を持つ", () => {
    // Arrange
    const context = { isTenantAdmin: true, isProjectAdmin: false, canCreateProject: false };

    // Act
    const groups = selectVisibleGroups(NAV_GROUPS, context);

    // Assert
    const tenantAccess = groups.find((group) => group.id === "tenantAccess");
    expect(tenantAccess?.items.map((item) => item.href)).toEqual([
      "/admin/users",
      "/admin/groups",
      "/admin/api-keys",
      "/admin/service-accounts",
    ]);
  });

  it("projects.create を持てば所属 0 件でもプロジェクトグループに作成だけを返す", () => {
    // Arrange
    const context = { isTenantAdmin: false, isProjectAdmin: false, canCreateProject: true };

    // Act
    const groups = selectVisibleGroups(NAV_GROUPS, context);

    // Assert
    expect(groups.map((group) => group.id)).toEqual(["workflow", "project"]);
    expect(groups[1]?.items.map((item) => item.id)).toEqual(["projectCreate"]);
  });
});

describe("resolveActiveItemId", () => {
  const allGroups = selectVisibleGroups(NAV_GROUPS, { isTenantAdmin: true, isProjectAdmin: true, canCreateProject: true });

  it("作成画面ではメンバーではなく作成の項目を選択する", () => {
    // Arrange
    const pathname = "/projects/new";

    // Act
    const activeItemId = resolveActiveItemId(allGroups, pathname);

    // Assert
    expect(activeItemId).toBe("projectCreate");
  });

  it("配下の深い URL では親の項目を選択する", () => {
    // Arrange
    const pathname = "/definitions/abc/edit";

    // Act
    const activeItemId = resolveActiveItemId(allGroups, pathname);

    // Assert
    expect(activeItemId).toBe("definitions");
  });

  it("管理画面の詳細 URL では一覧の項目を選択する", () => {
    // Arrange
    const pathname = "/admin/groups/group-1";

    // Act
    const activeItemId = resolveActiveItemId(allGroups, pathname);

    // Assert
    expect(activeItemId).toBe("adminGroups");
  });

  it("ServiceAccount の詳細 URL では ServiceAccount の項目を選択する", () => {
    // Arrange
    const pathname = "/admin/service-accounts/sa-1";

    // Act
    const activeItemId = resolveActiveItemId(allGroups, pathname);

    // Assert
    expect(activeItemId).toBe("adminServiceAccounts");
  });

  it("接頭辞の境界が `/` でない URL は一致させない", () => {
    // Arrange
    const pathname = "/definitions-archive";

    // Act
    const activeItemId = resolveActiveItemId(allGroups, pathname);

    // Assert
    expect(activeItemId).toBeNull();
  });

  it("どの項目の配下でもない URL では null を返す", () => {
    // Arrange
    const pathname = "/account";

    // Act
    const activeItemId = resolveActiveItemId(allGroups, pathname);

    // Assert
    expect(activeItemId).toBeNull();
  });

  it("複数の接頭辞に一致するときは最も長い接頭辞の項目を選択する", () => {
    // Arrange
    const groups: NavGroup[] = [
      {
        id: "workflow",
        items: [
          { id: "dashboard", href: "/admin", matchPrefixes: ["/admin"], visibility: "all", icon: LayoutDashboard },
          { id: "executions", href: "/admin/users", matchPrefixes: ["/admin/users"], visibility: "all", icon: LayoutDashboard },
        ],
      },
    ];

    // Act
    const activeItemId = resolveActiveItemId(groups, "/admin/users/u-1");

    // Assert
    expect(activeItemId).toBe("executions");
  });
});
