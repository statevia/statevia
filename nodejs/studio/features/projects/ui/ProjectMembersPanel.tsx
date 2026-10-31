"use client";

import { useCallback, useEffect, useState } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { Toast } from "@/shared/ui/Toast";
import {
  listGrantCandidates,
  listProjectMembers,
  removeProjectMember,
  upsertProjectMember,
} from "../api";
import { PROJECT_ROLES, type ProjectGrantCandidate, type ProjectMember, type ProjectRole } from "../types";

type ProjectMembersPanelProps = {
  /** 操作対象の Project。 */
  projectId: string;
};

/**
 * まだメンバーでない候補だけを残す。
 * @param candidates 付与候補。
 * @param members 現在のメンバー。
 * @returns 追加用の候補。
 */
function availableCandidates(
  candidates: readonly ProjectGrantCandidate[],
  members: readonly ProjectMember[],
): ProjectGrantCandidate[] {
  const memberIds = new Set(members.map((member) => member.principalId));
  return candidates.filter((candidate) => !memberIds.has(candidate.principalId));
}

/**
 * メンバーの付与、ロール変更、剥奪。
 * 表示はユーザー名と API キー名。Principal UUID は画面に出さない。
 * 定義エディタへの導線は持たない。
 * @param props 対象 Project。
 * @returns メンバーパネル。
 */
export function ProjectMembersPanel({ projectId }: Readonly<ProjectMembersPanelProps>) {
  const uiText = useUiText();
  const text = uiText.projects.members;
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [candidates, setCandidates] = useState<ProjectGrantCandidate[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [role, setRole] = useState<ProjectRole>("reader");
  const [busyPrincipalId, setBusyPrincipalId] = useState<string | null>(null);
  const [granting, setGranting] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);
  const grantChoices = availableCandidates(candidates, members);

  const load = useCallback(async () => {
    const [nextMembers, nextCandidates] = await Promise.all([
      listProjectMembers(projectId),
      listGrantCandidates(projectId),
    ]);
    setMembers(nextMembers);
    setCandidates(nextCandidates);
  }, [projectId]);

  useEffect(() => {
    let cancelled = false;
    void Promise.all([listProjectMembers(projectId), listGrantCandidates(projectId)])
      .then(([nextMembers, nextCandidates]) => {
        if (cancelled) return;
        setMembers(nextMembers);
        setCandidates(nextCandidates);
      })
      .catch((error: unknown) => {
        if (!cancelled) setToast(toToastError(error));
      });
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  useEffect(() => {
    const choices = availableCandidates(candidates, members);
    setSelectedId((current) => (
      choices.some((candidate) => candidate.principalId === current)
        ? current
        : (choices[0]?.principalId ?? "")
    ));
  }, [candidates, members]);

  const labelOf = (displayName: string, kind: ProjectMember["kind"]) =>
    text.label(displayName, text.kinds[kind]);

  const grant = async () => {
    const target = grantChoices.find((candidate) => candidate.principalId === selectedId);
    if (!target) {
      setToast({ tone: "error", message: text.candidateRequired });
      return;
    }
    setGranting(true);
    setToast(null);
    try {
      await upsertProjectMember(projectId, target.principalId, role);
      await load();
      setToast({ tone: "success", message: text.granted });
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setGranting(false);
    }
  };

  const changeRole = async (member: ProjectMember, nextRole: ProjectRole) => {
    setBusyPrincipalId(member.principalId);
    setToast(null);
    try {
      await upsertProjectMember(projectId, member.principalId, nextRole);
      await load();
      setToast({ tone: "success", message: text.granted });
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setBusyPrincipalId(null);
    }
  };

  const revoke = async (member: ProjectMember) => {
    setBusyPrincipalId(member.principalId);
    setToast(null);
    try {
      await removeProjectMember(projectId, member.principalId);
      await load();
      setToast({ tone: "success", message: text.revoked });
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setBusyPrincipalId(null);
    }
  };

  return (
    <section className="space-y-4 rounded-lg border border-md-outline bg-md-surface p-4">
      <Toast toast={toast} onClose={() => setToast(null)} />
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          void grant();
        }}
      >
        <label className="min-w-[240px] flex-1 text-sm">
          <span className="text-md-on-surface-variant">{text.candidateLabel}</span>
          {grantChoices.length === 0 ? (
            <p className="mt-1 text-sm text-md-on-surface-variant">{text.emptyCandidates}</p>
          ) : (
            <select
              className="mt-1 w-full rounded-sm border border-md-outline-variant bg-md-surface-container px-3 py-2 text-sm text-md-on-surface"
              value={selectedId}
              onChange={(event) => setSelectedId(event.target.value)}
            >
              {grantChoices.map((candidate) => (
                <option key={candidate.principalId} value={candidate.principalId}>
                  {labelOf(candidate.displayName, candidate.kind)}
                </option>
              ))}
            </select>
          )}
        </label>
        <label className="text-sm">
          <span className="text-md-on-surface-variant">{text.roleLabel}</span>
          <select
            className="mt-1 rounded-sm border border-md-outline-variant bg-md-surface-container px-3 py-2 text-sm text-md-on-surface"
            value={role}
            onChange={(event) => {
              const next = PROJECT_ROLES.find((item) => item === event.target.value);
              if (next) setRole(next);
            }}
          >
            {PROJECT_ROLES.map((item) => (
              <option key={item} value={item}>{uiText.projects.roles[item]}</option>
            ))}
          </select>
        </label>
        <button
          type="submit"
          className="rounded-sm border-2 border-brand-cta-border bg-brand-cta-bg px-4 py-2 text-sm font-medium text-brand-cta-fg hover:bg-brand-cta-bg-hover disabled:opacity-60"
          disabled={granting || grantChoices.length === 0}
        >
          {granting ? text.granting : text.grant}
        </button>
      </form>
      {members.length === 0 ? (
        <p className="text-sm text-md-on-surface-variant">{text.empty}</p>
      ) : (
        <ul className="divide-y divide-md-outline">
          {members.map((member) => (
            <li key={member.principalId} className="flex flex-wrap items-center justify-between gap-3 py-3">
              <span className="text-sm text-md-on-surface">{labelOf(member.displayName, member.kind)}</span>
              <div className="flex items-center gap-2">
                <select
                  aria-label={`${labelOf(member.displayName, member.kind)} ${text.roleLabel}`}
                  className="rounded-sm border border-md-outline-variant bg-md-surface-container px-2 py-1 text-sm"
                  value={member.role}
                  disabled={busyPrincipalId === member.principalId}
                  onChange={(event) => {
                    const next = PROJECT_ROLES.find((item) => item === event.target.value);
                    if (next && next !== member.role) void changeRole(member, next);
                  }}
                >
                  {PROJECT_ROLES.map((item) => (
                    <option key={item} value={item}>{uiText.projects.roles[item]}</option>
                  ))}
                </select>
                <button
                  type="button"
                  className="rounded-sm border border-md-outline-variant px-3 py-1.5 text-sm text-md-on-surface disabled:opacity-60"
                  disabled={busyPrincipalId === member.principalId}
                  onClick={() => void revoke(member)}
                >
                  {busyPrincipalId === member.principalId ? text.revoking : text.revoke}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
