"use client";

import type { FormEvent } from "react";
import { PageShell } from "@/shared/ui/PageShell";
import { useUiText } from "@/shared/i18n/uiTextContext";
import type { TenantActionBinding } from "../actionBindings";
import { useAdminActionBindingsPage } from "../hooks/useAdminActionBindingsPage";

/**
 * テナント管理者向けの Action バインディング画面。
 * @returns 一覧、作成、更新、削除。
 */
export function AdminActionBindingsPageClient() {
  const uiText = useUiText();
  const page = useAdminActionBindingsPage();
  const text = uiText.admin.actionBindings;

  const onCreate = (event: FormEvent) => {
    event.preventDefault();
    void page.createBinding();
  };
  const bindingList = page.bindings.length === 0 ? (
    <p className="text-sm">{text.empty}</p>
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
            onDelete={() => void page.deleteBinding(binding.name)}
          />
        </li>
      ))}
    </ul>
  );

  return (
    <PageShell title={text.title} description={text.description}>
      {page.errorMessage ? <p className="mb-4 text-sm text-red-700">{page.errorMessage}</p> : null}
      <form onSubmit={onCreate} className="mb-6 rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs">
        <h2 className="mb-3 text-lg font-medium">{text.createTitle}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block text-sm font-medium">
            {text.nameLabel}
            <input
              value={page.name}
              onChange={(event) => page.setName(event.target.value)}
              required
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
          <label className="block text-sm font-medium">
            {text.kindLabel}
            <select
              value={page.kind}
              onChange={(event) => page.setKind(event.target.value === "variable" ? "variable" : "secret")}
              className="mt-1 w-full rounded-lg border border-md-outline bg-md-surface px-3 py-2 text-sm text-md-on-surface"
            >
              <option value="secret" className="bg-md-surface text-md-on-surface">{text.kindSecret}</option>
              <option value="variable" className="bg-md-surface text-md-on-surface">{text.kindVariable}</option>
            </select>
          </label>
          <label className="block text-sm font-medium">
            {text.valueLabel}
            <input
              type={page.kind === "secret" ? "password" : "text"}
              value={page.value}
              autoComplete="off"
              onChange={(event) => page.setValue(event.target.value)}
              required
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </label>
          <label className="block text-sm font-medium">
            {text.allowedActionIdsLabel}
            <textarea
              value={page.allowedActionIdsText}
              onChange={(event) => page.setAllowedActionIdsText(event.target.value)}
              required
              rows={3}
              className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
            <span className="mt-1 block text-xs font-normal">{text.allowedActionIdsHint}</span>
          </label>
        </div>
        <button
          type="submit"
          disabled={page.submitting}
          className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {page.submitting ? text.creating : text.createSubmit}
        </button>
      </form>

      {page.loading ? null : bindingList}
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
}: {
  binding: TenantActionBinding;
  draftValue: string;
  draftActions: string;
  submitting: boolean;
  onDraftValue: (value: string) => void;
  onDraftActions: (value: string) => void;
  onSave: () => void;
  onDelete: () => void;
}) {
  const text = useUiText().admin.actionBindings;
  return (
    <form
      className="rounded-xl border border-md-outline bg-md-surface p-4"
      onSubmit={(event) => {
        event.preventDefault();
        onSave();
      }}
    >
      <div className="mb-2 flex items-baseline justify-between gap-3">
        <h3 className="font-medium">{binding.name}</h3>
        <span className="text-xs">{binding.kind === "secret" ? text.kindSecret : text.kindVariable}</span>
      </div>
      <p className="mb-3 text-xs">{text.updatedAt(binding.updatedAt)}</p>
      {binding.kind === "secret" ? <p className="mb-2 text-sm">{text.valueConfigured}</p> : null}
      <label className="mb-3 block text-sm font-medium">
        {text.valueLabel}
        <input
          type={binding.kind === "secret" ? "password" : "text"}
          value={draftValue}
          autoComplete="off"
          onChange={(event) => onDraftValue(event.target.value)}
          className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
        />
        {binding.kind === "secret" ? <span className="mt-1 block text-xs font-normal">{text.secretValueHint}</span> : null}
      </label>
      <label className="mb-3 block text-sm font-medium">
        {text.allowedActionIdsLabel}
        <textarea
          value={draftActions}
          rows={3}
          onChange={(event) => onDraftActions(event.target.value)}
          className="mt-1 w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
        />
      </label>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={submitting}
          className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? text.saving : text.save}
        </button>
        <button
          type="button"
          disabled={submitting}
          onClick={onDelete}
          className="rounded-lg border border-md-outline px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container disabled:opacity-60"
        >
          {submitting ? text.deleting : text.delete}
        </button>
      </div>
    </form>
  );
}
