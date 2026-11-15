import { AlertTriangle, CheckCircle2, Info, XCircle, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

/** お知らせの種類。色はトークンの `*-container` から取り、ライトとダークの両方で読める。 */
export type NoticeTone = "info" | "success" | "warning" | "error";

/**
 * ダークでは `*-container` のベタ塗りが濃く見えるため、`statusStyle` と同じく混色で弱める。
 * クラス名は Tailwind が拾えるよう静的な文字列で持つ。
 */
const TONE_CLASS: Record<NoticeTone, string> = {
  info: "border-md-info/40 bg-md-info-container text-md-on-info-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-info-container)_30%,transparent)]",
  success:
    "border-md-success/40 bg-md-success-container text-md-on-success-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-success-container)_30%,transparent)]",
  warning:
    "border-md-warning/40 bg-md-warning-container text-md-on-warning-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-warning-container)_30%,transparent)]",
  error:
    "border-md-error/40 bg-md-error-container text-md-on-error-container dark:bg-[color-mix(in_srgb,var(--md-sys-color-error-container)_30%,transparent)]",
};

const TONE_ICON: Record<NoticeTone, LucideIcon> = {
  info: Info,
  success: CheckCircle2,
  warning: AlertTriangle,
  error: XCircle,
};

type NoticeProps = Readonly<{
  /** 種類。 */
  tone: NoticeTone;
  /** 1 行目の見出し。 */
  title: ReactNode;
  /** 本文。 */
  children?: ReactNode;
  /** 本文の下に置く操作（再試行など）。 */
  actions?: ReactNode;
  /** 外枠に足すクラス。 */
  className?: string;
}>;

/**
 * 情報・成功・注意・エラーのお知らせ。画面ごとの `amber-*` / `red-*` / `emerald-*` の直書きの枠を置き換える。
 * エラーは `role="alert"` で、それ以外は `<output>` で読み上げる。
 */
export function Notice({ tone, title, children, actions, className }: NoticeProps) {
  const Icon = TONE_ICON[tone];
  const boxClassName = ["flex gap-3 rounded-card border px-4 py-3", TONE_CLASS[tone], className].filter(Boolean).join(" ");
  const content = (
    <>
      <Icon aria-hidden="true" className="mt-0.5 h-5 w-5 shrink-0" />
      <div className="flex min-w-0 flex-col items-start gap-1">
        <p className="text-sm font-semibold">{title}</p>
        {children ? <div className="text-[13px] leading-relaxed">{children}</div> : null}
        {actions ? <div className="mt-1 flex flex-wrap gap-2">{actions}</div> : null}
      </div>
    </>
  );
  if (tone === "error") {
    return (
      <div role="alert" className={boxClassName}>
        {content}
      </div>
    );
  }
  return <output className={boxClassName}>{content}</output>;
}
