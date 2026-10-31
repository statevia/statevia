"use client";

import type { ComponentProps } from "react";
import { PROJECTS_CREATE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";
import { AppShell } from "@/shared/ui/AppShell";
import { canAdmin } from "@/shared/project/roles";
import { useProject } from "@/shared/project/projectContext";
import { ProjectSwitcher } from "./ProjectSwitcher";

type ProjectAwareAppShellProps = Omit<ComponentProps<typeof AppShell>, "isProjectAdmin" | "canCreateProject" | "projectSlot">;

/**
 * 選択中 Project のロールと Project 切替を `AppShell` へ渡すアダプタ。
 * `shared/ui` から `features/projects` を参照しないよう、依存はこちら側に置く。
 * Project 一覧の未ロード中や所属 0 件では、Project 管理者として扱わず、切替の枠も出さない。
 * 作成リンクは `projects.create` だけで決め、所属 0 件でも出す（最初の Project を画面から作るため）。
 */
export function ProjectAwareAppShell(props: Readonly<ProjectAwareAppShellProps>) {
  const { selectedProject } = useProject();
  const permissions = usePermissions();
  return (
    <AppShell
      {...props}
      isProjectAdmin={canAdmin(selectedProject?.role)}
      canCreateProject={permissions.has(PROJECTS_CREATE_PERMISSION)}
      projectSlot={selectedProject ? <ProjectSwitcher /> : null}
    />
  );
}
