import { Loader2 } from "lucide-react";
import type { ButtonHTMLAttributes } from "react";

/**
 * ボタンの役割。色は役割で決め、画面で色のクラスを直書きしない。
 * - `lead`: 主導線（作成・保存・新規実行の開始など）。緑。1 画面に 1 つまで
 * - `standard`: その場で終わる通常の操作（編集・追加など）。ライトは紺、ダークは青みの淡い塗り
 * - `outline`: 補助（キャンセル・戻る・ダウンロードなど）
 * - `danger`: 破壊的な操作の入口（削除・無効化など）。赤枠
 * - `dangerSolid`: 確認ダイアログの中の決定だけに使う赤の塗り
 * - `ghost`: 一覧の行の「…」など、枠も塗りも持たない操作
 */
export type ButtonVariant = "lead" | "standard" | "outline" | "danger" | "dangerSolid" | "ghost";

/** ボタンの大きさ。`sm` は一覧の行の中だけ、`icon` はアイコンだけのボタン（`aria-label` 必須）。 */
export type ButtonSize = "md" | "sm" | "icon";

const BASE_CLASS =
  "inline-flex items-center justify-center gap-1.5 rounded-control text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-md-primary disabled:cursor-not-allowed";

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  lead: "bg-brand-lead-bg font-semibold text-brand-lead-fg hover:bg-brand-lead-bg-hover disabled:opacity-60",
  standard:
    "border border-brand-cta-border bg-brand-cta-bg text-brand-cta-fg hover:bg-brand-cta-bg-hover disabled:opacity-60",
  outline:
    "border border-md-outline-variant bg-md-surface text-md-on-surface hover:bg-md-surface-container-high disabled:opacity-60",
  danger:
    "border border-md-error/40 bg-md-surface text-md-error hover:bg-md-error-container disabled:opacity-60",
  dangerSolid: "bg-md-error font-semibold text-md-on-error hover:opacity-90 disabled:opacity-60",
  ghost: "text-md-on-surface-variant hover:bg-md-surface-container-high hover:text-md-on-surface disabled:opacity-60",
};

const SIZE_CLASS: Record<ButtonSize, string> = {
  md: "h-9 px-4",
  sm: "h-7 px-2.5 text-[13px]",
  icon: "h-9 w-9",
};

/**
 * ボタンと同じ見た目のクラス名を返す。`next/link` の `<Link>` をボタンの見た目にするときに使う。
 * @param variant ボタンの役割。
 * @param size 大きさ。省略すると `md`。
 * @param extra 呼び出し側の追加クラス。
 * @returns 結合した class 文字列。
 */
export function buttonClassName(variant: ButtonVariant, size: ButtonSize = "md", extra?: string): string {
  return [BASE_CLASS, VARIANT_CLASS[variant], SIZE_CLASS[size], extra].filter(Boolean).join(" ");
}

type ButtonProps = {
  /** ボタンの役割。 */
  variant: ButtonVariant;
  /** 大きさ。省略すると `md`。 */
  size?: ButtonSize;
  /** 送信中か。true のときはスピナーを出して押せなくし、二重送信を防ぐ。 */
  loading?: boolean;
} & ButtonHTMLAttributes<HTMLButtonElement>;

/**
 * 全画面で使うボタン。
 * `type` を省略したときは `button` になり、フォームの暗黙の submit にはしない。
 * `loading` のときは `disabled` と `aria-busy` を付け、先頭にスピナーを出す（文言は呼び出し側が「保存中…」などに替える）。
 */
export function Button({
  variant,
  size = "md",
  loading = false,
  className,
  type = "button",
  disabled,
  children,
  ...rest
}: Readonly<ButtonProps>) {
  return (
    <button
      type={type}
      className={buttonClassName(variant, size, className)}
      disabled={disabled === true || loading}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? <Loader2 aria-hidden="true" className="h-4 w-4 animate-spin" /> : null}
      {children}
    </button>
  );
}
