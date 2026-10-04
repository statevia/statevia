"use client";

import { DefinitionGraphEditor } from "./DefinitionGraphEditor";
import { YamlCodeEditor } from "./YamlCodeEditor";
import { ActionLinkGroup } from "@/shared/ui/ActionLinkGroup";
import { NAVIGATION_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useDefinitionEditorPage } from "../hooks/useDefinitionEditorPage";

type DefinitionEditorPageClientProps = {
  /** 編集対象。無いときは新規作成。 */
  definitionId?: string;
};

/**
 * Definition 専用エディタ。`definitionId` があるとき PUT、ないとき POST で保存する。
 * @param props 編集対象の定義 ID。
 * @returns 定義エディタ。
 */
export function DefinitionEditorPageClient({ definitionId }: Readonly<DefinitionEditorPageClientProps>) {
  const uiText = useUiText();
  const editor = useDefinitionEditorPage({ definitionId });

  return (
    <PageShell
      title={uiText.labels.definitionEditor}
      description={
        editor.isCreateMode || !definitionId
          ? uiText.definitionEditor.descriptionCreating
          : uiText.definitionEditor.descriptionEditingTarget(definitionId)
      }
      primaryActions={<ActionLinkGroup links={editor.actionLinks} />}
      className="max-w-[1600px]"
    >
      <Toast toast={editor.toast} onClose={editor.dismissToast} />

      {editor.loadingMeta && (
        <PageState state="loading" message={uiText.definitionEditor.loadingMeta} />
      )}

      <section className="space-y-3 rounded-lg border border-md-outline bg-md-surface p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-md-on-surface-variant">{uiText.definitionEditor.labels.name}</span>
          <input
            className="mt-1 w-full rounded border border-md-outline-variant bg-md-surface-container px-2 py-1.5 text-sm text-md-on-surface"
            value={editor.definitionName}
            onChange={(event) => editor.setDefinitionName(event.target.value)}
            autoComplete="off"
          />
        </label>
        {editor.apiNameMessages.length > 0 && (
          <p className="text-xs text-rose-600">{editor.apiNameMessages[0]}</p>
        )}

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className={`rounded border px-3 py-1 text-xs ${editor.editorMode === "yaml" ? "border-brand-cta-border bg-brand-cta-bg text-brand-cta-fg" : "border-md-outline-variant bg-md-surface-container text-md-on-surface"}`}
            onClick={editor.showYaml}
          >
            {uiText.definitionEditor.actions.switchToYaml}
          </button>
          <button
            type="button"
            className={`rounded border px-3 py-1 text-xs ${editor.editorMode === "graph" ? "border-brand-cta-border bg-brand-cta-bg text-brand-cta-fg" : "border-md-outline-variant bg-md-surface-container text-md-on-surface"}`}
            onClick={editor.showGraph}
          >
            {uiText.definitionEditor.actions.switchToGraph}
          </button>
        </div>

        {editor.editorMode === "yaml" ? (
          <label className="block text-sm">
            <span className="text-md-on-surface-variant">{uiText.definitionEditor.labels.yaml}</span>
            <YamlCodeEditor
              value={editor.yaml}
              onChange={editor.onYamlChange}
              completionKeywords={editor.completionKeywords}
              onLintChange={editor.onYamlLintChange}
              onDiagnosticsChange={editor.onYamlDiagnosticsChange}
            />
          </label>
        ) : (
          <>
            {editor.yamlParseMessages.length > 0 && (
              <p className="text-xs text-amber-700">{uiText.definitionEditor.graph.parseFailed}</p>
            )}
            <DefinitionGraphEditor
              document={editor.graphDocument}
              onDocumentChange={editor.onGraphDocumentChange}
              validationMessages={editor.graphHintMessages}
              actionValidationDetails={editor.actionValidationDetails}
              labels={uiText.definitionEditor.graph}
            />
          </>
        )}
        {editor.hasYamlError && <p className="text-xs text-rose-600">{uiText.definitionEditor.validation.yamlLintInvalid}</p>}
        {editor.hintMessages.length > 0 && (
          <section className="rounded border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
            <p className="font-medium">{uiText.definitionEditor.hints.title}</p>
            <ul className="mt-2 list-inside list-disc space-y-1">
              {editor.hintMessages.slice(0, 8).map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          </section>
        )}

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="w-full rounded border-2 border-brand-cta-border bg-brand-cta-bg px-3 py-1.5 text-sm text-brand-cta-fg hover:bg-brand-cta-bg-hover disabled:opacity-50 sm:w-auto"
            onClick={() => void editor.save()}
            disabled={editor.saving || editor.hasYamlError}
          >
            {editor.saving ? uiText.definitionEditor.actions.saving : uiText.definitionEditor.actions.saveWithApiHint}
          </button>
          <button
            type="button"
            className="w-full rounded border border-md-outline-variant bg-md-surface-container px-3 py-1.5 text-sm text-md-on-surface hover:bg-md-surface-container-high sm:ml-auto sm:w-auto"
            onClick={editor.resetToInitial}
            disabled={editor.saving || !editor.canResetToInitial}
          >
            {uiText.definitionEditor.actions.resetTemplate}
          </button>
        </div>
      </section>

      {editor.savedDefinition && (
        <section className="space-y-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-950">
          <p className="font-medium">{uiText.definitionEditor.saved.complete(editor.savedDefinition.displayId)}</p>
          <div className="flex flex-wrap gap-3">
            <button
              type="button"
              className={NAVIGATION_BUTTON_CLASS}
              onClick={editor.openSavedDetail}
            >
              {uiText.definitionEditor.saved.openNewDetail}
            </button>
            <button
              type="button"
              className={NAVIGATION_BUTTON_CLASS}
              onClick={editor.openSavedRun}
            >
              {uiText.definitionEditor.saved.runWithThisDefinition}
            </button>
          </div>
        </section>
      )}
    </PageShell>
  );
}
