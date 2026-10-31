import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderWithUiText } from "@/tests/testUtils";

vi.mock("@/features/projects/api", () => ({
  listGrantCandidates: vi.fn(),
  listProjectMembers: vi.fn(),
  removeProjectMember: vi.fn(),
  upsertProjectMember: vi.fn(),
}));

import {
  listGrantCandidates,
  listProjectMembers,
  removeProjectMember,
  upsertProjectMember,
} from "@/features/projects/api";
import type { ProjectGrantCandidate, ProjectMember } from "@/features/projects/types";
import { ProjectMembersPanel } from "@/features/projects/ui/ProjectMembersPanel";

const projectId = "project-1";

const alice: ProjectMember = {
  principalId: "user-alice",
  displayName: "alice",
  kind: "user",
  role: "reader",
  createdAt: "2026-01-01T00:00:00Z",
};

const aliceCandidate: ProjectGrantCandidate = { principalId: "user-alice", displayName: "alice", kind: "user" };
const bobCandidate: ProjectGrantCandidate = { principalId: "user-bob", displayName: "bob", kind: "user" };
const keyCandidate: ProjectGrantCandidate = { principalId: "key-module", displayName: "module", kind: "serviceAccount" };

/** API エラー以外の失敗を toToastError が表す文言。 */
const unknownErrorMessage = "UNKNOWN: Unknown error";

/**
 * 付与フォームの select を返す。並びは候補、ロールの順。
 * @returns 候補 select とロール select。
 */
function grantSelects(): { candidateSelect: HTMLElement; roleSelect: HTMLElement } {
  const [candidateSelect, roleSelect] = screen.getAllByRole("combobox");
  if (!candidateSelect || !roleSelect) {
    throw new Error("付与フォームの select が見つかりません");
  }
  return { candidateSelect, roleSelect };
}

describe("ProjectMembersPanel", () => {
  beforeEach(() => {
    vi.mocked(listProjectMembers).mockReset();
    vi.mocked(listGrantCandidates).mockReset();
    vi.mocked(upsertProjectMember).mockReset();
    vi.mocked(removeProjectMember).mockReset();
    vi.mocked(listProjectMembers).mockResolvedValue([alice]);
    vi.mocked(listGrantCandidates).mockResolvedValue([aliceCandidate, bobCandidate, keyCandidate]);
  });

  it("メンバーを表示名と種別で出し、候補からは既存メンバーを除く", async () => {
    // Arrange & Act
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);

    // Assert
    expect(await screen.findByText("alice（ユーザー）")).toBeInTheDocument();
    const { candidateSelect } = grantSelects();
    const optionLabels = Array.from(candidateSelect.querySelectorAll("option")).map((option) => option.textContent);
    expect(optionLabels).toEqual(["bob（ユーザー）", "module（ServiceAccount）"]);
    expect(candidateSelect).toHaveValue("user-bob");
    expect(listProjectMembers).toHaveBeenCalledWith(projectId);
    expect(listGrantCandidates).toHaveBeenCalledWith(projectId);
    expect(screen.queryByText("user-alice")).not.toBeInTheDocument();
  });

  it("メンバーも候補も無ければ空表示にし、付与ボタンを無効にする", async () => {
    // Arrange
    vi.mocked(listProjectMembers).mockResolvedValue([]);
    vi.mocked(listGrantCandidates).mockResolvedValue([]);

    // Act
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);

    // Assert
    expect(await screen.findByText("メンバーはいません。")).toBeInTheDocument();
    expect(screen.getByText("追加できるユーザーも ServiceAccount もありません。")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "付与" })).toBeDisabled();
  });

  it("初回取得に失敗するとエラートーストを出す", async () => {
    // Arrange
    vi.mocked(listProjectMembers).mockRejectedValue(new Error("network"));

    // Act
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);

    // Assert
    expect(await screen.findByText(unknownErrorMessage)).toBeInTheDocument();
  });

  it("選んだ候補とロールで付与し、再取得して成功トーストを出す", async () => {
    // Arrange
    vi.mocked(upsertProjectMember).mockResolvedValue(undefined);
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    await screen.findByText("alice（ユーザー）");
    const { candidateSelect, roleSelect } = grantSelects();
    fireEvent.change(candidateSelect, { target: { value: "key-module" } });
    fireEvent.change(roleSelect, { target: { value: "executor" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "付与" }));

    // Assert
    expect(await screen.findByText("メンバーを更新しました。")).toBeInTheDocument();
    expect(upsertProjectMember).toHaveBeenCalledWith(projectId, "key-module", "executor");
    expect(listProjectMembers).toHaveBeenCalledTimes(2);
  });

  it("ロール欄に未知の値が来ても付与ロールを変えない", async () => {
    // Arrange
    vi.mocked(upsertProjectMember).mockResolvedValue(undefined);
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    await screen.findByText("alice（ユーザー）");
    const { roleSelect } = grantSelects();
    fireEvent.change(roleSelect, { target: { value: "owner" } });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "付与" }));

    // Assert
    await screen.findByText("メンバーを更新しました。");
    expect(upsertProjectMember).toHaveBeenCalledWith(projectId, "user-bob", "reader");
  });

  it("付与に失敗するとエラートーストを出し、ボタンを戻す", async () => {
    // Arrange
    vi.mocked(upsertProjectMember).mockRejectedValue(new Error("conflict"));
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    await screen.findByText("alice（ユーザー）");

    // Act
    fireEvent.click(screen.getByRole("button", { name: "付与" }));

    // Assert
    expect(await screen.findByText(unknownErrorMessage)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "付与" })).toBeEnabled();
  });

  it("メンバーのロールを変えると更新して成功トーストを出す", async () => {
    // Arrange
    vi.mocked(upsertProjectMember).mockResolvedValue(undefined);
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    const memberRoleSelect = await screen.findByRole("combobox", { name: "alice（ユーザー） ロール" });

    // Act
    fireEvent.change(memberRoleSelect, { target: { value: "publisher" } });

    // Assert
    expect(await screen.findByText("メンバーを更新しました。")).toBeInTheDocument();
    expect(upsertProjectMember).toHaveBeenCalledWith(projectId, "user-alice", "publisher");
  });

  it("メンバーのロールを同じ値にしても更新しない", async () => {
    // Arrange
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    const memberRoleSelect = await screen.findByRole("combobox", { name: "alice（ユーザー） ロール" });

    // Act
    fireEvent.change(memberRoleSelect, { target: { value: "reader" } });

    // Assert
    expect(upsertProjectMember).not.toHaveBeenCalled();
  });

  it("ロール変更に失敗するとエラートーストを出す", async () => {
    // Arrange
    vi.mocked(upsertProjectMember).mockRejectedValue(new Error("forbidden"));
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    const memberRoleSelect = await screen.findByRole("combobox", { name: "alice（ユーザー） ロール" });

    // Act
    fireEvent.change(memberRoleSelect, { target: { value: "admin" } });

    // Assert
    expect(await screen.findByText(unknownErrorMessage)).toBeInTheDocument();
    await waitFor(() => expect(memberRoleSelect).toBeEnabled());
  });

  it("剥奪すると再取得して成功トーストを出す", async () => {
    // Arrange
    vi.mocked(removeProjectMember).mockResolvedValue(undefined);
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    await screen.findByText("alice（ユーザー）");
    vi.mocked(listProjectMembers).mockResolvedValue([]);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "剥奪" }));

    // Assert
    expect(await screen.findByText("メンバーを剥奪しました。")).toBeInTheDocument();
    expect(removeProjectMember).toHaveBeenCalledWith(projectId, "user-alice");
    expect(screen.getByText("メンバーはいません。")).toBeInTheDocument();
  });

  it("剥奪に失敗するとエラートーストを出し、ボタンを戻す", async () => {
    // Arrange
    vi.mocked(removeProjectMember).mockRejectedValue(new Error("network"));
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    await screen.findByText("alice（ユーザー）");

    // Act
    fireEvent.click(screen.getByRole("button", { name: "剥奪" }));

    // Assert
    expect(await screen.findByText(unknownErrorMessage)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "剥奪" })).toBeEnabled();
  });

  it("トーストを閉じると消える", async () => {
    // Arrange
    vi.mocked(listProjectMembers).mockRejectedValue(new Error("network"));
    renderWithUiText(<ProjectMembersPanel projectId={projectId} />);
    await screen.findByText(unknownErrorMessage);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "通知を閉じる" }));

    // Assert
    expect(screen.queryByText(unknownErrorMessage)).not.toBeInTheDocument();
  });

  it("取得完了前にアンマウントしてもトーストを出さない", async () => {
    // Arrange
    let rejectMembers: (reason: unknown) => void = () => undefined;
    vi.mocked(listProjectMembers).mockReturnValue(new Promise<ProjectMember[]>((_resolve, reject) => {
      rejectMembers = reject;
    }));
    const { unmount } = renderWithUiText(<ProjectMembersPanel projectId={projectId} />);

    // Act
    unmount();
    rejectMembers(new Error("late"));

    // Assert
    await waitFor(() => expect(screen.queryByText(unknownErrorMessage)).not.toBeInTheDocument());
  });
});
