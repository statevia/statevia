import type { InputHTMLAttributes, ReactNode } from "react";

type TextFieldProps = {
  /** 入力に結び付けるラベル文言。 */
  label: string;
  /** 入力へ追加するクラス。等幅が必要なときだけ `font-mono` を渡す。 */
  className?: string;
  /** 入力の直下に置く要素。候補リストなど、このフィールドだけの補足。 */
  children?: ReactNode;
} & Omit<InputHTMLAttributes<HTMLInputElement>, "className" | "children">;

/**
 * 追加クラスを基底クラスの後ろへ結合する。
 * @param base 入力の共通クラス。
 * @param extra 呼び出し側のクラス。無ければ基底のみ。
 * @returns 結合した class 文字列。
 */
function joinClassNames(base: string, extra?: string): string {
  return [base, extra].filter(Boolean).join(" ");
}

/**
 * ラベル付きの 1 行入力。
 * 枠と背景はセマンティックトークンに固定する。候補リストの開閉はこのコンポーネントの外で行う。
 */
export function TextField({ label, className, children, ...inputProps }: Readonly<TextFieldProps>) {
  const inputClassName = joinClassNames(
    "mt-1 w-full rounded border border-md-outline-variant bg-md-surface-container px-2 py-1.5 text-sm",
    className
  );

  return (
    <label className="relative block text-sm text-md-on-surface">
      <span>{label}</span>
      <input className={inputClassName} {...inputProps} />
      {children}
    </label>
  );
}
