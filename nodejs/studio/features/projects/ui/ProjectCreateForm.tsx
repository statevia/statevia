"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { TextAreaField } from "@/shared/ui/TextAreaField";
import { TextField } from "@/shared/ui/TextField";
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

/**
 * 項目エラーの文言を選ぶ。
 * @param kind エラー種別。無ければ undefined。
 * @param maxLength 欄の上限。
 * @param errors 辞書のエラー文。
 * @returns 文言。無ければ undefined。
 */
function fieldErrorText(
  kind: ProjectCreateFieldErrorKind | undefined,
  maxLength: number,
  errors: ReturnType<typeof useUiText>["projects"]["create"]["errors"],
): string | undefined {
  if (kind === undefined) return undefined;
  const messages: Record<ProjectCreateFieldErrorKind, string> = {
    required: errors.required,
    tooLong: errors.tooLong(maxLength),
    duplicate: errors.duplicate,
  };
  return messages[kind];
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

  return (
    <>
      <Toast toast={form.toast} onClose={form.clearToast} />
      <form
        noValidate
        onSubmit={(event) => void form.submit(event)}
        className="flex flex-col gap-4 rounded-card border border-md-outline bg-md-surface p-5"
      >
        <div>
          <h2 className="text-base font-semibold text-md-on-surface">{text.title}</h2>
          <p className="mt-1 text-sm text-md-on-surface-variant">{text.description}</p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            id="project-create-slug"
            label={text.slugLabel}
            required
            autoComplete="off"
            value={form.slug}
            onChange={(event) => form.setSlug(event.target.value)}
            hint={text.slugHint}
            error={fieldErrorText(form.fieldErrors.slug, PROJECT_SLUG_MAX_LENGTH, text.errors)}
          />
          <TextField
            id="project-create-display-name"
            label={text.displayNameLabel}
            required
            autoComplete="off"
            value={form.displayName}
            onChange={(event) => form.setDisplayName(event.target.value)}
            error={fieldErrorText(form.fieldErrors.displayName, PROJECT_DISPLAY_NAME_MAX_LENGTH, text.errors)}
          />
          <div className="sm:col-span-2">
            <TextAreaField
              id="project-create-description"
              label={text.descriptionLabel}
              rows={3}
              value={form.description}
              onChange={(event) => form.setDescription(event.target.value)}
            />
          </div>
        </div>
        <div className="flex justify-end border-t border-md-outline-variant pt-4">
          <Button variant="lead" type="submit" loading={form.submitting}>
            {form.submitting ? text.submitting : text.submit}
          </Button>
        </div>
      </form>
    </>
  );
}
