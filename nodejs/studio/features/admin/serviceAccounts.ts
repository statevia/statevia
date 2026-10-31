import { ASCII_LABEL_DEFAULT_MAX_LENGTH, ASCII_LABEL_PATTERN } from "@/shared/lib/validation/formRules";
import { matchesPattern } from "@/shared/lib/validation/primitives";
import type { AdminServiceAccount } from "./types";

/**
 * `POST /v1/admin/service-accounts` の本文。`name` と `groupIds` だけを送る。
 * 契約は `docs/specifications/api-http.md` §4.1.3。API キーの入力は持たない。
 */
export type CreateServiceAccountBody = {
  name: string;
  groupIds: string[];
};

/** `PATCH /v1/admin/service-accounts/{id}` の本文。変えた項目だけを持つ。 */
export type UpdateServiceAccountBody = {
  isActive?: boolean;
  displayName?: string;
  groupIds?: string[];
};

/** 作成フォームの入力（未加工）。 */
export type ServiceAccountCreateInput = {
  name: string;
  groupIds: readonly string[];
};

/** 詳細フォームの入力（未加工）。 */
export type ServiceAccountUpdateInput = {
  isActive: boolean;
  name: string;
  groupIds: readonly string[];
};

/** 項目エラーを出す欄。 */
export type ServiceAccountField = "name" | "groupIds";

/**
 * 項目エラーの種類。文言は画面側の辞書で出す。
 * - `required`: 名前が trim 後に空、またはグループが 0 件
 * - `tooLong`: 名前が trim 後に 128 文字を超える
 * - `format`: 名前が ASCII ラベルでない
 */
export type ServiceAccountFieldErrorKind = "required" | "tooLong" | "format";

/** 欄ごとの項目エラー。 */
export type ServiceAccountFieldErrors = Partial<Record<ServiceAccountField, ServiceAccountFieldErrorKind>>;

/** 作成本文の組み立て結果。 */
export type ServiceAccountCreateBuildResult =
  | { ok: true; body: CreateServiceAccountBody }
  | { ok: false; errors: ServiceAccountFieldErrors };

/**
 * 部分更新本文の組み立て結果。
 * `unchanged` は送る項目が無いことを表し、空の PATCH は送らない。
 */
export type ServiceAccountUpdateBuildResult =
  | { kind: "ready"; body: UpdateServiceAccountBody }
  | { kind: "invalid"; errors: ServiceAccountFieldErrors }
  | { kind: "unchanged" };

/**
 * 名前を Service API の `AsciiLabelConstraints` と同じ規則で検査する。
 * @param name trim 済みの名前。
 * @returns 違反の種類。問題なければ undefined。
 */
function checkName(name: string): ServiceAccountFieldErrorKind | undefined {
  if (name.length === 0) return "required";
  if (name.length > ASCII_LABEL_DEFAULT_MAX_LENGTH) return "tooLong";
  if (!matchesPattern(name, ASCII_LABEL_PATTERN)) return "format";
  return undefined;
}

/**
 * グループ ID の重複を除く。並びは最初に現れた順を保つ。
 * @param groupIds 選択中のグループ ID。
 * @returns 重複の無い ID。
 */
function distinctGroupIds(groupIds: readonly string[]): string[] {
  return [...new Set(groupIds)];
}

/**
 * 2 つのグループ選択が集合として等しいか。並び順は比べない。
 * @param left 重複の無い ID。
 * @param right 重複の無い ID。
 * @returns 同じ集合なら true。
 */
function sameGroupSet(left: readonly string[], right: readonly string[]): boolean {
  const rightSet = new Set(right);
  return left.length === rightSet.size && left.every((groupId) => rightSet.has(groupId));
}

/**
 * 作成本文を作る。名前は trim し、グループが 0 件なら送らない。
 * @param input フォームの入力。
 * @returns 送信できる本文、または欄ごとの項目エラー。
 */
export function buildCreateServiceAccountBody(input: ServiceAccountCreateInput): ServiceAccountCreateBuildResult {
  const name = input.name.trim();
  const groupIds = distinctGroupIds(input.groupIds);
  const errors: ServiceAccountFieldErrors = {
    name: checkName(name),
    groupIds: groupIds.length === 0 ? "required" : undefined
  };
  if (errors.name !== undefined || errors.groupIds !== undefined) {
    return { ok: false, errors };
  }
  return { ok: true, body: { name, groupIds } };
}

/**
 * 現在値と比べて、変えた項目だけの部分更新本文を作る。
 * 名前は trim して比べ、空文字では送らない。グループは並び順を無視して比べ、0 件なら送らない。
 * @param current `GET` で読んだ現在値。
 * @param input 詳細フォームの入力。
 * @returns 送信できる本文、項目エラー、または変更なし。
 */
export function buildUpdateServiceAccountBody(
  current: AdminServiceAccount,
  input: ServiceAccountUpdateInput
): ServiceAccountUpdateBuildResult {
  const name = input.name.trim();
  const groupIds = distinctGroupIds(input.groupIds);
  const nameChanged = name !== current.name;
  const groupsChanged = !sameGroupSet(groupIds, distinctGroupIds(current.groupIds));

  const errors: ServiceAccountFieldErrors = {
    name: nameChanged ? checkName(name) : undefined,
    groupIds: groupsChanged && groupIds.length === 0 ? "required" : undefined
  };
  if (errors.name !== undefined || errors.groupIds !== undefined) {
    return { kind: "invalid", errors };
  }

  const body: UpdateServiceAccountBody = {
    ...(input.isActive === current.isActive ? {} : { isActive: input.isActive }),
    ...(nameChanged ? { displayName: name } : {}),
    ...(groupsChanged ? { groupIds } : {})
  };
  return Object.keys(body).length === 0 ? { kind: "unchanged" } : { kind: "ready", body };
}
