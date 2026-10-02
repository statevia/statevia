"use client";

import { useParams, useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { startExecutionFromDefinition } from "@/features/definitions/api";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { getUtf8ByteLength } from "@/shared/lib/validation/primitives";
import { START_INPUT_MAX_BYTES } from "@/shared/lib/validation/formRules";
import { ActionInputCodeEditor } from "@/shared/ui/ActionInputCodeEditor";
import { NAVIGATION_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { Toast } from "@/shared/ui/Toast";

/**
 * Definition 起点で新規実行を開始する。
 * 開始成功時は `/executions/[executionId]/run` へ遷移する。
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
      router.push(`/executions/${encodeURIComponent(created.displayId)}/run`);
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setStarting(false);
    }
  };

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-5 p-6">
      <header className="space-y-1">
        <h1 className="text-xl font-semibold text-md-on-surface">
          {uiText.definitionRunPage.title}
        </h1>
        <p className="text-sm text-md-on-surface-variant">
          <span className="font-mono break-all">
            {uiText.definitionRunPage.definitionIdLine(
              uiText.labels.definitionId,
              definitionId || uiText.definitionRunPage.unspecifiedDefinitionId,
            )}
          </span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      <section className="space-y-3 rounded-lg border border-md-outline bg-md-surface p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-md-on-surface-variant">
            {uiText.definitionRunPage.inputLabelWithHint(uiText.labels.input)}
          </span>
          <ActionInputCodeEditor
            value={inputJson}
            onChange={setInputJson}
            placeholder={uiText.definitionRunPage.inputJsonPlaceholder}
            syntaxHighlight="jsonOnly"
            ariaLabel={uiText.labels.input}
            className="min-h-[7rem] w-full border-md-outline-variant bg-md-surface-container"
          />
        </label>
        <button
          type="button"
          className="rounded border-2 border-brand-cta-border bg-brand-cta-bg px-3 py-1.5 text-sm text-brand-cta-fg hover:bg-brand-cta-bg-hover disabled:opacity-50"
          onClick={() => void handleStart()}
          disabled={starting || !definitionId.trim()}
        >
          {starting
            ? uiText.definitionRunPage.actions.starting
            : uiText.definitionRunPage.actions.startExecution}
        </button>
        <p className="text-xs text-md-on-surface-variant">
          {uiText.definitionRunPage.help.redirectAfterStart("/executions/[executionId]/run")}
        </p>
      </section>

      <nav className="flex flex-wrap gap-3 text-sm">
        <button
          type="button"
          className={NAVIGATION_BUTTON_CLASS}
          onClick={() => router.push(`/definitions/${encodeURIComponent(definitionId)}`)}
        >
          {uiText.definitionRunPage.nav.backToDefinitionDetail}
        </button>
        <button type="button" className={NAVIGATION_BUTTON_CLASS} onClick={() => router.push("/executions")}>
          {uiText.lists.executions}
        </button>
      </nav>
    </main>
  );
}
