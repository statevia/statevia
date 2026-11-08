import { describe, expect, it } from "vitest";
import {
  buildCreateScheduleBody,
  buildUpdateScheduleBody,
  EMPTY_SCHEDULE_FORM,
  toScheduleFormValues,
  type ScheduleFormValues
} from "@/features/schedules/schedules";
import type { ScheduleDetail } from "@/features/schedules/types";

const filledForm: ScheduleFormValues = {
  ...EMPTY_SCHEDULE_FORM,
  name: " nightly ",
  definitionId: "def-1",
  cronExpression: " 0 9 * * 1-5 ",
  timeZone: " Asia/Tokyo ",
  runAsPrincipalId: "pr-1"
};

const detail: ScheduleDetail = {
  scheduleId: "sch-1",
  name: "nightly",
  definitionId: "def-1",
  definitionVersionId: null,
  runAsPrincipalId: "pr-1",
  cronExpression: "0 9 * * 1-5",
  timeZone: "Asia/Tokyo",
  overlapPolicy: "skip",
  enabled: true,
  nextFireAt: "2026-11-07T00:00:00Z",
  createdAt: "2026-11-01T00:00:00Z",
  updatedAt: "2026-11-01T00:00:00Z",
  createdByPrincipalId: "pr-admin",
  input: { orderId: 1 }
};

describe("buildCreateScheduleBody", () => {
  it("必須項目を trim して送り、空の版番号と input は付けない", () => {
    // Arrange
    const values = filledForm;

    // Act
    const result = buildCreateScheduleBody(values);

    // Assert
    expect(result).toEqual({
      ok: true,
      body: {
        name: "nightly",
        definitionId: "def-1",
        cronExpression: "0 9 * * 1-5",
        timeZone: "Asia/Tokyo",
        overlapPolicy: "skip",
        runAsPrincipalId: "pr-1",
        enabled: true
      }
    });
  });

  it("タイムゾーンが空なら送らず、UTC で埋めない", () => {
    // Arrange
    const values = { ...filledForm, timeZone: "  " };

    // Act
    const result = buildCreateScheduleBody(values);

    // Assert
    expect(result).toEqual({ ok: false, errors: expect.objectContaining({ timeZone: "required" }) });
  });

  it("cron が 5 フィールドでなければ形式エラーにする", () => {
    // Arrange
    const values = { ...filledForm, cronExpression: "0 9 * *" };

    // Act
    const result = buildCreateScheduleBody(values);

    // Assert
    expect(result).toEqual({ ok: false, errors: expect.objectContaining({ cronExpression: "format" }) });
  });

  it("版番号と input を入れたときだけ付ける", () => {
    // Arrange
    const values = { ...filledForm, definitionVersion: "3", inputText: '{"orderId": 1}' };

    // Act
    const result = buildCreateScheduleBody(values);

    // Assert
    expect(result).toEqual({
      ok: true,
      body: expect.objectContaining({ definitionVersion: 3, input: { orderId: 1 } })
    });
  });

  it("壊れた JSON の input と正でない版番号は送らない", () => {
    // Arrange
    const values = { ...filledForm, definitionVersion: "0", inputText: "{" };

    // Act
    const result = buildCreateScheduleBody(values);

    // Assert
    expect(result).toEqual({ ok: false, errors: expect.objectContaining({ definitionVersion: "format", input: "format" }) });
  });
});

describe("buildUpdateScheduleBody", () => {
  it("有効フラグだけを変えると enabled だけを送る", () => {
    // Arrange
    const values = { ...toScheduleFormValues(detail), enabled: false };

    // Act
    const result = buildUpdateScheduleBody(detail, values);

    // Assert
    expect(result).toEqual({ kind: "ready", body: { enabled: false } });
  });

  it("何も変えなければ送らない（input の整形だけの差も変更にしない）", () => {
    // Arrange
    const values = { ...toScheduleFormValues(detail), inputText: '{ "orderId" : 1 }' };

    // Act
    const result = buildUpdateScheduleBody(detail, values);

    // Assert
    expect(result).toEqual({ kind: "unchanged" });
  });

  it("保存済みの input を空にすると止める（PATCH で消せない）", () => {
    // Arrange
    const values = { ...toScheduleFormValues(detail), inputText: "" };

    // Act
    const result = buildUpdateScheduleBody(detail, values);

    // Assert
    expect(result).toEqual({ kind: "invalid", errors: expect.objectContaining({ input: "cannotClear" }) });
  });

  it("版を固定した定期実行の定義を変えるときは版番号を必須にする", () => {
    // Arrange
    const pinned = { ...detail, definitionVersionId: "ver-1" };
    const values = { ...toScheduleFormValues(pinned), definitionId: "def-2" };

    // Act
    const result = buildUpdateScheduleBody(pinned, values);

    // Assert
    expect(result).toEqual({ kind: "invalid", errors: expect.objectContaining({ definitionVersion: "versionRequired" }) });
  });

  it("版を固定していない定期実行は定義だけを変えて送れる", () => {
    // Arrange
    const values = { ...toScheduleFormValues(detail), definitionId: "def-2" };

    // Act
    const result = buildUpdateScheduleBody(detail, values);

    // Assert
    expect(result).toEqual({ kind: "ready", body: { definitionId: "def-2" } });
  });

  it("タイムゾーンを空にすると止める", () => {
    // Arrange
    const values = { ...toScheduleFormValues(detail), timeZone: "" };

    // Act
    const result = buildUpdateScheduleBody(detail, values);

    // Assert
    expect(result).toEqual({ kind: "invalid", errors: expect.objectContaining({ timeZone: "required" }) });
  });
});
