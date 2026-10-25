"use client";

import Link from "next/link";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { canAdmin } from "../roles";
import { useProject } from "../projectContext";

/**
 * ヘッダーの所属 Project 切替。未所属の Project は選択肢に出さない。
 * @returns セレクト。所属が無いときは null。
 */
export function ProjectSwitcher() {
  const uiText = useUiText();
  const { projects, selectedProject, selectProject, loaded } = useProject();
  if (!loaded || projects.length === 0 || !selectedProject) {
    return null;
  }

  return (
    <div className="flex items-center gap-2 text-sm">
      <label className="flex items-center gap-2">
        <span className="text-brand-header-fg-muted">{uiText.projects.switcher.label}</span>
        <select
          className="rounded-sm border border-white/20 bg-transparent px-2 py-1 text-brand-header-fg"
          aria-label={uiText.projects.switcher.label}
          value={selectedProject.projectId}
          onChange={(event) => selectProject(event.target.value)}
        >
          {projects.map((project) => (
            <option key={project.projectId} value={project.projectId}>
              {uiText.projects.switcher.option(
                project.displayName,
                project.role ? uiText.projects.roles[project.role] : "",
              )}
            </option>
          ))}
        </select>
      </label>
      {canAdmin(selectedProject.role) ? (
        <Link href="/projects/members" className="hover:text-brand-header-fg hover:underline">
          {uiText.projects.switcher.members}
        </Link>
      ) : null}
    </div>
  );
}
