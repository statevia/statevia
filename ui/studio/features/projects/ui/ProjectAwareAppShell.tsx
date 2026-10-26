"use client";

import type { ComponentProps } from "react";
import { AppShell } from "@/shared/ui/AppShell";
import { canAdmin } from "../roles";
import { useProject } from "../projectContext";
import { ProjectSwitcher } from "./ProjectSwitcher";

type ProjectAwareAppShellProps = Omit<ComponentProps<typeof AppShell>, "isProjectAdmin" | "projectSlot">;

/**
 * 選択中 Project のロールと Project 切替を `AppShell` へ渡すアダプタ。
 * `shared/ui` から `features/projects` を参照しないよう、依存はこちら側に置く。
 * Project 一覧の未ロード中や所属 0 件では、Project 管理者として扱わず、切替の枠も出さない。
 */
export function ProjectAwareAppShell(props: Readonly<ProjectAwareAppShellProps>) {
  const { selectedProject } = useProject();
  return (
    <AppShell
      {...props}
      isProjectAdmin={canAdmin(selectedProject?.role)}
      projectSlot={selectedProject ? <ProjectSwitcher /> : null}
    />
  );
}
