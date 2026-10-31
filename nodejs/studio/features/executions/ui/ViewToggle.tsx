"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";

/** 実行画面の表示モード（一覧 / グラフ）。 */
export type ViewMode = "list" | "graph";

type ViewToggleProps = {
  value: ViewMode;
  onChange: (mode: ViewMode) => void;
};

/** 一覧・グラフ表示を切り替えるトグル UI。 */
export function ViewToggle({ value, onChange }: Readonly<ViewToggleProps>) {
  const uiText = useUiText();
  return (
    <div className="inline-flex rounded-xl border border-md-outline-variant bg-md-surface-container p-1">
      <button
        type="button"
        className={`rounded-lg px-3 py-1.5 text-sm ${value === "list" ? "border-2 border-brand-cta-border bg-brand-cta-bg text-brand-cta-fg" : "text-md-on-surface hover:bg-md-surface-container-high"}`}
        onClick={() => onChange("list")}
      >
        {uiText.actions.viewList}
      </button>
      <button
        type="button"
        className={`rounded-lg px-3 py-1.5 text-sm ${value === "graph" ? "border-2 border-brand-cta-border bg-brand-cta-bg text-brand-cta-fg" : "text-md-on-surface hover:bg-md-surface-container-high"}`}
        onClick={() => onChange("graph")}
      >
        {uiText.actions.viewGraph}
      </button>
    </div>
  );
}

