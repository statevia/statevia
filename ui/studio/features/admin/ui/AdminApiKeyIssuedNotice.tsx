"use client";

import { useUiText } from "@/shared/i18n/uiTextContext";
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
 * 発行した API キーの平文を一度だけ見せる。
 * @param props 平文と、コピー・閉じるコマンド。
 * @returns 発行直後の通知。
 */
export function AdminApiKeyIssuedNotice({
  issuedKey,
  copied,
  onCopy,
  onDismiss
}: Readonly<AdminApiKeyIssuedNoticeProps>) {
  const uiText = useUiText();

  return (
    <section className="mb-6 rounded-xl border border-amber-500/60 bg-amber-50 p-4 text-sm text-amber-950 shadow-sm dark:bg-amber-950/30 dark:text-amber-100">
      <h2 className="mb-2 text-lg font-medium">{uiText.admin.apiKeys.issuedTitle}</h2>
      <p className="mb-3">{uiText.admin.apiKeys.issuedWarning}</p>
      <label htmlFor="issued-plain-key" className="mb-1 block font-medium">
        {uiText.admin.apiKeys.plainKeyLabel}
      </label>
      <div className="flex flex-wrap items-center gap-2">
        <input
          id="issued-plain-key"
          readOnly
          value={issuedKey.plainKey}
          className="min-w-0 flex-1 rounded-lg border border-md-outline bg-white px-3 py-2 font-mono text-xs dark:bg-md-surface"
        />
        <button
          type="button"
          onClick={onCopy}
          className="rounded-lg border border-md-outline px-3 py-2 hover:bg-md-surface-container"
        >
          {copied ? uiText.admin.apiKeys.copiedKey : uiText.admin.apiKeys.copyKey}
        </button>
        <button
          type="button"
          onClick={onDismiss}
          className="rounded-lg bg-emerald-700 px-3 py-2 text-white hover:bg-emerald-800"
        >
          {uiText.admin.apiKeys.dismissIssued}
        </button>
      </div>
    </section>
  );
}
