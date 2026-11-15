"use client";

import { useCallback, useEffect, useState } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { Button } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { FieldLabel } from "@/shared/ui/FormField";
import { SelectField } from "@/shared/ui/SelectField";
import { Toast } from "@/shared/ui/Toast";
import {
  listGrantCandidates,
  listProjectMembers,
  removeProjectMember,
  upsertProjectMember,
} from "../api";
import { PROJECT_ROLES, type ProjectRole } from "@/shared/project/types";
import type { ProjectGrantCandidate, ProjectMember } from "../types";

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
 * 定義エディタへの導線は持たない。メンバーから外す操作は確認ダイアログを経てから送る。
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
  const [revokeTarget, setRevokeTarget] = useState<ProjectMember | null>(null);
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
    <section className="flex flex-col gap-4 rounded-card border border-md-outline bg-md-surface p-5">
      <Toast toast={toast} onClose={() => setToast(null)} />
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          void grant();
        }}
      >
        <div className="min-w-[240px] flex-1">
          {grantChoices.length === 0 ? (
            <>
              <FieldLabel label={text.candidateLabel} />
              <p className="mt-1.5 text-sm text-md-on-surface-variant">{text.emptyCandidates}</p>
            </>
          ) : (
            <SelectField label={text.candidateLabel} value={selectedId} onChange={(event) => setSelectedId(event.target.value)}>
              {grantChoices.map((candidate) => (
                <option key={candidate.principalId} value={candidate.principalId}>
                  {labelOf(candidate.displayName, candidate.kind)}
                </option>
              ))}
            </SelectField>
          )}
        </div>
        <div className="w-48">
          <SelectField
            label={text.roleLabel}
            value={role}
            onChange={(event) => {
              const next = PROJECT_ROLES.find((item) => item === event.target.value);
              if (next) setRole(next);
            }}
          >
            {PROJECT_ROLES.map((item) => (
              <option key={item} value={item}>{uiText.projects.roles[item]}</option>
            ))}
          </SelectField>
        </div>
        <Button variant="standard" type="submit" loading={granting} disabled={grantChoices.length === 0}>
          {granting ? text.granting : text.grant}
        </Button>
      </form>
      {members.length === 0 ? (
        <p className="text-sm text-md-on-surface-variant">{text.empty}</p>
      ) : (
        <ul className="divide-y divide-md-outline-variant border-t border-md-outline-variant">
          {members.map((member) => (
            <li key={member.principalId} className="flex flex-wrap items-center justify-between gap-3 py-3">
              <span className="text-sm text-md-on-surface">{labelOf(member.displayName, member.kind)}</span>
              <div className="flex items-center gap-2">
                <select
                  aria-label={`${labelOf(member.displayName, member.kind)} ${text.roleLabel}`}
                  className="h-8 w-40 rounded-control border border-md-outline-variant bg-md-surface-container px-2 text-sm text-md-on-surface disabled:opacity-60"
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
                <Button
                  variant="danger"
                  size="sm"
                  disabled={busyPrincipalId === member.principalId}
                  onClick={() => setRevokeTarget(member)}
                >
                  {busyPrincipalId === member.principalId ? text.revoking : text.revoke}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <ConfirmDialog
        open={revokeTarget !== null}
        title={revokeTarget ? text.revokeConfirmTitle(labelOf(revokeTarget.displayName, revokeTarget.kind)) : ""}
        message={text.revokeConfirmMessage}
        confirmLabel={text.revokeConfirmSubmit}
        onConfirm={() => {
          if (revokeTarget === null) return;
          setRevokeTarget(null);
          void revoke(revokeTarget);
        }}
        onCancel={() => setRevokeTarget(null)}
      />
    </section>
  );
}
