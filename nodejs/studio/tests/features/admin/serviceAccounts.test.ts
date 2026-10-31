import { describe, expect, it } from "vitest";
import {
  buildCreateServiceAccountBody,
  buildUpdateServiceAccountBody
} from "@/features/admin/serviceAccounts";
import type { AdminServiceAccount } from "@/features/admin/types";

const current: AdminServiceAccount = {
  serviceAccountId: "sa-1",
  principalId: "pr-1",
  name: "nightly-runner",
  isActive: true,
  hasApiKey: false,
  groupIds: ["g-1", "g-2"],
  createdAt: "2026-10-01T00:00:00Z"
};

describe("service account create body", () => {
  it("名前を trim し、name と groupIds だけを送る", () => {
    // Arrange
    const input = { name: "  nightly-runner  ", groupIds: ["g-1"] };

    // Act
    const result = buildCreateServiceAccountBody(input);

    // Assert
    expect(result).toEqual({ ok: true, body: { name: "nightly-runner", groupIds: ["g-1"] } });
  });

  it("グループが 0 件なら送らない", () => {
    // Arrange
    const input = { name: "nightly-runner", groupIds: [] };

    // Act
    const result = buildCreateServiceAccountBody(input);

    // Assert
    expect(result).toEqual({ ok: false, errors: { name: undefined, groupIds: "required" } });
  });

  it("名前が ASCII ラベルでないなら送らない", () => {
    // Arrange
    const input = { name: "夜間ジョブ", groupIds: ["g-1"] };

    // Act
    const result = buildCreateServiceAccountBody(input);

    // Assert
    expect(result).toEqual({ ok: false, errors: { name: "format", groupIds: undefined } });
  });

  it("名前が 128 文字を超えるなら送らない", () => {
    // Arrange
    const input = { name: "a".repeat(129), groupIds: ["g-1"] };

    // Act
    const result = buildCreateServiceAccountBody(input);

    // Assert
    expect(result).toEqual({ ok: false, errors: { name: "tooLong", groupIds: undefined } });
  });

  it("重複したグループ ID は 1 件にして送る", () => {
    // Arrange
    const input = { name: "nightly-runner", groupIds: ["g-1", "g-1", "g-2"] };

    // Act
    const result = buildCreateServiceAccountBody(input);

    // Assert
    expect(result).toEqual({ ok: true, body: { name: "nightly-runner", groupIds: ["g-1", "g-2"] } });
  });
});

describe("service account update body", () => {
  it("有効フラグだけを変えたら isActive だけを送る", () => {
    // Arrange
    const input = { isActive: false, name: "nightly-runner", groupIds: ["g-1", "g-2"] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "ready", body: { isActive: false } });
  });

  it("名前を変えたら trim した displayName だけを送る", () => {
    // Arrange
    const input = { isActive: true, name: " weekly-runner ", groupIds: ["g-1", "g-2"] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "ready", body: { displayName: "weekly-runner" } });
  });

  it("グループの並びだけが違うときは変更なしとして送らない", () => {
    // Arrange
    const input = { isActive: true, name: "nightly-runner", groupIds: ["g-2", "g-1"] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "unchanged" });
  });

  it("グループを差し替えたら groupIds だけを送る", () => {
    // Arrange
    const input = { isActive: true, name: "nightly-runner", groupIds: ["g-3"] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "ready", body: { groupIds: ["g-3"] } });
  });

  it("グループを空にしたら送らない", () => {
    // Arrange
    const input = { isActive: true, name: "nightly-runner", groupIds: [] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "invalid", errors: { name: undefined, groupIds: "required" } });
  });

  it("名前を空にしたら空文字の displayName を送らない", () => {
    // Arrange
    const input = { isActive: true, name: "   ", groupIds: ["g-1", "g-2"] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "invalid", errors: { name: "required", groupIds: undefined } });
  });

  it("何も変えていなければ変更なしとして送らない", () => {
    // Arrange
    const input = { isActive: true, name: "nightly-runner", groupIds: ["g-1", "g-2"] };

    // Act
    const result = buildUpdateServiceAccountBody(current, input);

    // Assert
    expect(result).toEqual({ kind: "unchanged" });
  });
});
