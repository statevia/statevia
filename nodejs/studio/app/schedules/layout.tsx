import { redirect } from "next/navigation";
import { fetchAuthMeServer } from "@/shared/auth/serverAuthMe";

/**
 * 定期実行画面のレイアウト。テナント管理者だけが使う。
 *
 * API の権限（`executions.read` / `executions.write` と定義の Project の所属）とは別に、画面はテナント管理者に絞る。
 * run-as の候補を返す `GET /v1/admin/service-accounts` が管理者専用のため。管理者でも、所属の無い Project の行は API が返さない。
 * 未ログインはログインへ、管理者以外はダッシュボードへ戻し、schedules API は呼ばせない。
 */
export default async function SchedulesLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const me = await fetchAuthMeServer();
  if (!me) redirect("/login?from=/schedules");
  if (!me.isTenantAdmin) redirect("/dashboard");
  return children;
}
