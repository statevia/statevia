"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Notice } from "@/shared/ui/Notice";

type ExecutionStatusBannerProps = {
  cancelRequested?: boolean;
  terminal?: boolean;
};

/** 実行状態を要約表示するお知らせ。キャンセル要求済みはエラー、終了済みは情報の色で出す。 */
export function ExecutionStatusBanner({ cancelRequested, terminal }: Readonly<ExecutionStatusBannerProps>) {
  const uiText = useUiText();
  if (cancelRequested) {
    return (
      <Notice tone="error" title={uiText.executionStatusBanner.cancelRequestedNotice(uiText.actions.cancel, uiText.actions.resume)} />
    );
  }
  if (terminal) {
    return <Notice tone="info" title={uiText.executionStatusBanner.terminalNotice(uiText.entities.execution)} />;
  }
  return null;
}
