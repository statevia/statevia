import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { AppShell } from "@/shared/ui/AppShell";
import { uiText } from "@/shared/i18n/uiText";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import { SIDE_NAV_COOKIE_KEY } from "@/shared/ui/navigation/sideNavState";

const usePathname = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => usePathname()
}));

// jsdom は実遷移を実装しないため、クリック時の既定動作を止めてハンドラだけを呼ぶ。
vi.mock("next/link", () => ({
  default: ({ href, children, onClick, ...rest }: { href: string; children: ReactNode; onClick?: () => void }) => (
    <a
      href={href}
      {...rest}
      onClick={(event) => {
        event.preventDefault();
        onClick?.();
      }}
    >
      {children}
    </a>
  )
}));

vi.mock("next/image", () => ({
  default: ({ alt }: { alt: string }) => <img alt={alt} />
}));

vi.mock("@/shared/ui/ThemeToggle", () => ({
  ThemeToggle: () => <span>theme</span>
}));

vi.mock("@/shared/ui/LanguageToggle", () => ({
  LanguageToggle: () => <span>lang</span>
}));

type RenderOptions = {
  pathname?: string;
  isTenantAdmin?: boolean;
  initialCollapsed?: boolean;
};

/**
 * 日本語辞書でアプリシェルを描画する。
 * @param options URL・権限・畳み状態。省略時は一般利用者が `/dashboard` を展開表示で開く。
 * @returns render 結果。
 */
function renderShell(options: RenderOptions = {}) {
  const { pathname = "/dashboard", isTenantAdmin = false, initialCollapsed = false } = options;
  usePathname.mockReturnValue(pathname);
  return render(
    <UiTextProvider locale="ja">
      <AppShell theme="light" locale="ja" isTenantAdmin={isTenantAdmin} isProjectAdmin={false} initialCollapsed={initialCollapsed}>
        <p>content</p>
      </AppShell>
    </UiTextProvider>
  );
}

describe("AppShell", () => {
  beforeEach(() => {
    usePathname.mockReset();
    document.cookie = `${SIDE_NAV_COOKIE_KEY}=; Path=/; Max-Age=0`;
  });

  it("ログイン画面ではサイドメニューを出さず本文だけを描画する", () => {
    // Arrange & Act
    renderShell({ pathname: "/login" });

    // Assert
    expect(screen.queryByRole("navigation", { name: uiText.navigation.ariaSideNav })).toBeNull();
    expect(screen.getByText("content")).toBeInTheDocument();
  });

  it("一般利用者には管理系項目を初回描画から含めない", () => {
    // Arrange & Act
    renderShell({ isTenantAdmin: false });

    // Assert
    expect(screen.getByRole("link", { name: uiText.navigation.dashboard })).toHaveAttribute("aria-current", "page");
    expect(screen.queryByRole("link", { name: uiText.navigation.adminUsers })).toBeNull();
  });

  it("テナント管理者には管理系項目を初回描画から含める", () => {
    // Arrange & Act
    renderShell({ isTenantAdmin: true, pathname: "/admin/groups/g-1" });

    // Assert
    expect(screen.getByRole("link", { name: uiText.navigation.adminGroups })).toHaveAttribute("aria-current", "page");
  });

  it("畳むボタンでレール表示にし、cookie へ保存する", () => {
    // Arrange
    renderShell();

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.collapse }));

    // Assert
    expect(screen.getByRole("button", { name: uiText.navigation.expand })).toBeInTheDocument();
    expect(document.cookie).toContain(`${SIDE_NAV_COOKIE_KEY}=rail`);
  });

  it("メニューボタンでドロワーを開き、Escape で閉じてボタンへフォーカスを戻す", () => {
    // Arrange
    renderShell();
    const menuButton = screen.getByRole("button", { name: uiText.navigation.openMenu });
    fireEvent.click(menuButton);
    expect(screen.getByRole("dialog", { name: uiText.navigation.ariaSideNav })).toBeInTheDocument();

    // Act
    fireEvent.keyDown(document, { key: "Escape" });

    // Assert
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(menuButton).toHaveFocus();
  });

  it("ドロワー内の項目を押すとドロワーを閉じる", () => {
    // Arrange
    renderShell();
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.openMenu }));
    const dialog = screen.getByRole("dialog", { name: uiText.navigation.ariaSideNav });

    // Act
    fireEvent.click(within(dialog).getByRole("link", { name: uiText.navigation.executions }));

    // Assert
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("ドロワーの最後の要素で Tab を押すと先頭へフォーカスを戻す", () => {
    // Arrange
    renderShell();
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.openMenu }));
    const dialog = screen.getByRole("dialog", { name: uiText.navigation.ariaSideNav });
    const links = within(dialog).getAllByRole("link");
    links.at(-1)?.focus();

    // Act
    fireEvent.keyDown(document, { key: "Tab" });

    // Assert
    expect(within(dialog).getByRole("button", { name: uiText.navigation.closeMenu })).toHaveFocus();
  });
});
