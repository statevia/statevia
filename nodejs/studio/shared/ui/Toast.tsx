"use client";

import type { ToastState } from "@/shared/lib/errors";
import { useUiText } from "@/shared/i18n/uiTextContext";

type ToastProps = {
  toast: ToastState | null;
  onClose: () => void;
};

const TOAST_TONE_CLASS_MAP: Record<ToastState["tone"], string> = {
  error: "border-red-200 bg-red-50 text-red-900",
  success: "border-emerald-200 bg-emerald-50 text-emerald-900",
  info: "border-md-outline bg-md-surface text-md-on-surface"
};

function getToneClass(tone: ToastState["tone"]): string {
  return TOAST_TONE_CLASS_MAP[tone];
}

/** 画面上部に一時メッセージを表示するトースト。 */
export function Toast({ toast, onClose }: Readonly<ToastProps>) {
  const uiText = useUiText();
  if (!toast) return null;

  const toneClass = getToneClass(toast.tone);

  return (
    <div
      className={`rounded-2xl border px-4 py-3 text-sm ${toneClass}`}
    >
      <div className="flex items-start justify-between gap-4">
        <output className="flex-1" aria-live="polite" aria-atomic="true">
          {toast.message}
        </output>
        <button type="button" className="text-md-on-surface-variant hover:text-md-on-surface" onClick={onClose} aria-label={uiText.actions.closeToast}>
          ✕
        </button>
      </div>
    </div>
  );
}

