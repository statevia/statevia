import { AdminUserResourceGrantsPageClient } from "@/features/admin/ui/AdminUserResourceGrantsPageClient";

type PageProps = Readonly<{
  params: Promise<{ userId: string }>;
}>;

/**
 * ユーザーの実行リソース許可（薄い route wrapper）。
 */
export default async function AdminUserResourceGrantsPage({ params }: PageProps) {
  const { userId } = await params;
  return <AdminUserResourceGrantsPageClient userId={userId} />;
}
