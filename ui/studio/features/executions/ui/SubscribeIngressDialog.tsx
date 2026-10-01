"use client";

import { useState, type MouseEvent } from "react";
import { Button } from "@/shared/ui/Button";
import { TextField } from "@/shared/ui/TextField";
import {
  useSubscribeIngressDialog,
  type CandidateOption
} from "../hooks/useSubscribeIngressDialog";

type CandidateTextFieldProps = {
  label: string;
  value: string;
  options: readonly CandidateOption[];
  onChange: (value: string) => void;
};

/**
 * 入力の先頭一致で候補を絞る。空入力のときは候補をすべて出す。
 * @param options 表示候補。
 * @param value 現在の入力。
 * @returns リストに出す候補。
 */
function visibleCandidateOptions(options: readonly CandidateOption[], value: string): CandidateOption[] {
  const query = value.trim();
  return options.filter(
    (option) => query.length === 0 || option.value.startsWith(query) || option.label.startsWith(query)
  );
}

/**
 * topic / key の入力。候補は入力の直下に出す。
 * ダイアログ内の datalist は、ブラウザが候補を入力の右下へずらす。
 */
function CandidateTextField({ label, value, options, onChange }: Readonly<CandidateTextFieldProps>) {
  const [listOpen, setListOpen] = useState(false);
  const visibleOptions = visibleCandidateOptions(options, value);

  /**
   * 候補を選ぶ。blur より先に値を確定するため mousedown で受ける。
   * @param optionValue 入力へ入れる値。
   */
  function chooseOption(optionValue: string) {
    return (event: MouseEvent<HTMLButtonElement>) => {
      event.preventDefault();
      onChange(optionValue);
      setListOpen(false);
    };
  }

  return (
    <TextField
      label={label}
      value={value}
      className="font-mono"
      onChange={(event) => onChange(event.target.value)}
      onFocus={() => setListOpen(true)}
      onBlur={() => setListOpen(false)}
      autoComplete="off"
    >
      {listOpen && visibleOptions.length > 0 && (
        <ul className="absolute left-0 top-full z-10 mt-1 max-h-48 w-full overflow-auto rounded border border-md-outline-variant bg-md-surface-container shadow-lg">
          {visibleOptions.map((option) => (
            <li key={option.value || "empty"}>
              <button
                type="button"
                className="w-full px-2 py-1.5 text-left font-mono text-sm text-md-on-surface hover:bg-md-surface-container-high"
                onMouseDown={chooseOption(option.value)}
              >
                {option.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </TextField>
  );
}

/** 実行一覧の集合配送 POP。 */
export type SubscribeIngressDialogProps = {
  /** 開いているか。 */
  open: boolean;
  /** 閉じる。 */
  onClose: () => void;
  /** 204 受理後。一覧の再取得に使う。 */
  onAccepted: () => void;
};

/**
 * 実行一覧から topic / key を `POST /v1/events` へ送る。
 * 候補はテナント内のアクティブ購読。候補外の手入力も受け付ける。
 * 状態と送信は `useSubscribeIngressDialog` にあり、ここは開閉とレイアウトだけを持つ。
 * 色は Tailwind テーマの `md-*` / `brand-*`、送信とキャンセルは `Button`、入力は `TextField` を使う。
 */
export function SubscribeIngressDialog({ open, onClose, onAccepted }: Readonly<SubscribeIngressDialogProps>) {
  const dialog = useSubscribeIngressDialog({ open, onClose, onAccepted });
  if (!open) return null;

  return (
    <dialog
      open
      aria-labelledby="subscribe-ingress-title"
      className="fixed inset-0 z-40 m-0 flex h-full max-h-none w-full max-w-none items-center justify-center border-0 bg-black/40 p-4"
    >
      <form
        className="w-full max-w-lg space-y-3 rounded-lg border border-md-outline bg-md-surface p-4 shadow-lg"
        onSubmit={dialog.submit}
      >
        <h2 id="subscribe-ingress-title" className="text-sm font-semibold text-md-on-surface">
          {dialog.text.title}
        </h2>
        <p className="whitespace-pre-line text-xs text-md-on-surface-variant">{dialog.text.fanOutHint}</p>
        {dialog.candidatesFailed && (
          <p className="text-xs text-md-on-surface-variant">{dialog.text.candidatesUnavailable}</p>
        )}
        <CandidateTextField
          label={dialog.text.topicLabel}
          value={dialog.topic}
          options={dialog.topicOptions}
          onChange={dialog.setTopic}
        />
        <CandidateTextField
          label={dialog.text.keyLabel}
          value={dialog.key}
          options={dialog.keyOptions}
          onChange={dialog.setKey}
        />
        {dialog.fieldError && <p className="text-xs text-md-error">{dialog.fieldError}</p>}
        {dialog.formError && <p className="text-xs text-md-error">{dialog.formError}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {dialog.text.cancel}
          </Button>
          <Button type="submit" variant="primary" disabled={dialog.submitting}>
            {dialog.text.submit}
          </Button>
        </div>
      </form>
    </dialog>
  );
}
