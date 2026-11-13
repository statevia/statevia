"use client";

import { toDisplayValue } from "@/shared/lib/displayValue";
import { getStatusStyle, type StatusDotShape } from "@/shared/lib/statusStyle";
import { useUiText } from "@/shared/i18n/uiTextContext";

type StatusBadgeProps = {
  /**
   * 表示する状態文字列。
   * 既知の StatusLike に加え、API 由来の想定外値も受け付ける（getStatusStyle がフォールバックする）。
   */
  status: string;
  /**
   * 待機があるか。true のとき状態の横に「待機中」の印を付ける。
   * 実行の状態の値に待機は無いため、状態は変えずに印だけを足す（studio-ui-visual-refresh の U1）。
   */
  waiting?: boolean;
  /** 外枠に足すクラス。 */
  className?: string;
};

const DOT_SHAPE_CLASS: Record<StatusDotShape, string> = {
  ring: "h-2 w-2 rounded-full border-2 border-current",
  square: "h-2 w-2 rounded-[2px] bg-current",
  circle: "h-2 w-2 rounded-full bg-current",
  diamond: "h-2 w-2 rotate-45 bg-current",
  line: "h-0.5 w-2 bg-current",
};

/**
 * 点の形で状態を区別する小さな印。
 * @param props 点の形と色のクラス。
 * @returns 読み上げから外した点。
 */
function StatusDot({ shape, accentClass }: Readonly<{ shape: StatusDotShape; accentClass: string }>) {
  return <span aria-hidden="true" className={`inline-block shrink-0 ${DOT_SHAPE_CLASS[shape]} ${accentClass}`} />;
}

/**
 * Execution / Node の状態ラベルを共通表示するバッジ。
 * 文字幅に合わせたピルで、色に加えて点の形でも区別する。
 * 色は API の値で決め、表示は `toDisplayValue` で大文字にそろえる（訳さない）。
 */
export function StatusBadge({ status, waiting = false, className }: Readonly<StatusBadgeProps>) {
  const uiText = useUiText();
  const style = getStatusStyle(status);
  const badgeClassName = [
    "inline-flex h-6 items-center gap-1.5 rounded-pill px-2.5 text-xs font-semibold",
    style.bgClass,
    style.textClass,
    className,
  ]
    .filter(Boolean)
    .join(" ");
  const waitingStyle = getStatusStyle("WAITING");

  return (
    <span className="inline-flex items-center gap-1.5">
      <span className={badgeClassName}>
        <StatusDot shape={style.dotShape} accentClass={style.accentClass} />
        {toDisplayValue(status)}
      </span>
      {waiting ? (
        <span
          className={`inline-flex h-6 items-center gap-1.5 rounded-pill px-2 text-xs font-semibold ${waitingStyle.bgClass} ${waitingStyle.textClass}`}
        >
          <StatusDot shape={waitingStyle.dotShape} accentClass={waitingStyle.accentClass} />
          {uiText.statusBadge.waiting}
        </span>
      ) : null}
    </span>
  );
}
