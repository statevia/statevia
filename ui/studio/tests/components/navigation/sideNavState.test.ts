import { afterEach, describe, expect, it } from "vitest";
import { resolveSideNavCollapsed, SIDE_NAV_COOKIE_KEY, storeSideNavCollapsed } from "@/shared/ui/navigation/sideNavState";

describe("resolveSideNavCollapsed", () => {
  it("rail のときレール表示とみなす", () => {
    // Arrange & Act
    const collapsed = resolveSideNavCollapsed("rail");

    // Assert
    expect(collapsed).toBe(true);
  });

  it("expanded のとき展開表示とみなす", () => {
    // Arrange & Act
    const collapsed = resolveSideNavCollapsed("expanded");

    // Assert
    expect(collapsed).toBe(false);
  });

  it("不正値は展開表示とみなす", () => {
    // Arrange & Act
    const collapsed = resolveSideNavCollapsed("<script>");

    // Assert
    expect(collapsed).toBe(false);
  });

  it("cookie が無いときは展開表示とみなす", () => {
    // Arrange & Act
    const collapsed = resolveSideNavCollapsed(undefined);

    // Assert
    expect(collapsed).toBe(false);
  });
});

describe("storeSideNavCollapsed", () => {
  afterEach(() => {
    document.cookie = `${SIDE_NAV_COOKIE_KEY}=; Path=/; Max-Age=0`;
  });

  it("レール表示を cookie に rail として保存する", () => {
    // Arrange & Act
    storeSideNavCollapsed(true);

    // Assert
    expect(document.cookie).toContain(`${SIDE_NAV_COOKIE_KEY}=rail`);
  });
});
