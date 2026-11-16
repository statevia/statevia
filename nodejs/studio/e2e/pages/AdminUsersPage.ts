import type { Locator, Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";
import { step } from "../support/step";

const usersText = uiText.admin.users;

/**
 * 管理画面のユーザー管理（`/admin/users`）の Page Object。
 * @remarks `app/admin/layout.tsx` がサーバー側で `GET /v1/auth/me` を呼び、テナント管理者以外をダッシュボードへ戻す。
 * ブラウザの `page.route` ではモックできないので、real-api project だけで使う（U1=A）。
 * ユーザー名は E2E の資格情報なので、ロケーターにもステップ名にも使わない。検証（`expect`）は持たず、spec に書く。
 */
export class AdminUsersPage {
  /** 見出し（h1）の「ユーザー管理」。 */
  readonly heading: Locator;
  /** 行ごとの「パスワード更新」。1 つ以上あれば、一覧の API が返したユーザーが並んでいる。 */
  readonly passwordButtons: Locator;
  /** 一覧の取得に失敗したときの表示。 */
  readonly loadError: Locator;

  /**
   * @param page 対象のページ。
   */
  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { level: 1, name: usersText.title });
    this.passwordButtons = page.getByRole("button", { name: usersText.updatePassword, exact: true });
    this.loadError = page.getByText(uiText.pageState.error, { exact: true });
  }

  /** 管理画面の入口（`/admin`）を開く。ユーザー管理へ移る。 */
  async gotoAdminRoot(): Promise<void> {
    await step("管理画面を開く", async () => {
      await this.page.goto("/admin");
    });
  }
}
