"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import { listMembershipProjects } from "./api";
import type { ProjectRole, ProjectSummary } from "./types";

const STORAGE_KEY = "statevia.selectedProjectId";

/** ヘッダーと一覧が共有する所属 Project。 */
export type ProjectContextValue = {
  /** 所属一覧。 */
  projects: ProjectSummary[];
  /** 選択中の Project。 */
  selectedProject: ProjectSummary | null;
  /** 所属一覧の取得が終わったか。 */
  loaded: boolean;
  /**
   * 選択を切り替える。
   * @param projectId Project UUID。
   */
  selectProject: (projectId: string) => void;
  /**
   * 所属一覧からロールを引く。
   * @param projectId 定義などが持つ Project。
   * @returns 未所属または未知なら null。
   */
  roleOf: (projectId: string | null | undefined) => ProjectRole | null;
  /**
   * 所属一覧を取り直す。
   * @returns 取得できたら true。失敗時は一覧を空にして false。
   */
  reload: () => Promise<boolean>;
};

const ProjectContext = createContext<ProjectContextValue | null>(null);

type ProjectProviderProps = {
  children: ReactNode;
  /** 指定時は API を呼ばず、この一覧を選択状態の初期値にする。 */
  initialProjects?: ProjectSummary[];
};

/**
 * sessionStorage に残した Project を読む。
 * @returns 保存値。ブラウザ以外は null。
 */
function readStoredProjectId(): string | null {
  if (globalThis.sessionStorage === undefined) {
    return null;
  }
  return globalThis.sessionStorage.getItem(STORAGE_KEY);
}

/**
 * 選択中 Project を sessionStorage へ書く。
 * @param projectId Project UUID。
 */
function storeProjectId(projectId: string): void {
  if (globalThis.sessionStorage === undefined) {
    return;
  }
  globalThis.sessionStorage.setItem(STORAGE_KEY, projectId);
}

/**
 * 一覧と選択からコンテキスト値を作る。
 * @param projects 所属一覧。
 * @param selectedProjectId 選択中 ID。
 * @param loaded 取得済みか。
 * @param selectProject 選択操作。
 * @param reload 再取得。
 * @returns コンテキスト値。
 */
function useProjectValue(
  projects: ProjectSummary[],
  selectedProjectId: string | null,
  loaded: boolean,
  selectProject: (projectId: string) => void,
  reload: () => Promise<boolean>,
): ProjectContextValue {
  const selectedProject = useMemo(
    () => projects.find((project) => project.projectId === selectedProjectId) ?? null,
    [projects, selectedProjectId],
  );
  const roleOf = useCallback((projectId: string | null | undefined) => {
    if (!projectId) {
      return null;
    }
    return projects.find((project) => project.projectId === projectId)?.role ?? null;
  }, [projects]);

  return useMemo(
    () => ({
      projects,
      selectedProject,
      loaded,
      selectProject,
      roleOf,
      reload,
    }),
    [loaded, projects, reload, roleOf, selectProject, selectedProject],
  );
}

/**
 * API から所属一覧を読むプロバイダ。ログイン画面では取得しない。
 * @param props 子要素。
 * @returns プロバイダ。
 */
function LiveProjectProvider({ children }: Readonly<{ children: ReactNode }>) {
  const pathname = usePathname();
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const selectProject = useCallback((projectId: string) => {
    setSelectedProjectId(projectId);
    storeProjectId(projectId);
  }, []);

  const reload = useCallback(async () => {
    try {
      const rows = await listMembershipProjects();
      setProjects(rows);
      setSelectedProjectId((current) => {
        const preferred = current ?? readStoredProjectId();
        if (preferred && rows.some((project) => project.projectId === preferred)) {
          return preferred;
        }
        return rows[0]?.projectId ?? null;
      });
      return true;
    } catch {
      setProjects([]);
      setSelectedProjectId(null);
      return false;
    } finally {
      setLoaded(true);
    }
  }, []);

  useEffect(() => {
    if (pathname === "/login") {
      setLoaded(true);
      return;
    }
    void reload();
  }, [pathname, reload]);

  const value = useProjectValue(projects, selectedProjectId, loaded, selectProject, reload);
  return <ProjectContext.Provider value={value}>{children}</ProjectContext.Provider>;
}

/**
 * テストや固定データ用。API は呼ばない。
 * @param props 初期一覧と子要素。
 * @returns プロバイダ。
 */
function StaticProjectProvider({
  children,
  initialProjects,
}: Readonly<{ children: ReactNode; initialProjects: ProjectSummary[] }>) {
  const [projects, setProjects] = useState(initialProjects);
  const [selectedProjectId, setSelectedProjectId] = useState(initialProjects[0]?.projectId ?? null);
  const selectProject = useCallback((projectId: string) => {
    setSelectedProjectId(projectId);
  }, []);
  const reload = useCallback(() => {
    setProjects(initialProjects);
    return Promise.resolve(true);
  }, [initialProjects]);
  const value = useProjectValue(projects, selectedProjectId, true, selectProject, reload);
  return <ProjectContext.Provider value={value}>{children}</ProjectContext.Provider>;
}

/**
 * 所属 Project の選択をヘッダーと一覧で共有する。
 * @param props `initialProjects` があるときは API を呼ばない。
 * @returns プロバイダ。
 */
export function ProjectProvider({ children, initialProjects }: Readonly<ProjectProviderProps>) {
  if (initialProjects) {
    return <StaticProjectProvider initialProjects={initialProjects}>{children}</StaticProjectProvider>;
  }
  return <LiveProjectProvider>{children}</LiveProjectProvider>;
}

/**
 * 所属 Project コンテキストを返す。
 * @returns 選択中 Project とロール解決。
 * @throws ProjectProvider の外で呼んだとき。
 */
export function useProject(): ProjectContextValue {
  const value = useContext(ProjectContext);
  if (!value) {
    throw new Error("ProjectProvider が必要です");
  }
  return value;
}
