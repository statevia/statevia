"use client";

import type { ReactNode } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";

/**
 * 入力欄の枠の形と背景の共通クラス。横の余白と枠の色は含めない。
 * 同じ CSS プロパティのクラスを後から足しても、Tailwind は HTML の順ではなく CSS の順で勝ち負けが決まるため
 * （例: `px-3` に `pl-9` を足しても `px-3` が勝つ）、横の余白と枠の色は `inputClassName` で 1 つだけ選ぶ。
 */
export const INPUT_BASE_CLASS =
  "mt-1.5 w-full rounded-control border bg-md-surface-container text-sm text-md-on-surface focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-md-primary disabled:opacity-60";

type InputClassNameOptions = Readonly<{
  /** エラーのとき true。枠を赤にする。 */
  error?: boolean;
  /** 横の余白。アイコンを入力の中に置くときだけ変える（例: 左に虫眼鏡なら `pl-9 pr-3`）。省略時は `px-3`。 */
  paddingX?: string;
  /** 高さ・等幅などの追加クラス。横の余白と枠の色は渡さない。 */
  className?: string;
}>;

/**
 * 入力欄・選択欄・テキストエリアのクラスを組み立てる。
 * 枠の色（通常・エラー）と横の余白は、衝突しないようにどちらか 1 つだけを入れる。
 * @param options エラーか、横の余白、追加クラス。
 * @returns class 文字列。
 */
export function inputClassName({ error = false, paddingX = "px-3", className }: InputClassNameOptions = {}): string {
  return joinFieldClassNames(
    INPUT_BASE_CLASS,
    error ? "border-md-error" : "border-md-outline-variant",
    paddingX,
    className,
  );
}

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
