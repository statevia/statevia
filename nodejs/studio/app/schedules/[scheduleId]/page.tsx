import { ScheduleFormPageClient } from "@/features/schedules/ui/ScheduleFormPageClient";

type PageProps = Readonly<{
  params: Promise<{ scheduleId: string }>;
}>;

/**
 * 定期実行の編集（薄い route wrapper）。門は `../layout.tsx`。
 */
export default async function ScheduleEditPage({ params }: PageProps) {
  const { scheduleId } = await params;
  return <ScheduleFormPageClient scheduleId={scheduleId} />;
}
