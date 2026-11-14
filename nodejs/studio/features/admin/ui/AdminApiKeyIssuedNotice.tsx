"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { Notice } from "@/shared/ui/Notice";
import type { CreatedAdminApiKey } from "../types";

/** 発行直後の平文キー表示。 */
export type AdminApiKeyIssuedNoticeProps = {
  /** 発行応答。平文はこの表示を閉じるまでだけ保持する。 */
  issuedKey: CreatedAdminApiKey;
  /** 平文をコピー済みか。 */
  copied: boolean;
  /** 平文をコピーする。 */
  onCopy: () => void;
  /** この表示を閉じる。 */
  onDismiss: () => void;
};

/**
 * 発行した API キーの平文を一度だけ見せるダイアログ。平文は画面に常駐させない（要件 9.3）。
 * 再表示できない旨の注意を添え、コピーしてから閉じられるようにする。
 * @param props 平文と、コピー・閉じるコマンド。
 * @returns 発行直後のダイアログ。
 */
export function AdminApiKeyIssuedNotice({
  issuedKey,
  copied,
  onCopy,
  onDismiss
}: Readonly<AdminApiKeyIssuedNoticeProps>) {
  const uiText = useUiText();
  const text = uiText.admin.apiKeys;

  return (
    <Dialog
      open
      onClose={onDismiss}
      title={text.issuedTitle}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onCopy}>
            {copied ? text.copiedKey : text.copyKey}
          </Button>
          <Button variant="standard" onClick={onDismiss}>
            {text.dismissIssued}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <Notice tone="warning" title={text.issuedWarning} />
        <label className="block">
          <span className="text-sm font-medium text-md-on-surface">{text.plainKeyLabel}</span>
          <input
            id="issued-plain-key"
            readOnly
            value={issuedKey.plainKey}
            className="mt-1.5 h-9 w-full rounded-control border border-md-outline-variant bg-md-surface-container px-3 font-mono text-xs text-md-on-surface"
          />
        </label>
      </div>
    </Dialog>
  );
}
