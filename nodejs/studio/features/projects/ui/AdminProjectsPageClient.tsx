"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { PROJECTS_CREATE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { buttonClassName } from "@/shared/ui/Button";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { EmptyState } from "@/shared/ui/EmptyState";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { SelectField } from "@/shared/ui/SelectField";
import { Toast } from "@/shared/ui/Toast";
import { listAdminProjects } from "../api";
import type { ProjectSummary } from "@/shared/project/types";
import { ProjectMembersPanel } from "./ProjectMembersPanel";

/**
 * tenant.admin 向けの全 Project とメンバー。定義エディタへは進まない。
 * `projects.create` があるときは `/projects/new` への作成ボタンを出す。フォームは作成画面の 1 か所に置き、ここには置かない。
 * @returns 管理画面。
 */
export function AdminProjectsPageClient() {
  const uiText = useUiText();
  const text = uiText.projects.admin;
  const [projects, setProjects] = useState<ProjectSummary[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const permissions = usePermissions();

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
    <PageShell
      title={text.title}
      description={text.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantSettings }, { label: text.title }]} />}
      primaryActions={
        permissions.has(PROJECTS_CREATE_PERMISSION) && (
          <Link href="/projects/new" className={buttonClassName("lead")}>
            {text.create}
          </Link>
        )
      }
    >
      <Toast toast={toast} onClose={() => setToast(null)} />
      <p className="text-sm text-md-on-surface-variant">{text.definitionClosed}</p>
      {projects === null && <p className="text-sm text-md-on-surface-variant">{uiText.actions.loading}</p>}
      {projects !== null && projects.length === 0 && !forbidden && (
        <EmptyState title={text.empty} />
      )}
      {forbidden && projects?.length === 0 && <Notice tone="error" title={text.forbidden} />}
      {projects !== null && projects.length > 0 && selectedId && (
        <>
          <div className="max-w-sm">
            <SelectField label={text.select} value={selectedId} onChange={(event) => setSelectedId(event.target.value)}>
              {projects.map((project) => (
                <option key={project.projectId} value={project.projectId}>{project.displayName}</option>
              ))}
            </SelectField>
          </div>
          <ProjectMembersPanel projectId={selectedId} />
        </>
      )}
    </PageShell>
  );
}
