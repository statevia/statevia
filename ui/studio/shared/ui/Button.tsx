import type { ButtonHTMLAttributes } from "react";

/** 操作ボタンの種類。`primary` はブランドの CTA、`secondary` は枠線のみ。 */
export type ButtonVariant = "primary" | "secondary";

type ButtonProps = {
  /** 見た目。集合配送ダイアログの送信・キャンセルに合わせる。 */
  variant: ButtonVariant;
} & ButtonHTMLAttributes<HTMLButtonElement>;

const BUTTON_VARIANT_CLASS: Record<ButtonVariant, string> = {
  primary:
    "rounded border-2 border-brand-cta-border bg-brand-cta-bg px-3 py-1.5 text-sm font-medium text-brand-cta-fg",
  secondary: "rounded border border-md-outline-variant px-3 py-1.5 text-sm",
};

/**
 * 追加クラスを基底クラスの後ろへ結合する。
 * @param base バリアントのクラス。
 * @param extra 呼び出し側のクラス。無ければ基底のみ。
 * @returns 結合した class 文字列。
 */
function joinClassNames(base: string, extra?: string): string {
  return [base, extra].filter(Boolean).join(" ");
}

/**
 * フォームの送信とキャンセルに使うボタン。
 * `type` を省略したときは `button` になり、フォームの暗黙の submit にはしない。
 * disabled 時の透明度は付けない。集合配送ダイアログの現行の見た目に合わせる。
 * リンク用の hover（`ActionLinkGroup` の primary）とは別物であり、ここでは hover を足さない。
 */
export function Button({ variant, className, type = "button", ...rest }: Readonly<ButtonProps>) {
  return <button type={type} className={joinClassNames(BUTTON_VARIANT_CLASS[variant], className)} {...rest} />;
}
