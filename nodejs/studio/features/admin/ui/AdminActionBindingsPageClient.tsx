"use client";

import { useState, type FormEvent } from "react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { Card } from "@/shared/ui/Card";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { EmptyState } from "@/shared/ui/EmptyState";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { SelectField } from "@/shared/ui/SelectField";
import { TextAreaField } from "@/shared/ui/TextAreaField";
import { TextField } from "@/shared/ui/TextField";
import { useUiText } from "@/shared/i18n/uiTextContext";
import type { TenantActionBinding } from "../actionBindings";
import { useAdminActionBindingsPage } from "../hooks/useAdminActionBindingsPage";

/**
 * テナント管理者向けの Action バインディング画面。
 * 作成の送信だけを緑（主導線）にし、行の保存は紺、削除は赤枠で確認ダイアログを経てから送る。
 * @returns 一覧、作成、更新、削除。
 */
export function AdminActionBindingsPageClient() {
  const uiText = useUiText();
  const page = useAdminActionBindingsPage();
  const text = uiText.admin.actionBindings;
  const [deleteTargetName, setDeleteTargetName] = useState<string | null>(null);

  const onCreate = (event: FormEvent) => {
    event.preventDefault();
    void page.createBinding();
  };
  const bindingList = page.bindings.length === 0 ? (
    <EmptyState title={text.empty} />
  ) : (
    <ul className="space-y-4">
      {page.bindings.map((binding) => (
        <li key={binding.name}>
          <BindingRow
            binding={binding}
            draftValue={draftValueFor(binding, page.draftValues)}
            draftActions={page.draftActions[binding.name] ?? binding.allowedActionIds.join("\n")}
            submitting={page.submitting}
            onDraftValue={(next) =>
              page.setDraftValues((current) => ({
                ...current,
                [binding.name]: next
              }))
            }
            onDraftActions={(next) =>
              page.setDraftActions((current) => ({
                ...current,
                [binding.name]: next
              }))
            }
            onSave={() => void page.saveBinding(binding)}
            onDelete={() => setDeleteTargetName(binding.name)}
          />
        </li>
      ))}
    </ul>
  );

  return (
    <PageShell
      title={text.title}
      description={text.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantSettings }, { label: text.title }]} />}
    >
      {page.errorMessage ? <Notice tone="error" title={page.errorMessage} /> : null}
      <Card title={text.createTitle}>
        <form onSubmit={onCreate} className="flex flex-col gap-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label={text.nameLabel} value={page.name} onChange={(event) => page.setName(event.target.value)} required />
            <SelectField
              label={text.kindLabel}
              value={page.kind}
              onChange={(event) => page.setKind(event.target.value === "variable" ? "variable" : "secret")}
            >
              <option value="secret">{text.kindSecret}</option>
              <option value="variable">{text.kindVariable}</option>
            </SelectField>
            <TextField
              label={text.valueLabel}
              type={page.kind === "secret" ? "password" : "text"}
              value={page.value}
              autoComplete="off"
              onChange={(event) => page.setValue(event.target.value)}
              required
            />
            <TextAreaField
              label={text.allowedActionIdsLabel}
              value={page.allowedActionIdsText}
              onChange={(event) => page.setAllowedActionIdsText(event.target.value)}
              required
              rows={3}
              hint={text.allowedActionIdsHint}
            />
          </div>
          <div className="flex justify-end">
            <Button variant="lead" type="submit" loading={page.submitting}>
              {page.submitting ? text.creating : text.createSubmit}
            </Button>
          </div>
        </form>
      </Card>

      {page.loading ? null : bindingList}

      <ConfirmDialog
        open={deleteTargetName !== null}
        title={deleteTargetName ? text.deleteConfirmTitle(deleteTargetName) : ""}
        message={text.deleteConfirmMessage}
        confirmLabel={text.deleteConfirmSubmit}
        onConfirm={() => {
          if (deleteTargetName === null) return;
          setDeleteTargetName(null);
          void page.deleteBinding(deleteTargetName);
        }}
        onCancel={() => setDeleteTargetName(null)}
      />
    </PageShell>
  );
}

/**
 * 行の値入力。シークレットは空から始め、変数は保存済みの値を見せる。
 * @param binding 一覧の行。
 * @param drafts 編集中の値。
 * @returns 入力欄の値。
 */
function draftValueFor(binding: TenantActionBinding, drafts: Record<string, string>): string {
  const drafted = drafts[binding.name];
  if (drafted !== undefined) {
    return drafted;
  }
  if (binding.kind === "variable") {
    return binding.value ?? "";
  }
  return "";
}

/** 1 行の更新フォーム。シークレットの値は空から始める。 */
function BindingRow({
  binding,
  draftValue,
  draftActions,
  submitting,
  onDraftValue,
  onDraftActions,
  onSave,
  onDelete
}: Readonly<{
  binding: TenantActionBinding;
  draftValue: string;
  draftActions: string;
  submitting: boolean;
  onDraftValue: (value: string) => void;
  onDraftActions: (value: string) => void;
  onSave: () => void;
  onDelete: () => void;
}>) {
  const text = useUiText().admin.actionBindings;
  return (
    <form
      className="flex flex-col gap-3 rounded-card border border-md-outline bg-md-surface p-5"
      onSubmit={(event) => {
        event.preventDefault();
        onSave();
      }}
    >
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="font-semibold text-md-on-surface">{binding.name}</h3>
        <span className="inline-flex h-5 items-center rounded-pill bg-md-surface-container-high px-2 text-[11px] font-semibold text-md-on-surface-variant">
          {binding.kind === "secret" ? text.kindSecret : text.kindVariable}
        </span>
      </div>
      <p className="text-xs text-md-on-surface-variant">{text.updatedAt(binding.updatedAt)}</p>
      {binding.kind === "secret" ? <p className="text-sm text-md-on-surface">{text.valueConfigured}</p> : null}
      <TextField
        label={text.valueLabel}
        type={binding.kind === "secret" ? "password" : "text"}
        value={draftValue}
        autoComplete="off"
        onChange={(event) => onDraftValue(event.target.value)}
        hint={binding.kind === "secret" ? text.secretValueHint : undefined}
      />
      <TextAreaField label={text.allowedActionIdsLabel} value={draftActions} rows={3} onChange={(event) => onDraftActions(event.target.value)} />
      <div className="flex flex-wrap justify-between gap-2">
        <Button variant="danger" disabled={submitting} onClick={onDelete}>
          {submitting ? text.deleting : text.delete}
        </Button>
        <Button variant="standard" type="submit" loading={submitting}>
          {submitting ? text.saving : text.save}
        </Button>
      </div>
    </form>
  );
}
