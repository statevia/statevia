import { SchedulesPageClient } from "@/features/schedules/ui/SchedulesPageClient";

/**
 * 定期実行の一覧（薄い route wrapper）。門は `layout.tsx`。
 */
export default function SchedulesPage() {
  return <SchedulesPageClient />;
}
