type BrandMarkProps = Readonly<{
  /** 一辺の大きさ（px）。 */
  size?: number;
  /** 追加するクラス名。 */
  className?: string;
}>;

/**
 * Statevia のマーク（状態が分かれて進む図）。紺のヘッダーの上に置く前提の色で描く。
 * 元のアイコンから外側の円を外し、線を太くしたもの（studio-ui-visual-refresh の案 A）。
 * 下の分岐は進まなかった方に見えるため、矢じりを付けない。
 * 画像ファイルではなくインラインの SVG にし、next/image の SVG 設定に頼らない。
 * 飾りとして読み上げから外す。名前は置き場所（ヘッダーのリンク）の側で付ける。
 */
export function BrandMark({ size = 32, className }: BrandMarkProps) {
  return (
    <svg
      aria-hidden="true"
      width={size}
      height={size}
      viewBox="0 0 48 48"
      fill="none"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth={3.2}
      className={className}
    >
      <circle cx="8" cy="24" r="4.5" stroke="#3b82f6" />
      <path d="M12.5 24H19.5M16.5 21l3 3-3 3" stroke="#3b82f6" />
      <path d="M25 19v-2a5 5 0 0 1 5-5h4.5M31.5 9l3 3-3 3" stroke="#10b981" />
      <path d="M25 29v2a5 5 0 0 0 5 5h5" stroke="#94a3b8" />
      <circle cx="40.5" cy="36" r="4" stroke="#94a3b8" />
      <circle cx="25" cy="24" r="5.5" fill="#3b82f6" stroke="none" />
      <circle cx="40.5" cy="12" r="4.5" fill="#10b981" stroke="none" />
    </svg>
  );
}
