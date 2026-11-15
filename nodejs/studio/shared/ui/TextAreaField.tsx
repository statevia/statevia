"use client";

import { useId, type ReactNode, type TextareaHTMLAttributes } from "react";
import { FieldLabel, FieldMessage, inputClassName, joinFieldClassNames } from "./FormField";

type TextAreaFieldProps = {
  /** 入力に結び付けるラベル文言。 */
  label: string;
  /** 下に出す補足の説明。 */
  hint?: ReactNode;
  /** エラーの文言。 */
  error?: ReactNode;
  /** textarea へ追加するクラス。 */
  className?: string;
} & Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "className">;

/**
 * ラベル付きの複数行入力。見た目と必須のマーク・説明・エラーの位置は `TextField` と同じ。
 */
export function TextAreaField({ label, hint, error, className, required, ...textareaProps }: Readonly<TextAreaFieldProps>) {
  const messageId = useId();
  const hasMessage = Boolean(error ?? hint);
  return (
    <div className="block">
      <label className="block">
        <FieldLabel label={label} required={required} />
        <textarea
          className={inputClassName({ error: Boolean(error), className: joinFieldClassNames("py-2", className) })}
          required={required}
          aria-invalid={error ? true : undefined}
          aria-describedby={hasMessage ? messageId : undefined}
          {...textareaProps}
        />
      </label>
      <FieldMessage id={messageId} hint={hint} error={error} />
    </div>
  );
}
