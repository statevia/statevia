"use client";

import type { ReactNode } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";

/** 入力欄の枠と背景の共通クラス。エラーのときは `INPUT_ERROR_CLASS` を足す。 */
export const INPUT_BASE_CLASS =
  "mt-1.5 w-full rounded-control border border-md-outline-variant bg-md-surface-container px-3 text-sm text-md-on-surface focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-md-primary disabled:opacity-60";

/** エラーの入力欄に足すクラス。 */
export const INPUT_ERROR_CLASS = "border-md-error";

type FieldLabelProps = Readonly<{
  /** 項目名。 */
  label: string;
  /** 必須か。true のとき項目名の右に「*」を付ける。 */
  required?: boolean;
}>;

/**
 * 項目名と必須のマーク。マークは項目名の右に置き、項目名の下に回り込ませない。
 * マークは CSS の疑似要素で描き、ラベルの文字（アクセシブルな名前）には含めない。
 * 必須であることは入力の `required` で伝え、マークの意味は `title` で補う。
 */
export function FieldLabel({ label, required = false }: FieldLabelProps) {
  const uiText = useUiText();
  const requiredClass = required ? " after:ml-1 after:text-md-error after:content-['*']" : "";
  return (
    <span className={`text-sm font-medium text-md-on-surface${requiredClass}`} title={required ? uiText.dialog.required : undefined}>
      {label}
    </span>
  );
}

type FieldMessageProps = Readonly<{
  /** `aria-describedby` で入力と結ぶ ID。 */
  id: string;
  /** 補足の説明。エラーが無いときに出す。 */
  hint?: ReactNode;
  /** エラーの文言。あれば説明の代わりに出す。 */
  error?: ReactNode;
}>;

/**
 * 入力欄の下の説明またはエラー。エラーがあれば説明より優先して同じ位置に出す。
 * @returns 表示する内容が無ければ null。
 */
export function FieldMessage({ id, hint, error }: FieldMessageProps) {
  if (error) {
    return (
      <span id={id} role="alert" className="mt-1 block text-xs text-md-error">
        {error}
      </span>
    );
  }
  if (hint) {
    return (
      <span id={id} className="mt-1 block text-xs text-md-on-surface-variant">
        {hint}
      </span>
    );
  }
  return null;
}

/**
 * 追加クラスを基底クラスの後ろへ結合する。
 * @param parts 結合するクラス。false / undefined は捨てる。
 * @returns 結合した class 文字列。
 */
export function joinFieldClassNames(...parts: ReadonlyArray<string | false | undefined>): string {
  return parts.filter(Boolean).join(" ");
}
