"use client";

import { useId, type ReactNode, type SelectHTMLAttributes } from "react";
import { FieldLabel, FieldMessage, inputClassName, joinFieldClassNames } from "./FormField";

type SelectFieldProps = {
  /** 選択肢に結び付けるラベル文言。 */
  label: string;
  /** 下に出す補足の説明。 */
  hint?: ReactNode;
  /** エラーの文言。 */
  error?: ReactNode;
  /** `<option>` の並び。 */
  children: ReactNode;
  /** select へ追加するクラス。 */
  className?: string;
} & Omit<SelectHTMLAttributes<HTMLSelectElement>, "className" | "children">;

/**
 * ラベル付きの選択欄。見た目と必須のマーク・説明・エラーの位置は `TextField` と同じ。
 */
export function SelectField({ label, hint, error, children, className, required, ...selectProps }: Readonly<SelectFieldProps>) {
  const messageId = useId();
  const hasMessage = Boolean(error ?? hint);
  return (
    <div className="block">
      <label className="block">
        <FieldLabel label={label} required={required} />
        <select
          className={inputClassName({ error: Boolean(error), className: joinFieldClassNames("h-9", className) })}
          required={required}
          aria-invalid={error ? true : undefined}
          aria-describedby={hasMessage ? messageId : undefined}
          {...selectProps}
        >
          {children}
        </select>
      </label>
      <FieldMessage id={messageId} hint={hint} error={error} />
    </div>
  );
}
