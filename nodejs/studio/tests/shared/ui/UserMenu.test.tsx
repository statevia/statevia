import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { UserMenu } from "@/shared/ui/UserMenu";
import { uiText } from "@/shared/i18n/uiText";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import { clearSessionAndRedirectToLogin } from "@/shared/auth/authRedirect";

vi.mock("@/shared/auth/authRedirect", () => ({
  clearSessionAndRedirectToLogin: vi.fn()
}));

/**
 * 日本語辞書でユーザーメニューを描画し、開いた状態にする。
 */
function renderOpenedMenu() {
  render(
    <UiTextProvider locale="ja">
      <UserMenu />
    </UiTextProvider>
  );
  fireEvent.click(screen.getByRole("button", { name: uiText.navigation.userMenu }));
}

describe("UserMenu", () => {
  beforeEach(() => {
    vi.mocked(clearSessionAndRedirectToLogin).mockClear();
  });

  it("開くとアカウントとログアウトを出す", () => {
    // Arrange & Act
    renderOpenedMenu();

    // Assert
    expect(screen.getByRole("button", { name: uiText.navigation.userMenu })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("link", { name: uiText.navigation.account })).toHaveAttribute("href", "/account");
    expect(screen.getByRole("button", { name: uiText.navigation.logout })).toBeInTheDocument();
  });

  it("ログアウト押下でセッション破棄を from なしで呼ぶ", () => {
    // Arrange
    renderOpenedMenu();

    // Act
    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.logout }));

    // Assert
    expect(clearSessionAndRedirectToLogin).toHaveBeenCalledWith({ includeFrom: false });
  });

  it("Escape で閉じてボタンへフォーカスを戻す", () => {
    // Arrange
    renderOpenedMenu();

    // Act
    fireEvent.keyDown(document, { key: "Escape" });

    // Assert
    expect(screen.queryByRole("link", { name: uiText.navigation.account })).toBeNull();
    expect(screen.getByRole("button", { name: uiText.navigation.userMenu })).toHaveFocus();
  });

  it("外側を押すと閉じる", () => {
    // Arrange
    renderOpenedMenu();

    // Act
    fireEvent.mouseDown(document.body);

    // Assert
    expect(screen.queryByRole("link", { name: uiText.navigation.account })).toBeNull();
  });
});
