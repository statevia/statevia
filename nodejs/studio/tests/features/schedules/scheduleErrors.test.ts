import { describe, expect, it } from "vitest";
import { classifyScheduleReadError, classifyScheduleWriteError } from "@/features/schedules/scheduleErrors";

/**
 * `apiPost` などが投げる形の失敗を作る。
 * @param status HTTP ステータス。
 * @param code `error.code`。
 * @param details `error.details`。
 * @returns ApiError 形の値。
 */
function apiError(status: number, code: string, details?: unknown): unknown {
  return { status, error: { code, message: "server message", ...(details === undefined ? {} : { details }) } };
}

describe("classifyScheduleWriteError", () => {
  it("run-as の 422 は保存後の行が有効なら無効化の案内を付ける", () => {
    // Arrange
    const error = apiError(422, "VALIDATION_ERROR", { field: "runAsPrincipalId" });

    // Act
    const result = classifyScheduleWriteError(error, true);

    // Assert
    expect(result).toEqual({ kind: "runAsUnusable", disableHint: true });
  });

  it("保存後の行が無効なら定義の 422 に無効化の案内を付けない", () => {
    // Arrange
    const error = apiError(422, "VALIDATION_ERROR", { field: "definitionId" });

    // Act
    const result = classifyScheduleWriteError(error, false);

    // Assert
    expect(result).toEqual({ kind: "definitionUnavailable", disableHint: false });
  });

  it("形式検証の配列の details から欄を読む", () => {
    // Arrange
    const error = apiError(422, "VALIDATION_ERROR", [{ field: "TimeZone", message: "bad" }]);

    // Act
    const result = classifyScheduleWriteError(error, true);

    // Assert
    expect(result).toEqual({ kind: "fieldMessage", field: "timeZone", message: "server message" });
  });

  it("保存の 403 RESOURCE_GRANT_DENIED は run-as の許可不足にする", () => {
    // Arrange
    const error = apiError(403, "RESOURCE_GRANT_DENIED");

    // Act
    const result = classifyScheduleWriteError(error, true);

    // Assert
    expect(result).toEqual({ kind: "runAsGrantDenied" });
  });

  it("403 PROJECT_ACCESS_DENIED は呼び出し元の所属不足にする", () => {
    // Arrange
    const error = apiError(403, "PROJECT_ACCESS_DENIED");

    // Act
    const result = classifyScheduleWriteError(error, true);

    // Assert
    expect(result).toEqual({ kind: "projectAccessDenied" });
  });

  it("欄の分からない 422 と ApiError でない値は other にする", () => {
    // Arrange
    const withoutField = apiError(422, "VALIDATION_ERROR");
    const networkError = new TypeError("Failed to fetch");

    // Act
    const results = [classifyScheduleWriteError(withoutField, true), classifyScheduleWriteError(networkError, true)];

    // Assert
    expect(results).toEqual([{ kind: "other" }, { kind: "other" }]);
  });
});

describe("classifyScheduleReadError", () => {
  it("単票の 404 は notFound、403 は forbidden にする", () => {
    // Arrange
    const notFound = apiError(404, "NOT_FOUND");
    const forbidden = apiError(403, "RESOURCE_GRANT_DENIED");

    // Act
    const results = [classifyScheduleReadError(notFound), classifyScheduleReadError(forbidden)];

    // Assert
    expect(results).toEqual(["notFound", "forbidden"]);
  });
});
