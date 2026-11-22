import { AdminModulesPageClient } from "@/features/admin/ui/AdminModulesPageClient";

/**
 * アクションモジュールのロード結果の画面（薄い route wrapper）。門は `app/admin/layout.tsx` のテナント管理者。
 */
export default function AdminModulesPage() {
  return <AdminModulesPageClient />;
}
