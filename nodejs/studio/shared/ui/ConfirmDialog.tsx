"use client";

import { useEffect, useState, type ReactNode } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { Button, type ButtonVariant } from "./Button";
import { Dialog } from "./Dialog";
import { TextField } from "./TextField";

type ConfirmDialogProps = Readonly<{
  /** 開いているか。 */
  open: boolean;
  /** 見出し（例: 定義「invoice-sync」を削除しますか？）。 */
  title: string;
  /** 取り消せるかどうか・影響の範囲などの説明。 */
  message: ReactNode;
  /** 決定のボタンの文言（例: 削除する）。 */
  confirmLabel: string;
  /** キャンセルのボタンの文言。省略すると共通の「キャンセル」。 */
  cancelLabel?: string;
  /** 決定のボタンの役割。破壊的な操作は既定の `dangerSolid`。 */
  confirmVariant?: Extract<ButtonVariant, "dangerSolid" | "lead" | "standard">;
  /**
   * 指定すると、この文字列を入力するまで決定できない。
   * 影響の大きい削除（定義・プロジェクト・グループ）だけに使う（studio-ui-visual-refresh の U6）。
   */
  requireTypedName?: string;
  /** 送信中か。true のあいだは閉じられず、決定のボタンがスピナーになる。 */
  loading?: boolean;
  /** 決定したとき。 */
  onConfirm: () => void;
  /** キャンセル・Esc・閉じるのとき。 */
  onCancel: () => void;
}>;

/**
 * 破壊的な操作の前に出す共通の確認ダイアログ。行の中で確認する作りやブラウザの `confirm()` は使わない。
 * 閉じたときは何も送らない。
 */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel,
  confirmVariant = "dangerSolid",
  requireTypedName,
  loading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const uiText = useUiText();
  const [typedName, setTypedName] = useState("");

  useEffect(() => {
    if (open) setTypedName("");
  }, [open]);

  const nameMatches = requireTypedName === undefined || typedName === requireTypedName;

  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title={title}
      description={message}
      dismissible={!loading}
      footer={
        <>
          <Button variant="outline" onClick={onCancel} disabled={loading}>
            {cancelLabel ?? uiText.actions.cancel}
          </Button>
          <Button variant={confirmVariant} onClick={onConfirm} disabled={!nameMatches} loading={loading}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      {requireTypedName === undefined ? null : (
        <TextField
          label={uiText.dialog.typeNameToConfirm(requireTypedName)}
          value={typedName}
          onChange={(event) => setTypedName(event.target.value)}
          autoComplete="off"
          spellCheck={false}
          className="font-mono"
        />
      )}
    </Dialog>
  );
}
