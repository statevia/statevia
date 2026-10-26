"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Toast } from "@/shared/ui/Toast";
import { useProjectCreateForm, type ProjectCreatedHandler } from "../hooks/useProjectCreateForm";
import {
  PROJECT_DISPLAY_NAME_MAX_LENGTH,
  PROJECT_SLUG_MAX_LENGTH,
  type ProjectCreateFieldErrorKind,
} from "../projectCreate";

type ProjectCreateFormProps = {
  /** 作成成功後の後処理。所属の再取得と選択は呼び出し側が行う。 */
  onCreated: ProjectCreatedHandler;
};

type FieldErrorProps = {
  id: string;
  kind: ProjectCreateFieldErrorKind | undefined;
  maxLength: number;
};

/**
 * 欄の下に出す項目エラー。
 * @param props 要素 ID、エラー種別、上限。
 * @returns エラー文。無ければ null。
 */
function FieldError({ id, kind, maxLength }: Readonly<FieldErrorProps>) {
  const errors = useUiText().projects.create.errors;
  if (kind === undefined) return null;
  const messages: Record<ProjectCreateFieldErrorKind, string> = {
    required: errors.required,
    tooLong: errors.tooLong(maxLength),
    duplicate: errors.duplicate,
  };
  return (
    <p id={id} className="mt-1 text-xs text-md-error">
      {messages[kind]}
    </p>
  );
}

/**
 * Project の作成フォーム。`/projects/new` と `/admin/projects` で共有する。
 * 表示条件（`projects.create`）は呼び出し側で判定する。作成の強制は Service API。
 * @param props 作成後の後処理。
 * @returns フォーム。
 */
export function ProjectCreateForm({ onCreated }: Readonly<ProjectCreateFormProps>) {
  const text = useUiText().projects.create;
  const form = useProjectCreateForm(onCreated);
  const slugError = form.fieldErrors.slug;
  const displayNameError = form.fieldErrors.displayName;

  return (
    <>
      <Toast toast={form.toast} onClose={form.clearToast} />
      <form
        noValidate
        onSubmit={(event) => void form.submit(event)}
        className="rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs"
      >
        <h2 className="mb-1 text-lg font-medium">{text.title}</h2>
        <p className="mb-3 text-sm text-md-on-surface-variant">{text.description}</p>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="project-create-slug" className="mb-1 block text-sm font-medium">
              {text.slugLabel}
            </label>
            <input
              id="project-create-slug"
              type="text"
              autoComplete="off"
              value={form.slug}
              onChange={(event) => form.setSlug(event.target.value)}
              aria-invalid={slugError !== undefined}
              aria-describedby={slugError === undefined ? "project-create-slug-hint" : "project-create-slug-error"}
              className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
            {slugError === undefined ? (
              <p id="project-create-slug-hint" className="mt-1 text-xs text-md-on-surface-variant">
                {text.slugHint}
              </p>
            ) : (
              <FieldError id="project-create-slug-error" kind={slugError} maxLength={PROJECT_SLUG_MAX_LENGTH} />
            )}
          </div>
          <div>
            <label htmlFor="project-create-display-name" className="mb-1 block text-sm font-medium">
              {text.displayNameLabel}
            </label>
            <input
              id="project-create-display-name"
              type="text"
              autoComplete="off"
              value={form.displayName}
              onChange={(event) => form.setDisplayName(event.target.value)}
              aria-invalid={displayNameError !== undefined}
              aria-describedby={displayNameError === undefined ? undefined : "project-create-display-name-error"}
              className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
            <FieldError
              id="project-create-display-name-error"
              kind={displayNameError}
              maxLength={PROJECT_DISPLAY_NAME_MAX_LENGTH}
            />
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="project-create-description" className="mb-1 block text-sm font-medium">
              {text.descriptionLabel}
            </label>
            <textarea
              id="project-create-description"
              rows={3}
              value={form.description}
              onChange={(event) => form.setDescription(event.target.value)}
              className="w-full rounded-lg border border-md-outline px-3 py-2 text-sm"
            />
          </div>
        </div>
        <button
          type="submit"
          disabled={form.submitting}
          className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {form.submitting ? text.submitting : text.submit}
        </button>
      </form>
    </>
  );
}
