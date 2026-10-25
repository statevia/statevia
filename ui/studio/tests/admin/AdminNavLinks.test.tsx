import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { AdminNavLinks } from "@/shared/ui/AdminNavLinks";
import { renderWithUiText } from "../testUtils";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  )
}));

vi.mock("@/shared/auth/fetchAuthMe", () => ({
  fetchAuthMe: vi.fn()
}));

import { fetchAuthMe } from "@/shared/auth/fetchAuthMe";

describe("AdminNavLinks", () => {
  beforeEach(() => {
    vi.mocked(fetchAuthMe).mockResolvedValue({
      tenantId: "tenant",
      tenantKey: "default",
      principalId: "principal",
      username: "admin",
      email: "admin@example.com",
      isTenantAdmin: true,
      permissions: []
    });
  });

  it("テナント管理者には管理ナビを表示する", async () => {
    renderWithUiText(<AdminNavLinks />);

    expect(await screen.findByRole("link", { name: "ユーザー管理" })).toHaveAttribute("href", "/admin/users");
    expect(screen.getByRole("link", { name: "グループ管理" })).toHaveAttribute("href", "/admin/groups");
    expect(screen.getByRole("link", { name: "API キー" })).toHaveAttribute("href", "/admin/api-keys");
    expect(screen.getByRole("link", { name: "Action バインディング" })).toHaveAttribute(
      "href",
      "/admin/action-bindings"
    );
    expect(screen.getByRole("link", { name: "通知 SMTP" })).toHaveAttribute("href", "/admin/notification-smtp");
    expect(screen.getByRole("link", { name: "プロジェクト" })).toHaveAttribute("href", "/admin/projects");
  });

  it("非管理者には管理ナビを表示しない", async () => {
    vi.mocked(fetchAuthMe).mockResolvedValue({
      tenantId: "tenant",
      tenantKey: "default",
      principalId: "principal",
      username: "member",
      email: "member@example.com",
      isTenantAdmin: false,
      permissions: []
    });

    renderWithUiText(<AdminNavLinks />);

    await waitFor(() => {
      expect(fetchAuthMe).toHaveBeenCalled();
    });
    expect(screen.queryByRole("link", { name: "ユーザー管理" })).not.toBeInTheDocument();
  });
});
