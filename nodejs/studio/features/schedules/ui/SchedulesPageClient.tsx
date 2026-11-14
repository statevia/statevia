"use client";

import Link from "next/link";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { buttonClassName } from "@/shared/ui/Button";
import { Notice } from "@/shared/ui/Notice";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useSchedulesPage } from "../hooks/useSchedulesPage";
import { ScheduleList } from "./ScheduleList";

/**
 * 定期実行の一覧。作成ボタン、空状態、定期実行の編集・手動実行・削除を出す。
 * テナント管理者以外は `app/schedules/layout.tsx` が戻す。所属の無い Project の定期実行は API が返さないため、画面では隠さない。
 * @returns 定期実行の一覧画面。
 */
export function SchedulesPageClient() {
  const text = useI18n().uiText.schedules;
  const page = useSchedulesPage();
  const schedules = page.schedules;

  return (
    <PageShell
      title={text.title}
      description={text.description}
      primaryActions={
        <Link href="/schedules/new" className={buttonClassName("lead")}>
          {text.create}
        </Link>
      }
    >
      <Toast toast={page.toast} onClose={page.clearToast} />
      {page.loading ? <PageState state="loading" /> : null}
      {!page.loading && schedules === null ? (
        <PageState state="error" message={text.loadFailed} retryLabel={text.retry} onRetry={() => void page.reload()} />
      ) : null}
      {!page.loading && schedules !== null && schedules.length === 0 ? (
        <PageState
          state="empty"
          message={text.empty}
          emptyAction={
            <Link href="/admin/projects" className={buttonClassName("outline")}>
              {text.joinProjectLink}
            </Link>
          }
        />
      ) : null}
      {!page.loading && schedules !== null && schedules.length > 0 ? (
        <>
          {page.definitionNames === null ? (
            <Notice tone="warning" title={text.definitionNamesUnavailable} />
          ) : null}
          <ScheduleList schedules={schedules} page={page} />
        </>
      ) : null}
    </PageShell>
  );
}
