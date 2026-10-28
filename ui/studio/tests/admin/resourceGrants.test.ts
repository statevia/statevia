import { describe, expect, it } from "vitest";
import {
  MAX_GRANTED_DEFINITIONS,
  MAX_GRANTED_PROJECTS,
  buildReplaceResourceGrantsBody,
  resourceGrantsPath
} from "@/features/admin/resourceGrants";

/**
 * 連番の ID を作る。
 * @param prefix 接頭辞。
 * @param count 件数。
 * @returns ID の配列。
 */
function ids(prefix: string, count: number): string[] {
  return Array.from({ length: count }, (_, index) => `${prefix}-${index}`);
}

describe("resourceGrantsPath", () => {
  it("ユーザーは users 配下のパスを ID をエンコードして返す", () => {
    // Arrange
    const subject = { kind: "user", userId: "u/1" } as const;

    // Act
    const path = resourceGrantsPath(subject);

    // Assert
    expect(path).toBe("/admin/users/u%2F1/resource-grants");
  });

  it("ServiceAccount は service-accounts 配下のパスを返す", () => {
    // Arrange
    const subject = { kind: "serviceAccount", serviceAccountId: "sa-1" } as const;

    // Act
    const path = resourceGrantsPath(subject);

    // Assert
    expect(path).toBe("/admin/service-accounts/sa-1/resource-grants");
  });
});

describe("buildReplaceResourceGrantsBody", () => {
  it("両方が空でも両配列を持つ本文を作る", () => {
    // Arrange
    const selection = { projectIds: [], definitionIds: [] };

    // Act
    const result = buildReplaceResourceGrantsBody(selection);

    // Assert
    expect(result).toEqual({ ok: true, body: { projectIds: [], definitionIds: [] } });
  });

  it("重複した ID は 1 件に畳む", () => {
    // Arrange
    const selection = { projectIds: ["p-1", "p-1"], definitionIds: ["d-1", "d-2", "d-1"] };

    // Act
    const result = buildReplaceResourceGrantsBody(selection);

    // Assert
    expect(result).toEqual({ ok: true, body: { projectIds: ["p-1"], definitionIds: ["d-1", "d-2"] } });
  });

  it("project が上限ちょうどなら送れる", () => {
    // Arrange
    const selection = { projectIds: ids("p", MAX_GRANTED_PROJECTS), definitionIds: [] };

    // Act
    const result = buildReplaceResourceGrantsBody(selection);

    // Assert
    expect(result.ok).toBe(true);
  });

  it("project が 33 件なら送らない", () => {
    // Arrange
    const selection = { projectIds: ids("p", MAX_GRANTED_PROJECTS + 1), definitionIds: ["d-1"] };

    // Act
    const result = buildReplaceResourceGrantsBody(selection);

    // Assert
    expect(result).toEqual({ ok: false, errors: { projectIds: "tooMany" } });
  });

  it("定義が 129 件なら送らない", () => {
    // Arrange
    const selection = { projectIds: [], definitionIds: ids("d", MAX_GRANTED_DEFINITIONS + 1) };

    // Act
    const result = buildReplaceResourceGrantsBody(selection);

    // Assert
    expect(result).toEqual({ ok: false, errors: { definitionIds: "tooMany" } });
  });

  it("重複を畳んだあとの件数で上限を判定する", () => {
    // Arrange
    const selection = { projectIds: [...ids("p", MAX_GRANTED_PROJECTS), "p-0"], definitionIds: [] };

    // Act
    const result = buildReplaceResourceGrantsBody(selection);

    // Assert
    expect(result.ok).toBe(true);
  });
});
