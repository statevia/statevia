"use client";

import { useId, type InputHTMLAttributes, type ReactNode } from "react";
import { FieldLabel, FieldMessage, inputClassName, joinFieldClassNames } from "./FormField";

type TextFieldProps = {
  /** 入力に結び付けるラベル文言。 */
  label: string;
  /** 入力の下に出す補足の説明。 */
  hint?: ReactNode;
  /** エラーの文言。あれば枠を赤にし、説明の代わりに出す。 */
  error?: ReactNode;
  /** 入力へ追加するクラス。等幅が必要なときだけ `font-mono` を渡す。 */
  className?: string;
  /** 入力の直下に置く要素。候補リストなど、このフィールドだけの補足。 */
  children?: ReactNode;
} & Omit<InputHTMLAttributes<HTMLInputElement>, "className" | "children">;

/**
 * ラベル付きの 1 行入力。
 * `required` のときは項目名の右に「*」を付ける。説明とエラーは同じ位置に出し、`aria-describedby` で入力と結ぶ。
 * 枠と背景はセマンティックトークンに固定する。候補リストの開閉はこのコンポーネントの外で行う。
 */
export function TextField({ label, hint, error, className, children, required, ...inputProps }: Readonly<TextFieldProps>) {
  const messageId = useId();
  const hasMessage = Boolean(error ?? hint);
  return (
    <div className="relative block">
      <label className="block">
        <FieldLabel label={label} required={required} />
        <input
          className={inputClassName({ error: Boolean(error), className: joinFieldClassNames("h-9", className) })}
          required={required}
          aria-invalid={error ? true : undefined}
          aria-describedby={hasMessage ? messageId : undefined}
          {...inputProps}
        />
      </label>
      <FieldMessage id={messageId} hint={hint} error={error} />
      {children}
    </div>
  );
}
