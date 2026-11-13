/** 行ごとの棒の幅。同じ幅が並ぶと表に見えないため、行ごとに少しずらす。 */
const ROW_WIDTHS = ["w-36", "w-28", "w-40", "w-32", "w-24"] as const;

type TableSkeletonProps = Readonly<{
  /** 行の数。省略すると 5。 */
  rows?: number;
}>;

/**
 * 一覧の読み込み中に出す骨組み。表の形を先に出し、読み込み後に画面がずれないようにする。
 * 0.3 秒以内に終わる読み込みでは出さない（遅延は `PageState` と `useDelayedVisibility` が持つ）。
 * 中身は読み上げから外し、読み込み中であることは呼び出し側の `aria-busy` と文言で伝える。
 */
export function TableSkeleton({ rows = 5 }: TableSkeletonProps) {
  return (
    <div aria-hidden="true" className="overflow-hidden rounded-card border border-md-outline bg-md-surface">
      <div className="flex gap-6 border-b border-md-outline bg-md-surface-container px-4 py-3">
        <span className="h-2.5 w-20 rounded-pill bg-md-outline" />
        <span className="h-2.5 w-14 rounded-pill bg-md-outline" />
        <span className="h-2.5 w-24 rounded-pill bg-md-outline" />
      </div>
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} className="flex items-center gap-6 border-b border-md-outline px-4 py-4 last:border-b-0">
          <span className={`h-3 rounded-pill bg-md-outline ${ROW_WIDTHS[index % ROW_WIDTHS.length]}`} />
          <span className="h-3 w-10 rounded-pill bg-md-surface-container-high" />
          <span className="h-5 w-24 rounded-pill bg-md-surface-container-high" />
        </div>
      ))}
    </div>
  );
}
