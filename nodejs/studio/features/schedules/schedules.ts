import { ASCII_LABEL_DEFAULT_MAX_LENGTH, ASCII_LABEL_PATTERN } from "@/shared/lib/validation/formRules";
import { matchesPattern } from "@/shared/lib/validation/primitives";
import type { ScheduleDetail, ScheduleOverlapPolicy } from "./types";

/**
 * `POST /v1/schedules` の本文。`queue` と `definitionVersionId` は送らない（画面に出さない）。
 * `timeZone` は必須で、空を UTC で埋めない。
 */
export type CreateScheduleBody = {
  name: string;
  /** 定義のリソース UUID（API は display ID も受け付ける）。 */
  definitionId: string;
  cronExpression: string;
  timeZone: string;
  overlapPolicy: ScheduleOverlapPolicy;
  runAsPrincipalId: string;
  enabled: boolean;
  /** 版番号。省略時は発火時点の最新版。 */
  definitionVersion?: number;
  /** Start に渡す input。空なら付けない。 */
  input?: unknown;
};

/** `PATCH /v1/schedules/{id}` の本文。変えた項目だけを持つ。 */
export type UpdateScheduleBody = Partial<CreateScheduleBody>;

/** 作成・編集フォームの入力（未加工）。 */
export type ScheduleFormValues = {
  name: string;
  definitionId: string;
  cronExpression: string;
  timeZone: string;
  overlapPolicy: ScheduleOverlapPolicy;
  runAsPrincipalId: string;
  /** 版番号の文字列。空なら送らない。 */
  definitionVersion: string;
  /** input の JSON テキスト。空なら送らない。 */
  inputText: string;
  enabled: boolean;
};

/** 項目エラーを出す欄。 */
export type ScheduleField =
  | "name"
  | "definitionId"
  | "cronExpression"
  | "timeZone"
  | "overlapPolicy"
  | "runAsPrincipalId"
  | "definitionVersion"
  | "input";

/**
 * 送信前に止める項目エラーの種類。文言は画面側の辞書で出す。
 * - `required`: 必須が空
 * - `tooLong`: 名前が 128 文字を超える
 * - `format`: 名前が ASCII ラベルでない、cron が 5 フィールドでない、版番号が正の整数でない、input が JSON でない
 * - `cannotClear`: 既存の input を空にしようとした（PATCH で input を消す手段が無い）
 * - `versionRequired`: 版を固定した行の定義を変えるのに、版番号を入れていない
 */
export type ScheduleFieldErrorKind = "required" | "tooLong" | "format" | "cannotClear" | "versionRequired";

/** 欄ごとの項目エラー。 */
export type ScheduleFieldErrors = Partial<Record<ScheduleField, ScheduleFieldErrorKind>>;

/** 作成本文の組み立て結果。 */
export type ScheduleCreateBuildResult =
  | { ok: true; body: CreateScheduleBody }
  | { ok: false; errors: ScheduleFieldErrors };

/** 部分更新本文の組み立て結果。`unchanged` は送る項目が無いことを表し、空の PATCH は送らない。 */
export type ScheduleUpdateBuildResult =
  | { kind: "ready"; body: UpdateScheduleBody }
  | { kind: "invalid"; errors: ScheduleFieldErrors }
  | { kind: "unchanged" };

/** cron のフィールド数。API は 5 フィールドだけを受け付ける。 */
const CRON_FIELD_COUNT = 5;

/** 版番号の形式（1 以上の整数）。 */
const DEFINITION_VERSION_PATTERN = /^[1-9]\d*$/;

/** 空のフォーム。overlap の既定は `skip`、有効の既定は true。タイムゾーンは空のまま（UTC で埋めない）。 */
export const EMPTY_SCHEDULE_FORM: ScheduleFormValues = {
  name: "",
  definitionId: "",
  cronExpression: "",
  timeZone: "",
  overlapPolicy: "skip",
  runAsPrincipalId: "",
  definitionVersion: "",
  inputText: "",
  enabled: true
};

/**
 * 単票の値から編集フォームの初期値を作る。版は UUID しか返らないため、版番号の欄は空にする（空は現在の固定を維持）。
 * @param detail `GET /v1/schedules/{id}` の応答。
 * @returns 編集フォームの初期値。
 */
export function toScheduleFormValues(detail: ScheduleDetail): ScheduleFormValues {
  return {
    name: detail.name,
    definitionId: detail.definitionId,
    cronExpression: detail.cronExpression,
    timeZone: detail.timeZone,
    overlapPolicy: detail.overlapPolicy,
    runAsPrincipalId: detail.runAsPrincipalId,
    definitionVersion: "",
    inputText: detail.input === null || detail.input === undefined ? "" : JSON.stringify(detail.input, null, 2),
    enabled: detail.enabled
  };
}

/**
 * 名前を Service API の `AsciiLabelConstraints` と同じ規則で検査する。
 * @param name trim 済みの名前。
 * @returns 違反の種類。問題なければ undefined。
 */
function checkName(name: string): ScheduleFieldErrorKind | undefined {
  if (name.length === 0) return "required";
  if (name.length > ASCII_LABEL_DEFAULT_MAX_LENGTH) return "tooLong";
  if (!matchesPattern(name, ASCII_LABEL_PATTERN)) return "format";
  return undefined;
}

/**
 * cron が 5 フィールドかを見る。各フィールドの値の正しさは API の 422 に任せる。
 * @param cron trim 済みの cron。
 * @returns 違反の種類。問題なければ undefined。
 */
function checkCron(cron: string): ScheduleFieldErrorKind | undefined {
  if (cron.length === 0) return "required";
  return cron.split(/\s+/).length === CRON_FIELD_COUNT ? undefined : "format";
}

/**
 * 空でないことだけを見る。
 * @param value trim 済みの値。
 * @returns 空なら `required`。
 */
function checkRequired(value: string): ScheduleFieldErrorKind | undefined {
  return value.length === 0 ? "required" : undefined;
}

/** 版番号の解釈結果。`absent` は空欄。 */
type ParsedVersion = { kind: "absent" } | { kind: "value"; value: number } | { kind: "invalid" };

/**
 * 版番号の欄を解釈する。
 * @param raw 欄の文字列。
 * @returns 解釈結果。
 */
function parseVersion(raw: string): ParsedVersion {
  const trimmed = raw.trim();
  if (trimmed.length === 0) return { kind: "absent" };
  return DEFINITION_VERSION_PATTERN.test(trimmed) ? { kind: "value", value: Number(trimmed) } : { kind: "invalid" };
}

/** input の解釈結果。`absent` は空欄。 */
type ParsedInput = { kind: "absent" } | { kind: "value"; value: unknown } | { kind: "invalid" };

/**
 * input の JSON テキストを解釈する。JSON の `null` は未設定と同じに扱う（API も null を保存しない）。
 * @param raw 欄の文字列。
 * @returns 解釈結果。
 */
function parseInput(raw: string): ParsedInput {
  const trimmed = raw.trim();
  if (trimmed.length === 0) return { kind: "absent" };
  try {
    const value: unknown = JSON.parse(trimmed);
    return value === null ? { kind: "absent" } : { kind: "value", value };
  } catch {
    return { kind: "invalid" };
  }
}

/**
 * 項目エラーが 1 つでもあるか。
 * @param errors 欄ごとの項目エラー。
 * @returns エラーがあれば true。
 */
function hasErrors(errors: ScheduleFieldErrors): boolean {
  return Object.values(errors).some((kind) => kind !== undefined);
}

/**
 * 作成本文を作る。名前・cron・タイムゾーンは trim する。版番号と input は空なら付けない。
 * @param values フォームの入力。
 * @returns 送信できる本文、または欄ごとの項目エラー。
 */
export function buildCreateScheduleBody(values: ScheduleFormValues): ScheduleCreateBuildResult {
  const name = values.name.trim();
  const cronExpression = values.cronExpression.trim();
  const timeZone = values.timeZone.trim();
  const version = parseVersion(values.definitionVersion);
  const input = parseInput(values.inputText);
  const errors: ScheduleFieldErrors = {
    name: checkName(name),
    definitionId: checkRequired(values.definitionId),
    cronExpression: checkCron(cronExpression),
    timeZone: checkRequired(timeZone),
    runAsPrincipalId: checkRequired(values.runAsPrincipalId),
    definitionVersion: version.kind === "invalid" ? "format" : undefined,
    input: input.kind === "invalid" ? "format" : undefined
  };
  if (hasErrors(errors)) return { ok: false, errors };

  return {
    ok: true,
    body: {
      name,
      definitionId: values.definitionId,
      cronExpression,
      timeZone,
      overlapPolicy: values.overlapPolicy,
      runAsPrincipalId: values.runAsPrincipalId,
      enabled: values.enabled,
      ...(version.kind === "value" ? { definitionVersion: version.value } : {}),
      ...(input.kind === "value" ? { input: input.value } : {})
    }
  };
}

/**
 * input の変更を判定する。JSON として同じなら書式の違いは変更とみなさない。
 * @param current 現在の input。
 * @param input 欄の解釈結果（値あり）。
 * @returns 変更があれば true。
 */
function inputChanged(current: unknown, input: Extract<ParsedInput, { kind: "value" }>): boolean {
  const currentJson = current === null || current === undefined ? null : JSON.stringify(current);
  const nextJson = JSON.stringify(input.value);
  return currentJson !== nextJson;
}

/** 編集フォームの値を trim し、版番号と input を解釈したもの。 */
type NormalizedUpdate = {
  name: string;
  definitionId: string;
  cronExpression: string;
  timeZone: string;
  overlapPolicy: ScheduleOverlapPolicy;
  runAsPrincipalId: string;
  enabled: boolean;
  version: ParsedVersion;
  input: ParsedInput;
};

/**
 * 編集フォームの値をそろえる。
 * @param values 編集フォームの入力。
 * @returns trim と解釈を済ませた値。
 */
function normalizeUpdate(values: ScheduleFormValues): NormalizedUpdate {
  return {
    name: values.name.trim(),
    definitionId: values.definitionId,
    cronExpression: values.cronExpression.trim(),
    timeZone: values.timeZone.trim(),
    overlapPolicy: values.overlapPolicy,
    runAsPrincipalId: values.runAsPrincipalId,
    enabled: values.enabled,
    version: parseVersion(values.definitionVersion),
    input: parseInput(values.inputText)
  };
}

/** 現在値と比べる単純な項目。 */
const SCALAR_UPDATE_FIELDS = [
  "name",
  "definitionId",
  "cronExpression",
  "timeZone",
  "overlapPolicy",
  "runAsPrincipalId",
  "enabled"
] as const;

/** 単純な項目のうち、変わったもの。 */
type ChangedFields = ReadonlySet<(typeof SCALAR_UPDATE_FIELDS)[number]>;

/**
 * 編集の項目エラー。変えていない項目は検査しない（保存済みの値はそのまま送らない）。
 * @param current 現在値。
 * @param next そろえた入力。
 * @param changed 変わった項目。
 * @returns 欄ごとの項目エラー。
 */
function updateErrors(current: ScheduleDetail, next: NormalizedUpdate, changed: ChangedFields): ScheduleFieldErrors {
  const definitionChanged = changed.has("definitionId");
  return {
    name: changed.has("name") ? checkName(next.name) : undefined,
    definitionId: definitionChanged ? checkRequired(next.definitionId) : undefined,
    cronExpression: changed.has("cronExpression") ? checkCron(next.cronExpression) : undefined,
    timeZone: changed.has("timeZone") ? checkRequired(next.timeZone) : undefined,
    runAsPrincipalId: changed.has("runAsPrincipalId") ? checkRequired(next.runAsPrincipalId) : undefined,
    definitionVersion: versionError(next.version, definitionChanged && current.definitionVersionId !== null),
    input: inputError(next.input, current.input !== null && current.input !== undefined)
  };
}

/**
 * 変えた項目だけの本文を作る。
 * @param current 現在値。
 * @param next そろえた入力（項目エラーなし）。
 * @param changed 変わった項目。
 * @returns 部分更新本文。
 */
function updateBody(current: ScheduleDetail, next: NormalizedUpdate, changed: ChangedFields): UpdateScheduleBody {
  const scalars: UpdateScheduleBody = Object.fromEntries(
    SCALAR_UPDATE_FIELDS.filter((field) => changed.has(field)).map((field) => [field, next[field]])
  );
  const { version, input } = next;
  return {
    ...scalars,
    ...(version.kind === "value" ? { definitionVersion: version.value } : {}),
    ...(input.kind === "value" && inputChanged(current.input, input) ? { input: input.value } : {})
  };
}

/**
 * 現在値と比べて、変えた項目だけの部分更新本文を作る。
 * PATCH では input を消せないため、既存の input を空にすると `cannotClear` で止める。
 * 版を固定した行の定義を変えるときは、移し先の版番号を必須にする（古い版の固定を残さない）。
 * @param current `GET` で読んだ現在値。
 * @param values 編集フォームの入力。
 * @returns 送信できる本文、項目エラー、または変更なし。
 */
export function buildUpdateScheduleBody(current: ScheduleDetail, values: ScheduleFormValues): ScheduleUpdateBuildResult {
  const next = normalizeUpdate(values);
  const changed: ChangedFields = new Set(SCALAR_UPDATE_FIELDS.filter((field) => next[field] !== current[field]));
  const errors = updateErrors(current, next, changed);
  if (hasErrors(errors)) return { kind: "invalid", errors };

  const body = updateBody(current, next, changed);
  return Object.keys(body).length === 0 ? { kind: "unchanged" } : { kind: "ready", body };
}

/**
 * 版番号の欄の項目エラー。
 * @param version 解釈結果。
 * @param required 版番号が必須か（版を固定した行の定義を変えるとき）。
 * @returns 項目エラー。問題なければ undefined。
 */
function versionError(version: ParsedVersion, required: boolean): ScheduleFieldErrorKind | undefined {
  switch (version.kind) {
    case "invalid":
      return "format";
    case "absent":
      return required ? "versionRequired" : undefined;
    case "value":
      return undefined;
  }
}

/**
 * input の欄の項目エラー。
 * @param input 解釈結果。
 * @param currentHasInput 現在の行が input を持つか。
 * @returns 項目エラー。問題なければ undefined。
 */
function inputError(input: ParsedInput, currentHasInput: boolean): ScheduleFieldErrorKind | undefined {
  switch (input.kind) {
    case "invalid":
      return "format";
    case "absent":
      return currentHasInput ? "cannotClear" : undefined;
    case "value":
      return undefined;
  }
}
