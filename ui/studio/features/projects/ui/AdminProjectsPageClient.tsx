"use client";

import { useEffect, useState } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { PageShell } from "@/shared/ui/PageShell";
import { Toast } from "@/shared/ui/Toast";
import { listAdminProjects } from "../api";
import type { ProjectSummary } from "../types";
import { ProjectMembersPanel } from "./ProjectMembersPanel";

/**
 * tenant.admin 向けの全 Project とメンバー。定義エディタへは進まない。
 * @returns 管理画面。
 */
export function AdminProjectsPageClient() {
  const uiText = useUiText();
  const text = uiText.projects.admin;
  const [projects, setProjects] = useState<ProjectSummary[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);

  useEffect(() => {
    let cancelled = false;
    void listAdminProjects()
      .then((rows) => {
        if (cancelled) return;
        setProjects(rows);
        setSelectedId(rows[0]?.projectId ?? null);
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setProjects([]);
          setToast(toToastError(error));
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const forbidden = toast?.tone === "error";

  return (
    <PageShell title={text.title} description={text.description}>
      <Toast toast={toast} onClose={() => setToast(null)} />
      <p className="text-sm text-md-on-surface-variant">{text.definitionClosed}</p>
      {projects === null && <p className="text-sm text-md-on-surface-variant">{uiText.actions.loading}</p>}
      {projects !== null && projects.length === 0 && !forbidden && (
        <p className="text-sm text-md-on-surface-variant">{text.empty}</p>
      )}
      {forbidden && projects?.length === 0 && (
        <p className="text-sm text-md-on-surface-variant">{text.forbidden}</p>
      )}
      {projects !== null && projects.length > 0 && selectedId && (
        <>
          <label className="block text-sm">
            <span className="text-md-on-surface-variant">{text.select}</span>
            <select
              className="mt-1 rounded-sm border border-md-outline-variant bg-md-surface-container px-3 py-2 text-sm text-md-on-surface"
              value={selectedId}
              onChange={(event) => setSelectedId(event.target.value)}
            >
              {projects.map((project) => (
                <option key={project.projectId} value={project.projectId}>{project.displayName}</option>
              ))}
            </select>
          </label>
          <ProjectMembersPanel projectId={selectedId} />
        </>
      )}
    </PageShell>
  );
}
