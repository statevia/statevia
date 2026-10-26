import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { uiText } from "@/shared/i18n/uiText";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import { NAV_GROUPS } from "@/shared/ui/navigation/navItems";
import { selectVisibleGroups } from "@/shared/ui/navigation/navSelection";
import { SideNav, type SideNavMode } from "@/shared/ui/navigation/SideNav";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: ReactNode }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const adminGroups = selectVisibleGroups(NAV_GROUPS, { isTenantAdmin: true, isProjectAdmin: true, canCreateProject: true });
const memberGroups = selectVisibleGroups(NAV_GROUPS, { isTenantAdmin: false, isProjectAdmin: false, canCreateProject: false });

type RenderOptions = {
  mode?: SideNavMode;
  groups?: typeof adminGroups;
  activeItemId?: Parameters<typeof SideNav>[0]["activeItemId"];
  projectSlot?: ReactNode;
  onToggleCollapse?: () => void;
};

/**
 * 日本語辞書でサイドメニューを描画する。
 * @param options 表示モード等。省略時はテナント管理者の展開表示。
 * @returns render 結果。
 */
function renderSideNav(options: RenderOptions = {}) {
  const { mode = "expanded", groups = adminGroups, activeItemId = null, projectSlot, onToggleCollapse } = options;
  return render(
    <UiTextProvider locale="ja">
      <SideNav
        mode={mode}
        groups={groups}
        activeItemId={activeItemId}
        projectSlot={projectSlot}
        onToggleCollapse={onToggleCollapse}
      />
    </UiTextProvider>,
  );
}

describe("SideNav", () => {
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("一般利用者には管理系グループの見出しを出さない", () => {
    // Arrange & Act
    renderSideNav({ groups: memberGroups });

    // Assert
    expect(screen.getByRole("heading", { name: uiText.navigation.groups.workflow })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: uiText.navigation.groups.tenantAccess })).toBeNull();
    expect(screen.queryByRole("link", { name: uiText.navigation.adminUsers })).toBeNull();
  });

  it("テナント管理者には管理系の項目を遷移先つきで出す", () => {
    // Arrange & Act
    renderSideNav();

    // Assert
    expect(screen.getByRole("link", { name: uiText.navigation.adminUsers })).toHaveAttribute("href", "/admin/users");
    expect(screen.getByRole("link", { name: uiText.navigation.adminNotificationSmtp })).toHaveAttribute(
      "href",
      "/admin/notification-smtp",
    );
  });

  it("選択中の項目にだけ aria-current=page を付ける", () => {
    // Arrange & Act
    renderSideNav({ activeItemId: "definitions" });

    // Assert
    expect(screen.getByRole("link", { name: uiText.navigation.definitions })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: uiText.navigation.dashboard })).not.toHaveAttribute("aria-current");
  });

  it("グループ見出しを押すと項目を畳み、開閉状態を保存する", () => {
    // Arrange
    renderSideNav();

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.groups.tenantAccess }));

    // Assert
    expect(screen.queryByRole("link", { name: uiText.navigation.adminUsers })).toBeNull();
    expect(localStorage.getItem("statevia.sidenav.groups")).toBe(JSON.stringify(["tenantAccess"]));
  });

  it("保存で閉じたグループでも現在地を含むなら開いて表示する", () => {
    // Arrange
    localStorage.setItem("statevia.sidenav.groups", JSON.stringify(["tenantAccess"]));

    // Act
    renderSideNav({ activeItemId: "adminUsers" });

    // Assert
    expect(screen.getByRole("link", { name: uiText.navigation.adminUsers })).toBeInTheDocument();
  });

  it("現在地を含むグループも見出しを押せば閉じられる", () => {
    // Arrange
    renderSideNav({ activeItemId: "adminNotificationSmtp" });

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.groups.tenantSettings }));

    // Assert
    expect(screen.queryByRole("link", { name: uiText.navigation.adminNotificationSmtp })).toBeNull();
    expect(screen.getByRole("button", { name: uiText.navigation.groups.tenantSettings })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("閉じたグループ内の項目へ現在地が移ると、そのグループを開く", () => {
    // Arrange
    localStorage.setItem("statevia.sidenav.groups", JSON.stringify(["tenantAccess"]));
    const { rerender } = renderSideNav({ activeItemId: "dashboard" });
    expect(screen.queryByRole("link", { name: uiText.navigation.adminUsers })).toBeNull();

    // Act
    rerender(
      <UiTextProvider locale="ja">
        <SideNav mode="expanded" groups={adminGroups} activeItemId="adminUsers" />
      </UiTextProvider>,
    );

    // Assert
    expect(screen.getByRole("link", { name: uiText.navigation.adminUsers })).toBeInTheDocument();
  });

  it("localStorage が使えなくても描画し、開閉できる", () => {
    // Arrange
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    renderSideNav();

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.groups.workflow }));

    // Assert
    expect(screen.queryByRole("link", { name: uiText.navigation.dashboard })).toBeNull();
  });

  it("レール表示ではラベルを aria-label で持ち、テキストを出さない", () => {
    // Arrange & Act
    renderSideNav({ mode: "rail" });

    // Assert
    const link = screen.getByRole("link", { name: uiText.navigation.dashboard });
    expect(link).toHaveAttribute("title", uiText.navigation.dashboard);
    expect(link).not.toHaveTextContent(uiText.navigation.dashboard);
  });

  it("レール表示で Project アイコンを押すとメニューを広げる", () => {
    // Arrange
    const onToggleCollapse = vi.fn();
    renderSideNav({ mode: "rail", projectSlot: <span>project</span>, onToggleCollapse });

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.projects.switcher.label }));

    // Assert
    expect(onToggleCollapse).toHaveBeenCalledTimes(1);
  });

  it("展開表示では畳むボタンを出し、押すと通知する", () => {
    // Arrange
    const onToggleCollapse = vi.fn();
    renderSideNav({ onToggleCollapse });

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.collapse }));

    // Assert
    expect(onToggleCollapse).toHaveBeenCalledTimes(1);
  });

  it("ドロワー表示では畳むボタンを出さない", () => {
    // Arrange & Act
    renderSideNav({ mode: "drawer", onToggleCollapse: vi.fn() });

    // Assert
    expect(screen.queryByRole("button", { name: uiText.navigation.collapse })).toBeNull();
  });
});
