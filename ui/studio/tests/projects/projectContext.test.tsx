import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const usePathname = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => usePathname(),
}));

vi.mock("@/features/projects/api", () => ({
  listMembershipProjects: vi.fn(),
}));

import { listMembershipProjects } from "@/features/projects/api";
import { ProjectProvider, useProject } from "@/features/projects/projectContext";
import type { ProjectSummary } from "@/features/projects/types";

const STORAGE_KEY = "statevia.selectedProjectId";

/**
 * 所属一覧の 1 件。
 * @param projectId Project UUID。
 * @param role 実効ロール。
 * @returns 画面用の Project。
 */
function project(projectId: string, role: ProjectSummary["role"]): ProjectSummary {
  return {
    projectId,
    slug: projectId,
    displayName: projectId.toUpperCase(),
    description: null,
    createdAt: "2026-01-01T00:00:00Z",
    role,
  };
}

const alpha = project("alpha", "admin");
const beta = project("beta", "reader");

/**
 * API から一覧を読むプロバイダでラップする。
 * @param props 子要素。
 * @returns ラッパー。
 */
function LiveWrapper({ children }: Readonly<{ children: ReactNode }>) {
  return <ProjectProvider>{children}</ProjectProvider>;
}

describe("ProjectProvider（API 取得）", () => {
  beforeEach(() => {
    usePathname.mockReturnValue("/definitions");
    vi.mocked(listMembershipProjects).mockReset();
    globalThis.sessionStorage.clear();
  });

  afterEach(() => {
    globalThis.sessionStorage.clear();
  });

  it("保存値が無ければ先頭の Project を選ぶ", async () => {
    // Arrange
    vi.mocked(listMembershipProjects).mockResolvedValue([alpha, beta]);

    // Act
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });

    // Assert
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(result.current.projects).toEqual([alpha, beta]);
    expect(result.current.selectedProject).toEqual(alpha);
  });

  it("sessionStorage の Project が所属一覧にあればそれを選ぶ", async () => {
    // Arrange
    globalThis.sessionStorage.setItem(STORAGE_KEY, "beta");
    vi.mocked(listMembershipProjects).mockResolvedValue([alpha, beta]);

    // Act
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });

    // Assert
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(result.current.selectedProject).toEqual(beta);
  });

  it("sessionStorage の Project が所属一覧に無ければ先頭へ戻す", async () => {
    // Arrange
    globalThis.sessionStorage.setItem(STORAGE_KEY, "removed");
    vi.mocked(listMembershipProjects).mockResolvedValue([alpha, beta]);

    // Act
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });

    // Assert
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(result.current.selectedProject).toEqual(alpha);
  });

  it("所属が無ければ選択は null になる", async () => {
    // Arrange
    vi.mocked(listMembershipProjects).mockResolvedValue([]);

    // Act
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });

    // Assert
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(result.current.projects).toEqual([]);
    expect(result.current.selectedProject).toBeNull();
  });

  it("取得に失敗すると空一覧と未選択にし、取得済みにする", async () => {
    // Arrange
    vi.mocked(listMembershipProjects).mockRejectedValue(new Error("network"));

    // Act
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });

    // Assert
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(result.current.projects).toEqual([]);
    expect(result.current.selectedProject).toBeNull();
  });

  it("ログイン画面では API を呼ばずに取得済みにする", async () => {
    // Arrange
    usePathname.mockReturnValue("/login");

    // Act
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });

    // Assert
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(listMembershipProjects).not.toHaveBeenCalled();
    expect(result.current.projects).toEqual([]);
  });

  it("選択を切り替えると sessionStorage に残す", async () => {
    // Arrange
    vi.mocked(listMembershipProjects).mockResolvedValue([alpha, beta]);
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });
    await waitFor(() => expect(result.current.loaded).toBe(true));

    // Act
    act(() => result.current.selectProject("beta"));

    // Assert
    expect(result.current.selectedProject).toEqual(beta);
    expect(globalThis.sessionStorage.getItem(STORAGE_KEY)).toBe("beta");
  });

  it("再取得では現在の選択が一覧に残っていれば維持する", async () => {
    // Arrange
    vi.mocked(listMembershipProjects).mockResolvedValue([alpha, beta]);
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });
    await waitFor(() => expect(result.current.loaded).toBe(true));
    act(() => result.current.selectProject("beta"));
    const gamma = project("gamma", "executor");
    vi.mocked(listMembershipProjects).mockResolvedValue([gamma, beta]);

    // Act
    await act(() => result.current.reload());

    // Assert
    expect(result.current.projects).toEqual([gamma, beta]);
    expect(result.current.selectedProject).toEqual(beta);
  });

  it("roleOf は所属一覧からロールを引き、未指定と未所属は null を返す", async () => {
    // Arrange
    vi.mocked(listMembershipProjects).mockResolvedValue([alpha, beta]);
    const { result } = renderHook(() => useProject(), { wrapper: LiveWrapper });
    await waitFor(() => expect(result.current.loaded).toBe(true));

    // Act
    const roles = [
      result.current.roleOf("beta"),
      result.current.roleOf("unknown"),
      result.current.roleOf(null),
      result.current.roleOf(undefined),
    ];

    // Assert
    expect(roles).toEqual(["reader", null, null, null]);
  });
});

describe("ProjectProvider（固定一覧）", () => {
  beforeEach(() => {
    vi.mocked(listMembershipProjects).mockReset();
  });

  it("API を呼ばずに先頭を選び、切り替えと再取得ができる", async () => {
    // Arrange
    const initialProjects = [alpha, beta];
    const wrapper = ({ children }: Readonly<{ children: ReactNode }>) => (
      <ProjectProvider initialProjects={initialProjects}>{children}</ProjectProvider>
    );
    const { result } = renderHook(() => useProject(), { wrapper });

    // Act
    act(() => result.current.selectProject("beta"));
    await act(() => result.current.reload());

    // Assert
    expect(listMembershipProjects).not.toHaveBeenCalled();
    expect(result.current.loaded).toBe(true);
    expect(result.current.projects).toEqual(initialProjects);
    expect(result.current.selectedProject).toEqual(beta);
  });

  it("空の固定一覧では選択が null になる", () => {
    // Arrange
    const wrapper = ({ children }: Readonly<{ children: ReactNode }>) => (
      <ProjectProvider initialProjects={[]}>{children}</ProjectProvider>
    );

    // Act
    const { result } = renderHook(() => useProject(), { wrapper });

    // Assert
    expect(result.current.selectedProject).toBeNull();
  });
});

describe("useProject", () => {
  it("ProjectProvider の外で呼ぶと例外を投げる", () => {
    // Arrange
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => undefined);
    // React 18 の開発ビルドは描画中の例外を window の error イベントにも流すため、jsdom の出力を抑える。
    const suppressWindowError = (event: ErrorEvent) => event.preventDefault();
    globalThis.addEventListener("error", suppressWindowError);

    // Act
    const renderOutsideProvider = () => renderHook(() => useProject());

    // Assert
    expect(renderOutsideProvider).toThrow("ProjectProvider が必要です");
    globalThis.removeEventListener("error", suppressWindowError);
    consoleError.mockRestore();
  });
});
