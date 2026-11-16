import type { Locator, Page } from "@playwright/test";
import { uiText } from "@/shared/i18n/uiText";
import { step } from "../support/step";

const schedulesText = uiText.schedules;

/**
 * 定期実行の一覧（`/schedules`）の Page Object。
 * @remarks `app/schedules/layout.tsx` がサーバー側で `GET /v1/auth/me` を呼び、テナント管理者以外をダッシュボードへ戻す。
 * ブラウザの `page.route` ではモックできないので、real-api project だけで使う（U1=A）。
 * 定期実行の有無は環境によるので、読み込みの終わりは「表」か「空の表示」のどちらかで判断する。検証（`expect`）は持たず、spec に書く。
 */
export class SchedulesPage {
  /** 見出し（h1）の「定期実行」。 */
  readonly heading: Locator;
  /** 見出しの右の「定期実行を作成」。 */
  readonly createLink: Locator;
  /** 読み込みが終わったときに出るもの（定期実行の表、または定期実行が無いときの表示）。 */
  readonly loaded: Locator;
  /** 一覧の取得に失敗したときの表示。 */
  readonly loadError: Locator;

  /**
   * @param page 対象のページ。
   */
  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { level: 1, name: schedulesText.title });
    this.createLink = page.getByRole("link", { name: schedulesText.create, exact: true });
    this.loaded = page.getByRole("table").or(page.getByText(schedulesText.empty, { exact: true }));
    this.loadError = page.getByText(schedulesText.loadFailed, { exact: true });
  }

  /** 定期実行の一覧を開く。 */
  async goto(): Promise<void> {
    await step("定期実行の一覧を開く", async () => {
      await this.page.goto("/schedules");
    });
  }
}
