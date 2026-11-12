"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "./ExecutionDashboard";
import { ActionLinkGroup } from "@/shared/ui/ActionLinkGroup";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { useUiText } from "@/shared/i18n/uiTextContext";

/**
 * 1 つの実行を見る唯一の画面（実行詳細）。
 * @remarks 操作（キャンセル・再開・イベント送信）は executions.write を持つ利用者にだけ出す。
 * 閲覧の利用者には操作も権限が無いことの案内も出さない。出し分けは案内で、認可の正本は Service API である。
 */
export function ExecutionDetailPage() {
  const uiText = useUiText();
  const params = useParams();
  const executionId = useMemo(() => {
    const raw = params.executionId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.executionId]);

  if (!executionId.trim()) {
    return (
      <PageShell
        title={uiText.executionDetailPage.title}
        primaryActions={
          <ActionLinkGroup
            links={[{ label: uiText.lists.executions, href: "/executions", priority: "primary" }]}
          />
        }
      >
        <PageState state="error" message={uiText.executionDetailPage.missingExecutionId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={executionId}
      initialExecutionId={executionId}
      autoLoadOnMount
      headerTitle={uiText.executionDetailPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      headerNav={
        <ActionLinkGroup
          links={[{ label: uiText.lists.executions, href: "/executions", priority: "primary" }]}
        />
      }
    />
  );
}
