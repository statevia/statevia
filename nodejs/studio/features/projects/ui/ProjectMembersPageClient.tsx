"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { PageShell } from "@/shared/ui/PageShell";
import { canAdmin } from "../roles";
import { useProject } from "../projectContext";
import { ProjectMembersPanel } from "./ProjectMembersPanel";

/**
 * 選択中 Project のメンバー画面。project.admin 以外は操作しない。
 * @returns メンバー画面。
 */
export function ProjectMembersPageClient() {
  const uiText = useUiText();
  const text = uiText.projects.members;
  const { selectedProject, loaded } = useProject();

  let body = <p className="text-sm text-md-on-surface-variant">{text.emptyProject}</p>;
  if (!loaded) {
    body = <p className="text-sm text-md-on-surface-variant">{uiText.actions.loading}</p>;
  } else if (selectedProject && !canAdmin(selectedProject.role)) {
    body = <p className="text-sm text-md-on-surface-variant">{text.forbidden}</p>;
  } else if (selectedProject) {
    body = <ProjectMembersPanel projectId={selectedProject.projectId} />;
  }

  return (
    <PageShell title={text.title} description={text.description}>
      {body}
    </PageShell>
  );
}
