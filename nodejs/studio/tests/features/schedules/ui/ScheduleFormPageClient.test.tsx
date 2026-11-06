import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ScheduleFormPageClient } from "@/features/schedules/ui/ScheduleFormPageClient";
import type { ScheduleDetail } from "@/features/schedules/types";
import { uiText } from "@/shared/i18n/uiText";
import * as api from "@/shared/api";
import { renderWithUiText } from "@/tests/testUtils";

const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push })
}));

vi.mock("@/shared/api", () => ({
  apiGet: vi.fn(),
  apiPatch: vi.fn(),
  apiPost: vi.fn()
}));

const text = uiText.schedules;

const detail: ScheduleDetail = {
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
  updatedAt: "2026-11-01T00:00:00Z",
  createdByPrincipalId: "pr-admin",
  input: null
};

/**
 * パスごとに GET の応答を返すモックを入れる。
 * @param single 単票の応答。
 */
function mockGets(single: () => Promise<unknown> = async () => detail): void {
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path === "/schedules/sch-1") return single();
    if (path === "/admin/service-accounts") {
      return [
        { principalId: "pr-1", name: "runner", isActive: true },
        { principalId: "pr-9", name: "retired", isActive: false }
      ];
    }
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

/**
 * 作成フォームの必須項目を埋める（タイムゾーンは引数で選ぶ）。
 * @param timeZone タイムゾーン。
 */
async function fillCreateForm(timeZone: string): Promise<void> {
  await screen.findByRole("option", { name: "runner" });
  fireEvent.change(screen.getByLabelText(text.form.nameLabel), { target: { value: "nightly" } });
  fireEvent.change(screen.getByLabelText(text.form.definitionLabel), { target: { value: "def-1" } });
  fireEvent.change(screen.getByLabelText(text.form.cronLabel), { target: { value: "0 9 * * *" } });
  fireEvent.change(screen.getByLabelText(text.form.timeZoneLabel), { target: { value: timeZone } });
  fireEvent.change(screen.getByLabelText(text.form.runAsLabel), { target: { value: "pr-1" } });
}

describe("ScheduleFormPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("run-as の候補は有効な ServiceAccount だけで、無効な行を出さない", async () => {
    // Arrange
    mockGets();

    // Act
    renderWithUiText(<ScheduleFormPageClient />);

    // Assert
    expect(await screen.findByRole("option", { name: "runner" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "retired" })).toBeNull();
  });

  it("タイムゾーンが空なら送信せず、UTC で埋めない", async () => {
    // Arrange
    mockGets();
    renderWithUiText(<ScheduleFormPageClient />);
    await fillCreateForm("");

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.form.submitCreate }));

    // Assert
    expect(await screen.findByText(text.form.errors.required)).toBeInTheDocument();
    expect(api.apiPost).not.toHaveBeenCalled();
  });

  it("作成に成功したら一覧へ戻る", async () => {
    // Arrange
    mockGets();
    vi.mocked(api.apiPost).mockResolvedValue(detail);
    renderWithUiText(<ScheduleFormPageClient />);
    await fillCreateForm("Asia/Tokyo");

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.form.submitCreate }));

    // Assert
    await waitFor(() => expect(push).toHaveBeenCalledWith("/schedules"));
    expect(api.apiPost).toHaveBeenCalledWith(
      "/schedules",
      expect.objectContaining({ timeZone: "Asia/Tokyo", overlapPolicy: "skip", enabled: true })
    );
  });

  it("run-as の 422 は run-as 欄に出し、メンバー管理へのリンクと無効化の案内を添える", async () => {
    // Arrange
    mockGets();
    vi.mocked(api.apiPost).mockRejectedValue(apiFailure(422, "VALIDATION_ERROR", { field: "runAsPrincipalId" }));
    renderWithUiText(<ScheduleFormPageClient />);
    await fillCreateForm("Asia/Tokyo");

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.form.submitCreate }));

    // Assert
    const message = await screen.findByText(new RegExp(text.serverErrors.runAsUnusable.slice(0, 10)));
    expect(message).toHaveTextContent(text.serverErrors.disableHint);
    expect(screen.getByRole("link", { name: text.serverErrors.membersLink })).toHaveAttribute("href", "/admin/projects");
    expect(push).not.toHaveBeenCalled();
  });

  it("無効のまま作る行の 422 には無効化の案内を付けない", async () => {
    // Arrange
    mockGets();
    vi.mocked(api.apiPost).mockRejectedValue(apiFailure(422, "VALIDATION_ERROR", { field: "runAsPrincipalId" }));
    renderWithUiText(<ScheduleFormPageClient />);
    await fillCreateForm("Asia/Tokyo");
    fireEvent.click(screen.getByLabelText(text.form.enabledLabel));

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.form.submitCreate }));

    // Assert
    const message = await screen.findByText(new RegExp(text.serverErrors.runAsUnusable.slice(0, 10)));
    expect(message).not.toHaveTextContent(text.serverErrors.disableHint);
  });

  it("保存の 403 RESOURCE_GRANT_DENIED は run-as の許可不足として出し、成功に見せない", async () => {
    // Arrange
    mockGets();
    vi.mocked(api.apiPost).mockRejectedValue(apiFailure(403, "RESOURCE_GRANT_DENIED"));
    renderWithUiText(<ScheduleFormPageClient />);
    await fillCreateForm("Asia/Tokyo");

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.form.submitCreate }));

    // Assert
    expect(await screen.findByText(text.serverErrors.runAsGrantDenied)).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("編集で有効フラグだけを変えると enabled だけを PATCH する", async () => {
    // Arrange
    mockGets();
    vi.mocked(api.apiPatch).mockResolvedValue({ ...detail, enabled: false });
    renderWithUiText(<ScheduleFormPageClient scheduleId="sch-1" />);
    fireEvent.click(await screen.findByLabelText(text.form.enabledLabel));

    // Act
    fireEvent.click(screen.getByRole("button", { name: text.form.submitSave }));

    // Assert
    await waitFor(() => expect(api.apiPatch).toHaveBeenCalledWith("/schedules/sch-1", { enabled: false }));
    expect(await screen.findByText(text.form.saved)).toBeInTheDocument();
  });

  it("単票の 403 は呼び出し元の許可の外として出し、フォームを出さない", async () => {
    // Arrange
    mockGets(async () => {
      throw apiFailure(403, "RESOURCE_GRANT_DENIED");
    });

    // Act
    renderWithUiText(<ScheduleFormPageClient scheduleId="sch-1" />);

    // Assert
    expect(await screen.findByText(text.form.forbidden)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: text.form.submitSave })).toBeNull();
  });

  it("単票の 404 は見つからない、または所属外として出す", async () => {
    // Arrange
    mockGets(async () => {
      throw apiFailure(404, "NOT_FOUND");
    });

    // Act
    renderWithUiText(<ScheduleFormPageClient scheduleId="sch-1" />);

    // Assert
    expect(await screen.findByText(text.form.notFound)).toBeInTheDocument();
  });
});
