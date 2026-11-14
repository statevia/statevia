"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { getDefinition, startExecutionFromDefinition } from "@/features/definitions/api";
import { isApiNotFound } from "@/shared/api/httpStatus";
import { useProject } from "@/shared/project/projectContext";
import { canExecute } from "@/shared/project/roles";
import { EXECUTIONS_WRITE_PERMISSION, usePermissions } from "@/shared/auth/permissionsContext";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { getUtf8ByteLength } from "@/shared/lib/validation/primitives";
import { START_INPUT_MAX_BYTES } from "@/shared/lib/validation/formRules";
import { ActionInputCodeEditor } from "@/shared/ui/ActionInputCodeEditor";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { FieldLabel } from "@/shared/ui/FormField";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { Toast } from "@/shared/ui/Toast";

/**
 * Definition 起点で新規実行を開始する。
 * 開始成功時は実行詳細（`/executions/[executionId]`）へ遷移する。開始のボタンは緑の主導線。定義の詳細へはパンくずで戻る。
 */
export function DefinitionRunStartPage() {
  const uiText = useUiText();
  const params = useParams();
  const router = useRouter();
  const definitionId = useMemo(() => {
    const raw = params.definitionId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.definitionId]);

  const [inputJson, setInputJson] = useState("");
  const [toast, setToast] = useState<ToastState | null>(null);
  const [starting, setStarting] = useState(false);
  const [access, setAccess] = useState<"loading" | "missing" | "denied" | "ready">("loading");
  const { roleOf, loaded: projectsLoaded } = useProject();
  const { has: hasPermission, loaded: permissionsLoaded } = usePermissions();
  const canWriteExecutions = hasPermission(EXECUTIONS_WRITE_PERMISSION);

  useEffect(() => {
    const id = definitionId.trim();
    if (!id || !projectsLoaded) {
      return;
    }
    let cancelled = false;
    void getDefinition(id)
      .then((row) => {
        if (cancelled) return;
        setAccess(canExecute(roleOf(row.projectId)) ? "ready" : "denied");
      })
      .catch((error: unknown) => {
        if (!cancelled) setAccess(isApiNotFound(error) ? "missing" : "denied");
      });
    return () => {
      cancelled = true;
    };
  }, [definitionId, projectsLoaded, roleOf]);

  const handleStart = async () => {
    const id = definitionId.trim();
    if (!id) {
      setToast({
        tone: "error",
        message: uiText.definitionRunPage.toasts.definitionIdRequired(uiText.labels.definitionId),
      });
      return;
    }

    const trimmedInputJson = inputJson.trim();
    let input: unknown;
    if (trimmedInputJson) {
      const inputBytes = getUtf8ByteLength(trimmedInputJson);
      if (inputBytes > START_INPUT_MAX_BYTES) {
        setToast({
          tone: "error",
          message: uiText.definitionRunPage.toasts.inputTooLarge,
        });
        return;
      }
      try {
        input = JSON.parse(trimmedInputJson) as unknown;
      } catch {
        setToast({
          tone: "error",
          message: uiText.definitionRunPage.toasts.invalidInputJson(uiText.labels.input),
        });
        return;
      }
    }

    setStarting(true);
    setToast(null);
    try {
      const created = await startExecutionFromDefinition(id, input);
      setToast({
        tone: "success",
        message: uiText.definitionRunPage.toasts.executionStarted(created.displayId),
      });
      router.push(`/executions/${encodeURIComponent(created.displayId)}`);
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setStarting(false);
    }
  };

  const encodedId = encodeURIComponent(definitionId);

  return (
    <PageShell
      title={uiText.definitionRunPage.title}
      description={uiText.definitionRunPage.definitionIdLine(
        uiText.labels.definitionId,
        definitionId || uiText.definitionRunPage.unspecifiedDefinitionId,
      )}
      breadcrumbs={
        <Breadcrumbs
          items={[
            { label: uiText.navigation.groups.workflow },
            { label: uiText.lists.definitions, href: "/definitions" },
            { label: definitionId, href: `/definitions/${encodedId}` },
            { label: uiText.definitionRunPage.title },
          ]}
        />
      }
      secondaryActions={
        <Link href="/executions" className="hover:underline">
          {uiText.lists.executions}
        </Link>
      }
    >
      <Toast toast={toast} onClose={() => setToast(null)} />

      {access === "missing" && <Notice tone="info" title={uiText.definitionRunPage.notMember} />}
      {access === "denied" && <Notice tone="info" title={uiText.definitionRunPage.roleDenied} />}
      {access === "ready" && permissionsLoaded && !canWriteExecutions && (
        <Notice tone="info" title={uiText.definitionRunPage.permissionDenied} />
      )}
      {access === "ready" && canWriteExecutions && (
        <section className="flex max-w-2xl flex-col gap-4 rounded-card border border-md-outline bg-md-surface p-5">
          <div className="text-sm">
            <FieldLabel label={uiText.definitionRunPage.inputLabelWithHint(uiText.labels.input)} />
            <ActionInputCodeEditor
              value={inputJson}
              onChange={setInputJson}
              placeholder={uiText.definitionRunPage.inputJsonPlaceholder}
              syntaxHighlight="jsonOnly"
              ariaLabel={uiText.labels.input}
              className="mt-1.5 min-h-28 w-full rounded-control border-md-outline-variant bg-md-surface-container"
            />
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-md-outline-variant pt-4">
            <p className="text-xs text-md-on-surface-variant">{uiText.definitionRunPage.help.redirectAfterStart}</p>
            <Button
              variant="lead"
              onClick={() => void handleStart()}
              loading={starting}
              disabled={!definitionId.trim()}
            >
              {starting ? uiText.definitionRunPage.actions.starting : uiText.definitionRunPage.actions.startExecution}
            </Button>
          </div>
        </section>
      )}
    </PageShell>
  );
}
