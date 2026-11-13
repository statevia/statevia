import { Inbox, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

type EmptyStateProps = Readonly<{
  /** 見出し（例: まだ定義がありません）。 */
  title: string;
  /** 次にできることの説明。 */
  description?: ReactNode;
  /** 次にできる操作。1 つに絞る（0 件なら作成、検索に一致しないなら条件をクリア）。 */
  action?: ReactNode;
  /** 見出しの上のアイコン。省略すると受け箱。 */
  icon?: LucideIcon;
}>;

/**
 * データが無いとき・検索に一致しないとき・権限が無いときの表示。
 * 一覧の表と同じ枠の中に出し、次にできる操作を 1 つ添える。
 */
export function EmptyState({ title, description, action, icon: Icon = Inbox }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-card border border-md-outline bg-md-surface px-6 py-12 text-center">
      <span className="inline-flex h-12 w-12 items-center justify-center rounded-pill bg-md-surface-container-high text-md-on-surface-variant">
        <Icon aria-hidden="true" className="h-6 w-6" />
      </span>
      <div className="flex flex-col gap-1">
        <p className="text-base font-semibold text-md-on-surface">{title}</p>
        {description ? <div className="text-[13px] text-md-on-surface-variant">{description}</div> : null}
      </div>
      {action ? <div className="flex flex-wrap justify-center gap-2">{action}</div> : null}
    </div>
  );
}
