"use client";

import { Download, Upload } from "lucide-react";
import { useRef } from "react";
import { DefinitionGraphEditor } from "./DefinitionGraphEditor";
import { YamlCodeEditor } from "./YamlCodeEditor";
import { Breadcrumbs, type BreadcrumbItem } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { FieldLabel } from "@/shared/ui/FormField";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import { TextField } from "@/shared/ui/TextField";
import { Toast } from "@/shared/ui/Toast";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useDefinitionEditorPage, type DefinitionEditorMode } from "../hooks/useDefinitionEditorPage";
import { useProject } from "@/shared/project/projectContext";
import { canPublish } from "@/shared/project/roles";
import { DEFINITIONS_WRITE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";

type DefinitionEditorPageClientProps = {
  /** 編集対象。無いときは新規作成。 */
  definitionId?: string;
};

/** 構文ヒントを一度に出す上限。多すぎると本文が押し下げられるため、先頭から数件だけ出す。 */
const HINT_MESSAGES_LIMIT = 8;

/**
 * Definition 専用エディタ。`definitionId` があるとき PUT、ないとき POST で保存する。
 * 戻り導線はパンくずにまとめ、保存（緑の主導線）と「編集前に戻す」は画面下の固定の操作バーに置く（モック 04）。
 * 未保存の変更があるときに YAML を読み込むと、確認ダイアログを経てから上書きする。
 * @param props 編集対象の定義 ID。
 * @returns 定義エディタ。
 */
export function DefinitionEditorPageClient({ definitionId }: Readonly<DefinitionEditorPageClientProps>) {
  const uiText = useUiText();
  const text = uiText.definitionEditor;
  const { selectedProject, roleOf, loaded: projectsLoaded } = useProject();
  const permissions = usePermissions();
  const editor = useDefinitionEditorPage({
    definitionId,
    projectId: selectedProject?.projectId,
  });
  const yamlFileInputRef = useRef<HTMLInputElement>(null);

  const createBlocked = editor.isCreateMode && projectsLoaded && !canPublish(selectedProject?.role);
  const editBlocked = !editor.isCreateMode
    && editor.loadedProjectId !== null
    && projectsLoaded
    && !canPublish(roleOf(editor.loadedProjectId));
  // 保存 API はロールに加えてグループ権限 definitions.write を要求する。
  const permissionBlocked = permissions.loaded && !permissions.has(DEFINITIONS_WRITE_PERMISSION);
  const blockedMessage = (() => {
    if (editor.accessDenied) return text.notMember;
    if (createBlocked || editBlocked) return text.roleDenied;
    if (permissionBlocked) return text.permissionDenied;
    return null;
  })();

  const breadcrumbItems: BreadcrumbItem[] = [
    { label: uiText.navigation.groups.workflow },
    { label: uiText.lists.definitions, href: "/definitions" },
    ...(definitionId ? [{ label: definitionId, href: `/definitions/${encodeURIComponent(definitionId)}` }] : []),
    { label: uiText.labels.definitionEditor },
  ];

  const stickyFooter = blockedMessage ? undefined : (
    <>
      <span className="flex items-center gap-2 text-sm text-md-on-surface-variant">
        {editor.canResetToInitial ? (
          <>
            <span aria-hidden="true" className="h-2 w-2 rounded-pill bg-md-warning" />
            {text.actions.unsavedChanges}
          </>
        ) : null}
      </span>
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="outline" onClick={editor.resetToInitial} disabled={editor.saving || !editor.canResetToInitial}>
          {text.actions.resetTemplate}
        </Button>
        <Button
          variant="lead"
          onClick={() => void editor.save()}
          loading={editor.saving}
          disabled={editor.hasYamlError}
        >
          {editor.saving ? text.actions.saving : text.actions.saveWithApiHint}
        </Button>
      </div>
    </>
  );

  return (
    <PageShell
      title={uiText.labels.definitionEditor}
      description={
        editor.isCreateMode || !definitionId
          ? text.descriptionCreating
          : text.descriptionEditingTarget(definitionId)
      }
      breadcrumbs={<Breadcrumbs items={breadcrumbItems} />}
      stickyFooter={stickyFooter}
      className="max-w-[1600px]"
    >
      <Toast toast={editor.toast} onClose={editor.dismissToast} />

      {editor.loadingMeta && <PageState state="loading" message={text.loadingMeta} />}

      {blockedMessage && <Notice tone="info" title={blockedMessage} />}

      {editor.savedDefinition && (
        <Notice
          tone="success"
          title={text.saved.complete(editor.savedDefinition.displayId)}
          actions={
            <>
              <Button variant="outline" size="sm" onClick={editor.openSavedDetail}>
                {text.saved.openNewDetail}
              </Button>
              <Button variant="outline" size="sm" onClick={editor.openSavedRun}>
                {text.saved.runWithThisDefinition}
              </Button>
            </>
          }
        />
      )}

      {!blockedMessage && (
        <section className="flex flex-col gap-4 rounded-card border border-md-outline bg-md-surface p-5">
          <TextField
            label={text.labels.name}
            required
            value={editor.definitionName}
            onChange={(event) => editor.setDefinitionName(event.target.value)}
            autoComplete="off"
            error={editor.apiNameMessages[0]}
          />

          <div className="flex flex-wrap items-center gap-2">
            <SegmentedControl<DefinitionEditorMode>
              ariaLabel={text.actions.editorModeLabel}
              options={[
                { value: "yaml", label: text.actions.switchToYaml },
                { value: "graph", label: text.actions.switchToGraph },
              ]}
              value={editor.editorMode}
              onChange={(mode) => (mode === "yaml" ? editor.showYaml() : editor.showGraph())}
            />
            <div className="ml-auto flex flex-wrap items-center gap-2">
              <Button variant="outline" size="sm" onClick={() => yamlFileInputRef.current?.click()}>
                <Upload aria-hidden="true" className="h-3.5 w-3.5" />
                {text.actions.uploadYaml}
              </Button>
              <Button variant="outline" size="sm" onClick={editor.downloadYaml}>
                <Download aria-hidden="true" className="h-3.5 w-3.5" />
                {text.actions.downloadYaml}
              </Button>
              <input
                ref={yamlFileInputRef}
                type="file"
                accept=".yaml,.yml,text/yaml"
                className="hidden"
                aria-hidden
                tabIndex={-1}
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  event.target.value = "";
                  if (!file) {
                    return;
                  }
                  void editor.requestYamlUpload(file);
                }}
              />
            </div>
          </div>

          {editor.editorMode === "yaml" ? (
            <div className="text-sm">
              <FieldLabel label={text.labels.yaml} />
              <div className="mt-1.5">
                <YamlCodeEditor
                  value={editor.yaml}
                  onChange={editor.onYamlChange}
                  completionKeywords={editor.completionKeywords}
                  onLintChange={editor.onYamlLintChange}
                  onDiagnosticsChange={editor.onYamlDiagnosticsChange}
                  ariaLabel={text.labels.yamlEditor}
                />
              </div>
            </div>
          ) : (
            <>
              {editor.yamlParseMessages.length > 0 && <Notice tone="warning" title={text.graph.parseFailed} />}
              <DefinitionGraphEditor
                document={editor.graphDocument}
                onDocumentChange={editor.onGraphDocumentChange}
                validationMessages={editor.graphHintMessages}
                actionValidationDetails={editor.actionValidationDetails}
                labels={text.graph}
              />
            </>
          )}
          {editor.hasYamlError && <p className="text-xs text-md-error">{text.validation.yamlLintInvalid}</p>}
          {editor.hintMessages.length > 0 && (
            <Notice tone="warning" title={text.hints.title}>
              <ul className="list-inside list-disc space-y-1 text-xs">
                {editor.hintMessages.slice(0, HINT_MESSAGES_LIMIT).map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            </Notice>
          )}
        </section>
      )}

      <ConfirmDialog
        open={editor.yamlUploadConfirming}
        title={text.actions.overwriteConfirmTitle}
        message={text.actions.overwriteConfirmMessage}
        confirmLabel={text.actions.confirmOverwrite}
        cancelLabel={text.actions.cancelOverwrite}
        onConfirm={editor.confirmYamlOverwrite}
        onCancel={editor.cancelYamlOverwrite}
      />
    </PageShell>
  );
}
