import { describe, expect, it } from "vitest";
import {
  MAX_GRANTED_DEFINITIONS,
  MAX_GRANTED_PROJECTS,
  buildReplaceResourceGrantsBody,
  clearedGrantFields,
  definitionsOutsideSelectedProjects,
  groupDefinitionCandidates,
  idsOutsideCandidates,
  resourceGrantsPath,
  type ResourceGrantDefinitionCandidate
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

/**
 * 定義候補を作る。
 * @param resourceId 定義 ID。
 * @param projectId 所属 Project。
 * @returns 候補 1 件。
 */
function candidate(resourceId: string, projectId: string | null): ResourceGrantDefinitionCandidate {
  return { resourceId, name: `def-${resourceId}`, displayId: resourceId, projectId };
}

describe("groupDefinitionCandidates", () => {
  it("Project 名順にまとめ、所属が分からない定義は最後の組に置く", () => {
    // Arrange
    const candidates = [candidate("d-1", "p-ops"), candidate("d-2", null), candidate("d-3", "p-billing"), candidate("d-4", "p-gone")];
    const projects = [
      { projectId: "p-ops", displayName: "Ops" },
      { projectId: "p-billing", displayName: "Billing" }
    ];

    // Act
    const groups = groupDefinitionCandidates(candidates, projects);

    // Assert
    expect(groups.map((group) => [group.projectName, group.definitions.map((definition) => definition.resourceId)])).toEqual([
      ["Billing", ["d-3"]],
      ["Ops", ["d-1"]],
      [null, ["d-2", "d-4"]]
    ]);
  });
});

describe("idsOutsideCandidates", () => {
  it("候補に無い選択中の ID だけを選択の並びで返す", () => {
    // Arrange
    const selected = new Set(["d-9", "d-1", "d-8"]);

    // Act
    const outside = idsOutsideCandidates(selected, ["d-1", "d-2"]);

    // Assert
    expect(outside).toEqual(["d-9", "d-8"]);
  });
});

describe("definitionsOutsideSelectedProjects", () => {
  it("project を選んでいないときは注意する定義が無い", () => {
    // Arrange
    const selection = { projectIds: new Set<string>(), definitionIds: new Set(["d-1"]) };

    // Act
    const outside = definitionsOutsideSelectedProjects(selection, [candidate("d-1", "p-1")]);

    // Assert
    expect(outside).toEqual([]);
  });

  it("選んだ project の外にある選択中の定義だけを返し、所属が分からない定義は判定しない", () => {
    // Arrange
    const selection = { projectIds: new Set(["p-1"]), definitionIds: new Set(["d-1", "d-2", "d-3"]) };
    const candidates = [candidate("d-1", "p-1"), candidate("d-2", "p-2"), candidate("d-3", null), candidate("d-4", "p-2")];

    // Act
    const outside = definitionsOutsideSelectedProjects(selection, candidates);

    // Assert
    expect(outside.map((definition) => definition.resourceId)).toEqual(["d-2"]);
  });
});

describe("clearedGrantFields", () => {
  it("付いていた種別が空になるときだけ返す", () => {
    // Arrange
    const saved = { projectIds: ["p-1"], definitionIds: [] };
    const body = { projectIds: [], definitionIds: [] };

    // Act
    const cleared = clearedGrantFields(saved, body);

    // Assert
    expect(cleared).toEqual(["projectIds"]);
  });
});
