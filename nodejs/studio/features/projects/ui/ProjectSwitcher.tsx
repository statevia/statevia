"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { useProject } from "@/shared/project/projectContext";

/**
 * サイドメニュー先頭の所属 Project 切替。未所属の Project は選択肢に出さない。
 * メンバー画面への導線はサイドメニューの「プロジェクト」グループが持つ。
 * @returns セレクト。所属が無いときは null。
 */
export function ProjectSwitcher() {
  const uiText = useUiText();
  const { projects, selectedProject, selectProject, loaded } = useProject();
  if (!loaded || projects.length === 0 || !selectedProject) {
    return null;
  }

  return (
    <label className="flex flex-col gap-1 px-2 text-sm">
      <span className="text-xs font-semibold text-md-on-surface-variant">{uiText.projects.switcher.label}</span>
      <select
        className="w-full rounded-md border border-md-outline bg-md-surface px-2 py-1 text-md-on-surface"
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
  );
}
