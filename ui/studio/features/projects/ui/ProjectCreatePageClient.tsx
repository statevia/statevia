"use client";

import { useCallback } from "react";
import { PROJECTS_CREATE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { PageShell } from "@/shared/ui/PageShell";
import { useProject } from "../projectContext";
import type { ProjectSummary } from "../types";
import { ProjectCreateForm } from "./ProjectCreateForm";

/**
 * `/projects/new`。`projects.create` を持つ利用者だけにフォームを出す。所属 0 件でも作れる。
 * 作成後は所属一覧を取り直し、作った Project を選択する。
 * @returns 作成画面。
 */
export function ProjectCreatePageClient() {
  const uiText = useUiText();
  const text = uiText.projects.create;
  const permissions = usePermissions();
  const { reload, selectProject } = useProject();

  const handleCreated = useCallback(
    async (project: ProjectSummary) => {
      const reloaded = await reload();
      if (reloaded) {
        selectProject(project.projectId);
      }
      return reloaded;
    },
    [reload, selectProject],
  );

  return (
    <PageShell title={text.title}>
      {!permissions.loaded && <p className="text-sm text-md-on-surface-variant">{uiText.actions.loading}</p>}
      {permissions.loaded && !permissions.has(PROJECTS_CREATE_PERMISSION) && (
        <p className="text-sm text-md-on-surface-variant">{text.noPermission}</p>
      )}
      {permissions.loaded && permissions.has(PROJECTS_CREATE_PERMISSION) && (
        <ProjectCreateForm onCreated={handleCreated} />
      )}
    </PageShell>
  );
}
