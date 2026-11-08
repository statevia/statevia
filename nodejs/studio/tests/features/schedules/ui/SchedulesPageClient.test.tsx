import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SchedulesPageClient } from "@/features/schedules/ui/SchedulesPageClient";
import type { ScheduleListItem } from "@/features/schedules/types";
import { uiText } from "@/shared/i18n/uiText";
import * as api from "@/shared/api";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("@/shared/api", () => ({
  apiDelete: vi.fn(),
  apiGet: vi.fn(),
  apiPost: vi.fn()
}));

const text = uiText.schedules;

const enabledSchedule: ScheduleListItem = {
  scheduleId: "sch-1",
  name: "nightly",
  definitionId: "def-1",
  definitionVersionId: null,
  runAsPrincipalId: "pr-1",
  cronExpression: "0 9 * * *",
  timeZone: "Asia/Tokyo",
  overlapPolicy: "skip",
  enabled: true,
  nextFireAt: "2026-11-07T00:00:00Z",
  createdAt: "2026-11-01T00:00:00Z",
  updatedAt: "2026-11-01T00:00:00Z"
};

const disabledSchedule: ScheduleListItem = { ...enabledSchedule, scheduleId: "sch-2", name: "paused", enabled: false };

/**
 * パスごとに GET の応答を返すモックを入れる。
 * @param schedules 一覧の応答。
 */
function mockGets(schedules: () => Promise<unknown>): void {
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path === "/schedules") return schedules();
    if (path.startsWith("/definitions?")) {
      return {
        items: [{ displayId: "D1", resourceId: "def-1", name: "OrderFlow", createdAt: "", updatedAt: "" }],
        totalCount: 1,
        offset: 0,
        limit: 500,
        hasMore: false
      };
    }
    throw new Error(`unexpected path: ${path}`);
  });
}

/**
 * ApiError 形の失敗を作る。
 * @param status HTTP ステータス。
 * @param code `error.code`。
 * @param details `error.details`。
 * @returns 投げる値。
 */
function apiFailure(status: number, code: string, details?: Record<string, unknown>): Error {
  return Object.assign(new Error("failed"), { status, error: { code, message: "failed", details } });
}

describe("SchedulesPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("一覧は単票 GET をせず、定義名を引いて input を出さない", async () => {
    // Arrange
    mockGets(async () => [{ ...enabledSchedule, input: { secret: "do-not-show" } }]);

    // Act
    renderWithUiText(<SchedulesPageClient />);

    // Assert
    expect(await screen.findByText("nightly")).toBeInTheDocument();
    expect(screen.getByText(/OrderFlow/)).toBeInTheDocument();
    expect(screen.queryByText(/do-not-show/)).toBeNull();
    const paths = vi.mocked(api.apiGet).mock.calls.map(([path]) => path);
    expect(paths.filter((path) => path.startsWith("/schedules/"))).toEqual([]);
  });

  it("0 件のときは空状態と、プロジェクトに参加する案内を出す", async () => {
    // Arrange
    mockGets(async () => []);

    // Act
    renderWithUiText(<SchedulesPageClient />);

    // Assert
    expect(await screen.findByText(text.empty)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: text.joinProjectLink })).toHaveAttribute("href", "/admin/projects");
  });

  it("一覧の取得に失敗したら空一覧に見せずにエラーを出す", async () => {
    // Arrange
    mockGets(async () => {
      throw apiFailure(403, "FORBIDDEN");
    });

    // Act
    renderWithUiText(<SchedulesPageClient />);

    // Assert
    expect(await screen.findByText(text.loadFailed)).toBeInTheDocument();
    expect(screen.queryByText(text.empty)).toBeNull();
  });

  it("無効な定期実行には手動実行のボタンを出さない", async () => {
    // Arrange
    mockGets(async () => [disabledSchedule]);

    // Act
    renderWithUiText(<SchedulesPageClient />);

    // Assert
    expect(await screen.findByText("paused")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: text.actions.run })).toBeNull();
  });

  it("削除は確定するまで送らず、確定の連打でも 1 回だけ DELETE する", async () => {
    // Arrange
    mockGets(async () => [enabledSchedule]);
    vi.mocked(api.apiDelete).mockImplementation(() => new Promise<void>((resolve) => setTimeout(resolve, 10)));
    renderWithUiText(<SchedulesPageClient />);
    fireEvent.click(await screen.findByRole("button", { name: text.actions.delete }));

    // Act
    const confirmButton = screen.getByRole("button", { name: text.actions.confirmDelete });
    fireEvent.click(confirmButton);
    fireEvent.click(confirmButton);

    // Assert
    await waitFor(() => expect(api.apiDelete).toHaveBeenCalledTimes(1));
    expect(api.apiDelete).toHaveBeenCalledWith("/schedules/sch-1");
  });

  it("手動実行は確認文を出し、確定後に開始した実行の詳細へリンクする", async () => {
    // Arrange
    mockGets(async () => [enabledSchedule]);
    vi.mocked(api.apiPost).mockResolvedValue({ displayId: "ExA1" });
    renderWithUiText(<SchedulesPageClient />);
    fireEvent.click(await screen.findByRole("button", { name: text.actions.run }));
    expect(screen.getByText(text.actions.runConfirmNote)).toBeInTheDocument();
    expect(api.apiPost).not.toHaveBeenCalled();

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.actions.confirmRun }));

    // Assert
    expect(await screen.findByRole("link", { name: text.actions.runStarted("ExA1") })).toHaveAttribute(
      "href",
      "/executions/ExA1"
    );
    expect(api.apiPost).toHaveBeenCalledWith("/schedules/sch-1/run", {});
  });

  it("手動実行が run-as の許可不足で失敗したら理由を出し、実行へのリンクは出さない", async () => {
    // Arrange
    mockGets(async () => [enabledSchedule]);
    vi.mocked(api.apiPost).mockRejectedValue(apiFailure(403, "RESOURCE_GRANT_DENIED"));
    renderWithUiText(<SchedulesPageClient />);
    fireEvent.click(await screen.findByRole("button", { name: text.actions.run }));

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.actions.confirmRun }));

    // Assert
    expect(await screen.findByText(text.serverErrors.runAsGrantDenied)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /ExA1/ })).toBeNull();
  });

  it("手動実行が run-as の権限の不足で失敗したらリンクを出し、無効化の案内は出さない", async () => {
    // Arrange
    mockGets(async () => [enabledSchedule]);
    vi.mocked(api.apiPost).mockRejectedValue(
      apiFailure(422, "VALIDATION_ERROR", { field: "runAsPrincipalId", reason: "missingExecutionsWrite" })
    );
    renderWithUiText(<SchedulesPageClient />);
    fireEvent.click(await screen.findByRole("button", { name: text.actions.run }));

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.actions.confirmRun }));

    // Assert
    const message = await screen.findByText(new RegExp(text.serverErrors.runAsMissingPermission.slice(0, 10)));
    expect(message).not.toHaveTextContent(text.serverErrors.disableHint);
    expect(screen.getByRole("link", { name: text.serverErrors.serviceAccountsLink })).toHaveAttribute(
      "href",
      "/admin/service-accounts"
    );
  });
});
