import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PrincipalResourceGrants } from "@/features/admin/resourceGrants";
import { ResourceGrantsForm } from "@/features/admin/ui/ResourceGrantsForm";
import type { ProjectSummary } from "@/features/projects/types";
import * as api from "@/shared/api";
import { renderWithUiText } from "../testUtils";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPut: vi.fn()
  };
});

const subject = { kind: "serviceAccount", serviceAccountId: "sa-1" } as const;
const grantsPath = "/admin/service-accounts/sa-1/resource-grants";
const unknownDefinitionId = "99999999-9999-4999-8999-999999999999";

/**
 * Project 候補を作る。
 * @param projectId Project ID。
 * @param displayName 表示名。
 * @param slug プロジェクトキー。
 * @returns Project 一覧の 1 件。
 */
function project(projectId: string, displayName: string, slug: string): ProjectSummary {
  return { projectId, slug, displayName, description: null, createdAt: "2026-10-01T00:00:00Z", role: null };
}

/**
 * 定義一覧の 1 件を作る。
 * @param resourceId 定義 ID。
 * @param name 定義名。
 * @param projectId 所属 Project。
 * @returns 定義一覧の要素。
 */
function definition(resourceId: string, name: string, projectId: string) {
  return {
    displayId: name,
    resourceId,
    name,
    projectId,
    createdAt: "2026-10-01T00:00:00Z",
    updatedAt: "2026-10-01T00:00:00Z"
  };
}

/**
 * パスごとに GET の応答を返すモックを入れる。
 * @param grants 許可の応答。
 * @param candidatesFail true なら Project 一覧の取得を失敗させる。
 */
function mockGets(grants: PrincipalResourceGrants, candidatesFail = false): void {
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path === grantsPath) return grants;
    if (path === "/admin/projects") {
      if (candidatesFail) throw Object.assign(new Error("unavailable"), { status: 503 });
      return [project("p-1", "Billing", "billing"), project("p-2", "Ops", "ops")];
    }
    if (path.startsWith("/definitions?")) {
      return {
        items: [definition("d-1", "invoice", "p-1"), definition("d-2", "deploy", "p-2")],
        totalCount: 2,
        offset: 0,
        limit: 500,
        hasMore: false
      };
    }
    throw new Error(`unexpected path: ${path}`);
  });
}

/**
 * フォームを描画し、保存ボタンが出るまで待つ。
 */
async function renderReady(): Promise<void> {
  renderWithUiText(<ResourceGrantsForm subject={subject} />);
  await screen.findByRole("button", { name: "保存" });
}

describe("ResourceGrantsForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("付与済み project は表示名とプロジェクトキーで出し、追加は候補の選択欄だけで GUID の入力欄は無い", async () => {
    // Arrange
    mockGets({ projectIds: ["p-1"], definitionIds: [] });

    // Act
    await renderReady();

    // Assert
    const grantedRow = within(screen.getByRole("list")).getByRole("listitem");
    expect(grantedRow).toHaveTextContent("Billing");
    expect(grantedRow).toHaveTextContent("billing");
    expect(screen.getByRole("combobox", { name: "Project を追加" })).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
  });

  it("project を外して保存しても、候補に無い付与済みの定義 ID は本文に残る", async () => {
    // Arrange
    mockGets({ projectIds: ["p-1", "p-2"], definitionIds: ["d-1", unknownDefinitionId] });
    vi.mocked(api.apiPut).mockImplementation(async (_path: string, body: unknown) => body);
    await renderReady();
    expect(screen.getByText(unknownDefinitionId)).toBeInTheDocument();

    // Act
    fireEvent.click(screen.getByRole("button", { name: "Ops を外す" }));
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    await waitFor(() =>
      expect(api.apiPut).toHaveBeenCalledWith(grantsPath, {
        projectIds: ["p-1"],
        definitionIds: ["d-1", unknownDefinitionId]
      })
    );
  });

  it("422 は失敗として出し、成功の通知を出さない", async () => {
    // Arrange
    mockGets({ projectIds: [], definitionIds: [] });
    vi.mocked(api.apiPut).mockRejectedValue({
      status: 422,
      error: { code: "VALIDATION_ERROR", message: "projectIds is invalid." }
    });
    await renderReady();
    fireEvent.change(screen.getByRole("combobox", { name: "Project を追加" }), { target: { value: "p-1" } });
    fireEvent.click(screen.getByRole("button", { name: "追加" }));

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    // Assert
    expect(await screen.findByText(/VALIDATION_ERROR - projectIds is invalid\./)).toBeInTheDocument();
    expect(screen.queryByText("実行リソース許可を保存しました。")).not.toBeInTheDocument();
  });

  it("付いていた種別を空にするときは確認を出し、キャンセルなら送らない", async () => {
    // Arrange
    mockGets({ projectIds: ["p-1"], definitionIds: [] });
    await renderReady();
    fireEvent.click(screen.getByRole("button", { name: "Billing を外す" }));

    // Act
    fireEvent.click(screen.getByRole("button", { name: "保存" }));
    fireEvent.click(await screen.findByRole("button", { name: "キャンセル" }));

    // Assert
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.apiPut).not.toHaveBeenCalled();
  });

  it("選んだ project の外にある定義には注意を出す", async () => {
    // Arrange
    mockGets({ projectIds: ["p-1"], definitionIds: [] });
    await renderReady();

    // Act
    fireEvent.click(screen.getByRole("checkbox", { name: /deploy/ }));

    // Assert
    expect(screen.getByRole("status")).toHaveTextContent("deploy");
  });

  it("候補を読めないときは理由を出し、保存できない", async () => {
    // Arrange
    mockGets({ projectIds: ["p-1"], definitionIds: [] }, true);

    // Act
    await renderReady();

    // Assert
    expect(screen.getByRole("alert")).toHaveTextContent("候補を読み込めませんでした");
    expect(screen.getByRole("button", { name: "保存" })).toBeDisabled();
  });
});
