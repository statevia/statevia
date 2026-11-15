"use client";

import { Plus } from "lucide-react";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button, buttonClassName } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { apiGet, apiPost } from "@/shared/api";
import type { AdminGroupListItem } from "../types";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { isWithinMaxLength, matchesPattern } from "@/shared/lib/validation/primitives";
import { ASCII_LABEL_DEFAULT_MAX_LENGTH, ASCII_LABEL_PATTERN } from "@/shared/lib/validation/formRules";
import { TextField } from "@/shared/ui/TextField";
import { useAdminGroupsUiText, useUiText } from "@/shared/i18n/uiTextContext";

/** 作成フォームの `<form>` の id。ダイアログ下部の送信ボタンから `form` 属性で結ぶ。 */
const CREATE_FORM_ID = "admin-group-create-form";

/**
 * グループ一覧・作成。作成は見出しの緑のボタンからダイアログで行う（studio-ui-visual-refresh の U5）。
 */
export function AdminGroupsPageClient() {
  const pageUi = useAdminGroupsUiText();
  const uiText = useUiText();
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [groupList, setGroupList] = useState<AdminGroupListItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [name, setName] = useState("");

  const loadGroups = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const list = await apiGet<AdminGroupListItem[]>("/admin/groups");
      setGroupList(list);
    } catch (error) {
      setToast(toToastError(error));
      setGroupList(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadGroups();
  }, [loadGroups]);

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setToast(null);
    const trimmedName = name.trim();
    if (
      !isWithinMaxLength(trimmedName, ASCII_LABEL_DEFAULT_MAX_LENGTH) ||
      !matchesPattern(trimmedName, ASCII_LABEL_PATTERN)
    ) {
      setToast({ tone: "error", message: pageUi.nameInvalidFormat });
      setSubmitting(false);
      return;
    }
    try {
      await apiPost("/admin/groups", { name: trimmedName });
      setName("");
      setCreateDialogOpen(false);
      await loadGroups();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSubmitting(false);
    }
  }

  function renderListBody() {
    if (loading) return <PageState state="loading" />;
    if (groupList === null) return <PageState state="error" />;
    if (groupList.length === 0) return <PageState state="empty" />;
    return (
      <ul className="divide-y divide-md-outline overflow-hidden rounded-card border border-md-outline bg-md-surface">
        {groupList.map((item) => (
          <li
            key={item.groupId}
            className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
          >
            <div>
              <p className="font-semibold text-md-on-surface">
                {item.name}
                {item.isSystem ? (
                  <span className="ml-2 inline-flex h-5 items-center rounded-pill bg-md-surface-container-high px-2 text-[11px] font-semibold text-md-on-surface-variant">
                    {pageUi.systemBadge}
                  </span>
                ) : null}
              </p>
              <p className="text-xs text-md-on-surface-variant">
                {pageUi.memberCount(item.memberCount)} · {pageUi.permissionCount(item.permissionCount)}
              </p>
            </div>
            <Link
              href={`/admin/groups/${item.groupId}`}
              className={buttonClassName("outline", "sm")}
            >
              {pageUi.openDetail}
            </Link>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <PageShell
      title={pageUi.title}
      description={pageUi.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantAccess }, { label: pageUi.title }]} />}
      primaryActions={
        <Button
          variant="lead"
          onClick={() => {
            setToast(null);
            setCreateDialogOpen(true);
          }}
        >
          <Plus aria-hidden="true" className="h-4 w-4" />
          {pageUi.createTitle}
        </Button>
      }
    >
      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}

      {renderListBody()}

      <Dialog
        open={createDialogOpen}
        onClose={() => setCreateDialogOpen(false)}
        title={pageUi.createTitle}
        dismissible={!submitting}
        footer={
          <>
            <Button variant="outline" onClick={() => setCreateDialogOpen(false)} disabled={submitting}>
              {uiText.actions.cancel}
            </Button>
            <Button variant="lead" type="submit" form={CREATE_FORM_ID} loading={submitting}>
              {submitting ? pageUi.creating : pageUi.createSubmit}
            </Button>
          </>
        }
      >
        <form
          id={CREATE_FORM_ID}
          onSubmit={(event) => {
            void handleCreate(event);
          }}
        >
          <TextField label={pageUi.nameLabel} type="text" required value={name} onChange={(e) => setName(e.target.value)} />
        </form>
      </Dialog>
    </PageShell>
  );
}
