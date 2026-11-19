import type { AuthMeResponse } from "@/shared/auth/authMe";
import type { ProjectSummary } from "@/shared/project/types";

/**
 * 撮影の時計を止める時刻（2026-11-12 10:30 JST）。
 * @remarks 架空データの日時はこの時刻より前に置く。ダッシュボードの「直近 24 時間」もこの時刻から数える。
 */
export const SCREENSHOT_NOW = "2026-11-12T01:30:00Z";

/**
 * 撮影で使うテナント管理者（`GET /v1/auth/me`）。
 * @remarks 管理画面と定期実行はサーバー側でもこの応答を使うので、ブラウザ側と同じ値を返す。
 */
export const screenshotAdmin: AuthMeResponse = {
  tenantId: "00000000-0000-4000-8000-000000000001",
  tenantKey: "demo",
  principalId: "00000000-0000-4000-8000-000000000111",
  username: "sato.hanako",
  email: "sato.hanako@example.com",
  isTenantAdmin: true,
  permissions: [
    "tenant.admin",
    "definitions.read",
    "definitions.write",
    "executions.read",
    "executions.write",
    "projects.create"
  ]
};

/** 架空の Project の ID。先頭の「受注処理」をサイドメニューで選んだ状態で撮る。 */
export const PROJECT_IDS = {
  orders: "00000000-0000-4000-8000-000000000201",
  inventory: "00000000-0000-4000-8000-000000000202"
} as const;

/** `GET /v1/projects` の応答（所属 Project が 2 件。どちらも管理者）。 */
export const screenshotProjects: ProjectSummary[] = [
  {
    projectId: PROJECT_IDS.orders,
    slug: "order-processing",
    displayName: "受注処理",
    description: "受注から出荷・通知までのワークフロー",
    createdAt: "2026-09-01T00:00:00Z",
    role: "admin"
  },
  {
    projectId: PROJECT_IDS.inventory,
    slug: "inventory",
    displayName: "在庫管理",
    description: "倉庫間の在庫移動と棚卸",
    createdAt: "2026-09-15T00:00:00Z",
    role: "admin"
  }
];
