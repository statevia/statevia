import { ScheduleFormPageClient } from "@/features/schedules/ui/ScheduleFormPageClient";

/**
 * 定期実行の作成（薄い route wrapper）。門は `../layout.tsx`。
 */
export default function ScheduleCreatePage() {
  return <ScheduleFormPageClient />;
}
