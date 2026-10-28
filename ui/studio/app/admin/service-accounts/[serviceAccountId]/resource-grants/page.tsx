import { AdminServiceAccountResourceGrantsPageClient } from "@/features/admin/ui/AdminServiceAccountResourceGrantsPageClient";

type PageProps = Readonly<{
  params: Promise<{ serviceAccountId: string }>;
}>;

/**
 * ServiceAccount の実行リソース許可（薄い route wrapper）。
 */
export default async function AdminServiceAccountResourceGrantsPage({ params }: PageProps) {
  const { serviceAccountId } = await params;
  return <AdminServiceAccountResourceGrantsPageClient serviceAccountId={serviceAccountId} />;
}
