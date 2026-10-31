"use client";

import { useState } from "react";
import type { ProjectSummary } from "@/shared/project/types";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useResourceGrantsEditor, type ResourceGrantsEditorModel } from "../hooks/useResourceGrantsEditor";
import {
  MAX_GRANTED_DEFINITIONS,
  MAX_GRANTED_PROJECTS,
  definitionsOutsideSelectedProjects,
  groupDefinitionCandidates,
  idsOutsideCandidates,
  type ResourceGrantDefinitionCandidate,
  type ResourceGrantField,
  type ResourceGrantSubject
} from "../resourceGrants";

type ResourceGrantText = ReturnType<typeof useUiText>["admin"]["resourceGrants"];

type ResourceGrantsFormProps = {
  /** 対象の主体。参照が変わると読み直すため、呼び出し側で安定させる。 */
  subject: ResourceGrantSubject;
};

type SectionProps = {
  /** 編集状態。 */
  editor: ResourceGrantsEditorModel;
};

type DefinitionsSectionProps = SectionProps & {
  /** 定義候補。 */
  definitionCandidates: ResourceGrantDefinitionCandidate[];
  /** 名前解決に使う Project 候補。 */
  projectCandidates: ProjectSummary[];
};

const SECONDARY_BUTTON_CLASS =
  "rounded-lg border border-md-outline px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container disabled:opacity-60";
const PRIMARY_BUTTON_CLASS =
  "rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60";

/**
 * 種別の上限超過と、その理由。
 * @param props 文言と上限。
 * @returns エラー表示。
 */
function LimitError({ id, message, text }: Readonly<{ id: string; message: string; text: ResourceGrantText }>) {
  return (
    <p id={id} className="mt-2 text-xs text-md-error" role="alert">
      {message} {text.limitReason}
    </p>
  );
}

/**
 * 付与済み project の一覧と、候補からの追加。GUID の手入力は置かない。
 * 一覧に無い付与済み ID は GUID のまま出し、外せるようにする。
 * @param props 編集状態と Project 候補。
 * @returns project の欄。
 */
function ProjectsSection({ editor, projectCandidates }: Readonly<SectionProps & { projectCandidates: ProjectSummary[] }>) {
  const text = useUiText().admin.resourceGrants;
  const [projectToAdd, setProjectToAdd] = useState("");
  const selectedProjects = projectCandidates.filter((project) => editor.selectedProjectIds.has(project.projectId));
  const unknownProjectIds = idsOutsideCandidates(
    editor.selectedProjectIds,
    projectCandidates.map((project) => project.projectId)
  );
  const addable = projectCandidates.filter((project) => !editor.selectedProjectIds.has(project.projectId));
  const errorId = "resource-grants-projects-error";

  /** 選んだ候補を追加し、選択欄を空に戻す。 */
  const addProject = () => {
    if (projectToAdd === "" || editor.selectedProjectIds.has(projectToAdd)) return;
    editor.toggleProject(projectToAdd);
    setProjectToAdd("");
  };

  return (
    <fieldset className="mb-6" aria-describedby={editor.limitErrors.projectIds === undefined ? undefined : errorId}>
      <legend className="mb-1 flex w-full items-baseline justify-between text-base font-medium">
        <span>{text.projectsTitle}</span>
        <span className="text-xs font-normal text-md-on-surface-variant">
          {text.projectCount(editor.selectedProjectIds.size, MAX_GRANTED_PROJECTS)}
        </span>
      </legend>
      <p className="mb-2 text-xs text-md-on-surface-variant">{text.projectsMembershipNote}</p>
      {editor.selectedProjectIds.size === 0 ? (
        <p className="mb-2 text-sm text-md-on-surface-variant">{text.projectsEmpty}</p>
      ) : (
        <ul className="mb-2 divide-y divide-md-outline rounded-lg border border-md-outline">
          {selectedProjects.map((project) => (
            <li key={project.projectId} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
              <span>
                {project.displayName}{" "}
                <span className="font-mono text-xs text-md-on-surface-variant">{project.slug}</span>
              </span>
              <button
                type="button"
                onClick={() => editor.toggleProject(project.projectId)}
                aria-label={text.removeNamed(project.displayName)}
                className={SECONDARY_BUTTON_CLASS}
              >
                {text.remove}
              </button>
            </li>
          ))}
          {unknownProjectIds.map((projectId) => (
            <li key={projectId} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
              <span>
                {text.unknownProject} <span className="font-mono text-xs">{projectId}</span>
              </span>
              <button
                type="button"
                onClick={() => editor.toggleProject(projectId)}
                aria-label={text.removeNamed(projectId)}
                className={SECONDARY_BUTTON_CLASS}
              >
                {text.remove}
              </button>
            </li>
          ))}
        </ul>
      )}
      {addable.length === 0 ? (
        <p className="text-xs text-md-on-surface-variant">{text.noMoreProjects}</p>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <label htmlFor="resource-grants-project-add" className="text-sm">
            {text.projectAddLabel}
          </label>
          <select
            id="resource-grants-project-add"
            value={projectToAdd}
            onChange={(event) => setProjectToAdd(event.target.value)}
            className="rounded-lg border border-md-outline px-3 py-1.5 text-sm"
          >
            <option value="">{text.projectAddPlaceholder}</option>
            {addable.map((project) => (
              <option key={project.projectId} value={project.projectId}>
                {project.displayName} ({project.slug})
              </option>
            ))}
          </select>
          <button type="button" onClick={addProject} disabled={projectToAdd === ""} className={SECONDARY_BUTTON_CLASS}>
            {text.projectAddSubmit}
          </button>
        </div>
      )}
      {editor.limitErrors.projectIds === undefined ? null : (
        <LimitError id={errorId} message={text.projectsTooMany(MAX_GRANTED_PROJECTS)} text={text} />
      )}
    </fieldset>
  );
}

/**
 * 定義の選択。候補は Project ごとにまとめ、候補に無い付与済み ID は GUID で残す。
 * 選んだ project の外にある定義は許可されないため注意を出す（送信は止めない）。
 * @param props 編集状態と候補。
 * @returns 定義の欄。
 */
function DefinitionsSection({ editor, definitionCandidates, projectCandidates }: Readonly<DefinitionsSectionProps>) {
  const text = useUiText().admin.resourceGrants;
  const groups = groupDefinitionCandidates(definitionCandidates, projectCandidates);
  const unknownDefinitionIds = idsOutsideCandidates(
    editor.selectedDefinitionIds,
    definitionCandidates.map((definition) => definition.resourceId)
  );
  const outside = definitionsOutsideSelectedProjects(
    { projectIds: editor.selectedProjectIds, definitionIds: editor.selectedDefinitionIds },
    definitionCandidates
  );
  const errorId = "resource-grants-definitions-error";

  return (
    <fieldset className="mb-6" aria-describedby={editor.limitErrors.definitionIds === undefined ? undefined : errorId}>
      <legend className="mb-1 flex w-full items-baseline justify-between text-base font-medium">
        <span>{text.definitionsTitle}</span>
        <span className="text-xs font-normal text-md-on-surface-variant">
          {text.definitionCount(editor.selectedDefinitionIds.size, MAX_GRANTED_DEFINITIONS)}
        </span>
      </legend>
      <p className="text-xs text-md-on-surface-variant">{text.definitionsScopeNote}</p>
      {editor.definitionCandidatesTruncated ? (
        <p className="text-xs text-md-on-surface-variant">{text.definitionsTruncated}</p>
      ) : null}
      {editor.selectedDefinitionIds.size === 0 ? (
        <p className="mt-2 text-sm text-md-on-surface-variant">{text.definitionsEmpty}</p>
      ) : null}
      {groups.length === 0 ? (
        <p className="mt-2 text-sm text-md-on-surface-variant">{text.noDefinitionCandidates}</p>
      ) : (
        groups.map((group) => (
          <div key={group.projectId ?? "unassigned"} className="mt-3">
            <h3 className="mb-1 text-sm font-medium">{group.projectName ?? text.definitionsUnassigned}</h3>
            <div className="grid gap-1 sm:grid-cols-2">
              {group.definitions.map((definition) => (
                <label key={definition.resourceId} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={editor.selectedDefinitionIds.has(definition.resourceId)}
                    onChange={() => editor.toggleDefinition(definition.resourceId)}
                  />
                  {definition.name}
                  <span className="font-mono text-xs text-md-on-surface-variant">{definition.displayId}</span>
                </label>
              ))}
            </div>
          </div>
        ))
      )}
      {unknownDefinitionIds.length === 0 ? null : (
        <div className="mt-3">
          <h3 className="mb-1 text-sm font-medium">{text.unknownDefinitionsTitle}</h3>
          {unknownDefinitionIds.map((definitionId) => (
            <label key={definitionId} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={editor.selectedDefinitionIds.has(definitionId)}
                onChange={() => editor.toggleDefinition(definitionId)}
              />
              <span className="font-mono text-xs">{definitionId}</span>
            </label>
          ))}
        </div>
      )}
      {outside.length === 0 ? null : (
        <output className="mt-3 block rounded-lg border border-md-warning bg-md-warning-container p-3 text-sm text-md-on-warning-container">
          <p>{text.outsideProjectsWarning}</p>
          <ul className="mt-1 list-disc pl-5">
            {outside.map((definition) => (
              <li key={definition.resourceId}>{definition.name}</li>
            ))}
          </ul>
        </output>
      )}
      {editor.limitErrors.definitionIds === undefined ? null : (
        <LimitError id={errorId} message={text.definitionsTooMany(MAX_GRANTED_DEFINITIONS)} text={text} />
      )}
    </fieldset>
  );
}

/**
 * 空にする種別の確認。空配列は禁止ではなく、所属の範囲で追加制限を消すことを伝える。
 * @param props 編集状態と、空になる種別。
 * @returns 確認ダイアログ。
 */
function ClearConfirmDialog({ editor, fields }: Readonly<SectionProps & { fields: ResourceGrantField[] }>) {
  const text = useUiText().admin.resourceGrants;
  const fieldLabels: Record<ResourceGrantField, string> = {
    projectIds: text.projectsTitle,
    definitionIds: text.definitionsTitle
  };

  return (
    <dialog
      open
      aria-labelledby="resource-grants-clear-title"
      className="mt-4 w-full max-w-none rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs"
    >
      <h2 id="resource-grants-clear-title" className="mb-2 text-lg font-medium">
        {text.clearConfirmTitle}
      </h2>
      <p className="mb-4 text-sm">{text.clearConfirmBody(fields.map((field) => fieldLabels[field]))}</p>
      <div className="flex gap-2">
        <button
          type="button"
          disabled={editor.saving}
          onClick={() => void editor.confirmClear()}
          className={PRIMARY_BUTTON_CLASS}
        >
          {text.clearConfirm}
        </button>
        <button type="button" disabled={editor.saving} onClick={editor.cancelClear} className={SECONDARY_BUTTON_CLASS}>
          {text.cancel}
        </button>
      </div>
    </dialog>
  );
}

/**
 * ユーザーと ServiceAccount で共通の、実行リソース許可の編集フォーム。
 * PUT は常に project と定義の両配列を送る。候補を読めない間は保存させない。
 * 付与先の所属は確かめず、保存時の 422 を失敗として出す。外枠（PageShell）は route 側が持つ。
 * @param props 対象の主体。
 * @returns 許可の編集フォーム、または読込・失敗の表示。
 */
export function ResourceGrantsForm({ subject }: Readonly<ResourceGrantsFormProps>) {
  const text = useUiText().admin.resourceGrants;
  const editor = useResourceGrantsEditor(subject);

  switch (editor.loadState) {
    case "loading":
      return <PageState state="loading" />;
    case "notFound":
      return <PageState state="error" message={text.notFound} />;
    case "error":
      return (
        <PageState
          state="error"
          message={text.loadFailed}
          retryLabel={text.retry}
          onRetry={() => void editor.reload()}
        />
      );
    case "ready":
      break;
  }

  return (
    <>
      <Toast toast={editor.toast} onClose={editor.clearToast} />
      <p className="mb-4 text-sm text-md-on-surface-variant">{text.description}</p>
      <form
        noValidate
        onSubmit={(event) => void editor.submit(event)}
        className="rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs"
      >
        {editor.submitError === null ? null : (
          <p className="mb-3 text-sm text-md-error" role="alert">
            {editor.submitError}
          </p>
        )}
        {editor.candidatesLoadFailed ? (
          <p className="mb-3 text-sm text-md-error" role="alert">
            {text.candidatesLoadFailed}
          </p>
        ) : null}
        {editor.projectCandidates === null ? null : (
          <ProjectsSection editor={editor} projectCandidates={editor.projectCandidates} />
        )}
        {editor.projectCandidates === null || editor.definitionCandidates === null ? null : (
          <DefinitionsSection
            editor={editor}
            definitionCandidates={editor.definitionCandidates}
            projectCandidates={editor.projectCandidates}
          />
        )}
        <button type="submit" disabled={!editor.canSave} className={PRIMARY_BUTTON_CLASS}>
          {editor.saving ? text.saving : text.saveSubmit}
        </button>
      </form>
      {editor.pendingClearFields === null ? null : (
        <ClearConfirmDialog editor={editor} fields={editor.pendingClearFields} />
      )}
    </>
  );
}
