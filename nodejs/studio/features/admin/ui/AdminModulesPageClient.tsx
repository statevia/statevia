"use client";

import { RefreshCw } from "lucide-react";
import { Breadcrumbs } from "@/shared/ui/Breadcrumbs";
import { Button } from "@/shared/ui/Button";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { useAdminModulesPage } from "../hooks/useAdminModulesPage";
import { AdminModuleList } from "./AdminModuleList";

/**
 * アクションモジュールのロード結果を見る画面。読み取りだけで、主導線（緑のボタン）は置かない。
 * 「一覧を読み直す」は `GET /v1/admin/modules` を取り直すだけで、`POST /internal/modules/reload` は呼ばない。
 * 配置・再読み込み・削除は CLI の運用手順に残す。取得の失敗（401 / 403 を含む）は空の一覧にせずエラーで出す。
 * @returns アクションモジュール一覧画面。
 */
export function AdminModulesPageClient() {
  const uiText = useUiText();
  const text = uiText.admin.modules;
  const page = useAdminModulesPage();
  const modules = page.modules;

  return (
    <PageShell
      title={text.title}
      description={text.description}
      breadcrumbs={<Breadcrumbs items={[{ label: uiText.navigation.groups.tenantSettings }, { label: text.title }]} />}
      primaryActions={
        <Button variant="outline" onClick={() => void page.reload()} disabled={page.loading}>
          <RefreshCw aria-hidden="true" className="h-4 w-4" />
          {text.refresh}
        </Button>
      }
    >
      <p className="mb-3 text-xs text-md-on-surface-variant">{text.reloadNote}</p>
      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && modules === null ? (
        <PageState state="error" message={text.loadFailed} retryLabel={text.retry} onRetry={() => void page.reload()} />
      ) : null}
      {!page.loading && modules !== null && modules.length === 0 ? <PageState state="empty" message={text.empty} /> : null}
      {!page.loading && modules !== null && modules.length > 0 ? <AdminModuleList modules={modules} /> : null}
    </PageShell>
  );
}
