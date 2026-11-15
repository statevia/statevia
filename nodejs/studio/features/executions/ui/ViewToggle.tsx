"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";

/** 実行画面の表示モード（一覧 / グラフ）。 */
export type ViewMode = "list" | "graph";

type ViewToggleProps = {
  value: ViewMode;
  onChange: (mode: ViewMode) => void;
};

/** 一覧・グラフ表示を切り替えるトグル UI（共通の `SegmentedControl`）。 */
export function ViewToggle({ value, onChange }: Readonly<ViewToggleProps>) {
  const uiText = useUiText();
  return (
    <SegmentedControl<ViewMode>
      ariaLabel={uiText.executionDashboard.viewModeLabel}
      options={[
        { value: "list", label: uiText.actions.viewList },
        { value: "graph", label: uiText.actions.viewGraph }
      ]}
      value={value}
      onChange={onChange}
    />
  );
}
