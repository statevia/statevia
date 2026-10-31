import { AdminServiceAccountDetailPageClient } from "@/features/admin/ui/AdminServiceAccountDetailPageClient";

type PageProps = Readonly<{
  params: Promise<{ serviceAccountId: string }>;
}>;

/**
 * ServiceAccount 詳細（薄い route wrapper）。
 */
export default async function AdminServiceAccountDetailPage({ params }: PageProps) {
  const { serviceAccountId } = await params;
  return <AdminServiceAccountDetailPageClient serviceAccountId={serviceAccountId} />;
}
