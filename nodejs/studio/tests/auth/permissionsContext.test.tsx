import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { PermissionsProvider, usePermissions } from "@/shared/auth/permissionsContext";

let pathnameValue = "/definitions";

vi.mock("next/navigation", () => ({
  usePathname: () => pathnameValue,
}));

vi.mock("@/shared/auth/fetchAuthMe", () => ({
  fetchAuthMe: vi.fn(),
}));

import { fetchAuthMe } from "@/shared/auth/fetchAuthMe";

/** 取得状態と executions.write の有無を文字で出す。 */
function Probe() {
  const { loaded, has } = usePermissions();
  return <output>{loaded ? `loaded:${String(has("executions.write"))}` : "loading"}</output>;
}

describe("PermissionsProvider", () => {
  beforeEach(() => {
    pathnameValue = "/definitions";
    vi.mocked(fetchAuthMe).mockReset();
  });

  it("auth/me の permissions を権限として使う", async () => {
    // Arrange
    vi.mocked(fetchAuthMe).mockResolvedValue({
      tenantId: "t",
      tenantKey: "default",
      principalId: "p",
      username: "member",
      email: null,
      isTenantAdmin: false,
      permissions: ["executions.read", "executions.write"],
    });

    // Act
    render(
      <PermissionsProvider>
        <Probe />
      </PermissionsProvider>,
    );

    // Assert
    expect(await screen.findByText("loaded:true")).toBeInTheDocument();
  });

  it("auth/me が取れないときは権限なしとして扱う", async () => {
    // Arrange
    vi.mocked(fetchAuthMe).mockRejectedValue(new Error("network"));

    // Act
    render(
      <PermissionsProvider>
        <Probe />
      </PermissionsProvider>,
    );

    // Assert
    expect(await screen.findByText("loaded:false")).toBeInTheDocument();
  });

  it("ログイン画面では auth/me を呼ばない", async () => {
    // Arrange
    pathnameValue = "/login";

    // Act
    render(
      <PermissionsProvider>
        <Probe />
      </PermissionsProvider>,
    );

    // Assert
    expect(await screen.findByText("loaded:false")).toBeInTheDocument();
    expect(fetchAuthMe).not.toHaveBeenCalled();
  });
});
