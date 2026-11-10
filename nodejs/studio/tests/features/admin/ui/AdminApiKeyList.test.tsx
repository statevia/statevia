import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { AdminApiKeyListItem, PermissionDefinitionDto } from "@/features/admin/types";
import { AdminApiKeyList } from "@/features/admin/ui/AdminApiKeyList";
import { renderWithUiText } from "@/tests/testUtils";

const permissions: PermissionDefinitionDto[] = [
  {
    permissionKey: "modules.read",
    displayLabel: "Read action modules",
    displayKey: "permissions.modulesRead",
    isSystem: true,
    isDeprecated: false
  },
  {
    permissionKey: "modules.reload",
    displayLabel: "Reload action modules",
    displayKey: "permissions.modulesReload",
    isSystem: true,
    isDeprecated: false
  }
];

/**
 * 許可スコープだけを変えた API キー 1 件を作る。
 * @param allowedScopes 許可スコープ。
 * @returns API キー一覧の 1 件。
 */
function apiKeyWithScopes(allowedScopes: string[]): AdminApiKeyListItem {
  return {
    apiKeyId: "key-1",
    name: "module-full",
    keyPrefix: "stv_abcd",
    allowedScopes,
    expiresAt: null,
    lastUsedAt: null,
    createdAt: "2026-06-01T00:00:00Z",
    isActive: true
  };
}

describe("AdminApiKeyList", () => {
  it("許可スコープを権限キーではなく権限名で並べる", () => {
    // Arrange
    const apiKey = apiKeyWithScopes(["modules.reload", "modules.read"]);

    // Act
    renderWithUiText(
      <AdminApiKeyList apiKeys={[apiKey]} permissions={permissions} revokingId={null} onRevoke={() => {}} />
    );

    // Assert
    expect(screen.getByText(/アクションモジュールの再読み込み、アクションモジュールの閲覧/)).toBeInTheDocument();
    expect(screen.queryByText(/modules\.reload/)).not.toBeInTheDocument();
  });

  it("カタログに無い権限キーはキーのまま出す", () => {
    // Arrange
    const apiKey = apiKeyWithScopes(["modules.read", "future.permission"]);

    // Act
    renderWithUiText(
      <AdminApiKeyList apiKeys={[apiKey]} permissions={permissions} revokingId={null} onRevoke={() => {}} />
    );

    // Assert
    expect(screen.getByText(/アクションモジュールの閲覧、future\.permission/)).toBeInTheDocument();
  });
});
