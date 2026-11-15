import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { AppHeader } from "@/shared/ui/AppHeader";
import { uiText } from "@/shared/i18n/uiText";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";

vi.mock("@/shared/ui/ThemeToggle", () => ({
  ThemeToggle: () => <span data-testid="theme-toggle">theme</span>
}));

vi.mock("@/shared/ui/LanguageToggle", () => ({
  LanguageToggle: () => <span data-testid="language-toggle">lang</span>
}));

vi.mock("@/shared/ui/UserMenu", () => ({
  UserMenu: () => <span data-testid="user-menu">user</span>
}));

/**
 * 日本語辞書でヘッダーを描画する。
 * @param showNavigation ログイン済み画面か。
 * @param onOpenNavigation メニューボタン押下時。
 * @returns render 結果。
 */
function renderHeader(showNavigation: boolean, onOpenNavigation?: () => void) {
  return render(
    <UiTextProvider locale="ja">
      <AppHeader theme="dark" locale="ja" showNavigation={showNavigation} onOpenNavigation={onOpenNavigation} />
    </UiTextProvider>
  );
}

describe("AppHeader", () => {
  it("ログイン済み画面ではメニューボタン・テーマ・言語・ユーザーメニューを出し、画面リンクは持たない", () => {
    // Arrange & Act
    renderHeader(true);

    // Assert
    expect(screen.getByRole("button", { name: uiText.navigation.openMenu })).toBeInTheDocument();
    expect(screen.getByTestId("theme-toggle")).toBeInTheDocument();
    expect(screen.getByTestId("language-toggle")).toBeInTheDocument();
    expect(screen.getByTestId("user-menu")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: uiText.navigation.definitions })).toBeNull();
    expect(document.querySelector('header a[href="/dashboard"]')).not.toBeNull();
  });

  it("ログイン画面ではメニューボタンとユーザーメニューを出さない", () => {
    // Arrange & Act
    renderHeader(false);

    // Assert
    expect(screen.queryByRole("button", { name: uiText.navigation.openMenu })).toBeNull();
    expect(screen.queryByTestId("user-menu")).toBeNull();
    expect(screen.getByTestId("theme-toggle")).toBeInTheDocument();
    expect(document.querySelector('header a[href="/login"]')).not.toBeNull();
  });

  it("ブランドのリンクに枠なしのマークを出し、狭い幅でも名前を読み上げられる", () => {
    // Arrange & Act
    renderHeader(true);

    // Assert
    const brandLink = screen.getByRole("link", { name: "statevia" });
    expect(brandLink).toHaveAttribute("href", "/dashboard");
    expect(brandLink.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
    expect(brandLink.querySelector("img")).toBeNull();
  });

  it("メニューボタン押下で親へ通知する", () => {
    // Arrange
    const onOpenNavigation = vi.fn();
    renderHeader(true, onOpenNavigation);

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.openMenu }));

    // Assert
    expect(onOpenNavigation).toHaveBeenCalledTimes(1);
  });
});
