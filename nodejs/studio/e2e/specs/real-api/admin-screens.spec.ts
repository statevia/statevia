import { realTest as test, expect } from "../../fixtures/test";

/**
 * テナント管理者だけが使う画面の real-api の E2E。
 * @remarks どちらの画面も layout がサーバー側で `GET /v1/auth/me` を呼び、テナント管理者以外を戻す（モックできない。U1=A）。
 * ここでは、setup-real が保存したテナント管理者の Cookie で画面に入り、一覧の API がプロキシ経由で返ることだけを確かめる。
 * データは作らないので後始末は要らない。前提と実行例は `e2e/README.md`。
 */
test.describe("テナント管理者の画面 (real)", () => {
  test("管理画面を開くとユーザー管理へ移り、ユーザーの一覧が出る", async ({ adminUsersPage, page }) => {
    // Arrange / Act
    await adminUsersPage.gotoAdminRoot();

    // Assert
    await expect(page).toHaveURL(/\/admin\/users$/);
    await expect(adminUsersPage.heading).toBeVisible();
    await expect(adminUsersPage.passwordButtons.first()).toBeVisible();
    await expect(adminUsersPage.loadError).toHaveCount(0);
  });

  test("定期実行を開くと、一覧（または定期実行が無いこと）と作成の導線が出る", async ({ schedulesPage, page }) => {
    // Arrange / Act
    await schedulesPage.goto();

    // Assert
    await expect(page).toHaveURL(/\/schedules$/);
    await expect(schedulesPage.heading).toBeVisible();
    await expect(schedulesPage.loaded).toBeVisible();
    await expect(schedulesPage.loadError).toHaveCount(0);
    await expect(schedulesPage.createLink).toBeVisible();
  });
});
