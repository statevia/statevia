"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Notice } from "@/shared/ui/Notice";

type ReplayBannerProps = {
  onBackToCurrent: () => void;
};

/** リプレイ中であることを示すお知らせ。今の状態へ戻る操作を右に置く。 */
export function ReplayBanner({ onBackToCurrent }: Readonly<ReplayBannerProps>) {
  const uiText = useUiText();
  return (
    <Notice
      tone="info"
      title={uiText.executionTimeline.replayingPastStateMessage}
      actions={
        <Button variant="outline" size="sm" onClick={onBackToCurrent}>
          {uiText.executionTimeline.backToCurrent}
        </Button>
      }
    />
  );
}
