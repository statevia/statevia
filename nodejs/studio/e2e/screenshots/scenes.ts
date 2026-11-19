import { expect, type Page } from "@playwright/test";
import { definitionEditorUiTextJa } from "@/features/definition-editor/i18n";
import { ExecutionDetailPage } from "../pages/ExecutionDetailPage";
import { screenshotApiKeys, screenshotServiceAccounts, screenshotUsers } from "./data/admin";
import { screenshotDefinitions } from "./data/definitions";
import { screenshotExecutions, WAITING_EXECUTION_ID } from "./data/executions";
import { screenshotProjectMembers } from "./data/projects";
import { screenshotEditSchedule, screenshotSchedules } from "./data/schedules";

/**
 * 画面を撮れる状態にする。表示の切り替えやダイアログを開き、架空データが描かれるまで待つ。
 * @param page 画面を開いたページ。
 */
type Scene = (page: Page) => Promise<void>;

/**
 * 架空データの文字が見えるまで待つ場面を作る。
 * @param text 画面に出る架空データの文字（一覧の最後の行など、読み込みが終わると出るもの）。
 * @param exact 文字の全体が一致するものだけを探すか。「佐藤 花子（ユーザー）」のように前後に語が付く画面では false。
 * @returns 場面。
 */
const waitForText =
  (text: string, exact = true): Scene =>
  async (page) => {
    await expect(page.getByText(text, { exact }).first()).toBeVisible();
  };

const editorText = definitionEditorUiTextJa.definitionEditor;

/**
 * グラフ（React Flow）の「全体を表示」を押す。全画面に広げた直後は、広げる前の大きさで合わせた表示のまま端が切れるため。
 * @param page グラフを表示したページ。
 */
async function fitGraphToView(page: Page): Promise<void> {
  // Reason: React Flow の操作ボタンは Studio の辞書を通らず、ライブラリの英語のラベル（Fit View）を持つ。
  await page.getByRole("button", { name: /fit view/i }).click();
}

/**
 * manifest の画面 ID ごとの場面。
 * @remarks manifest に画面を足したらここにも足す。足し忘れは撮影の spec が失敗にする。
 * 待つ文字は data の値から取り、文言の直書きを避ける。
 */
export const screenshotScenes: Readonly<Record<string, Scene>> = {
  "execution-graph": async (page) => {
    const detail = new ExecutionDetailPage(page);
    await expect(detail.heading(WAITING_EXECUTION_ID)).toBeVisible();
    await detail.showGraph();
    // README で目を引くよう、グラフを全画面に広げて撮る。
    await detail.fullscreenButton.click();
    await fitGraphToView(page);
    // グラフにだけ出る、まだ到達していないノード。
    await expect(page.getByText("order.merge", { exact: true }).first()).toBeVisible();
  },
  "definition-editor": async (page) => {
    await expect(page.getByRole("textbox", { name: editorText.labels.name })).toHaveValue(screenshotDefinitions[0].name);
    await page
      .getByRole("toolbar", { name: editorText.actions.editorModeLabel })
      .getByRole("button", { name: editorText.actions.switchToGraph, exact: true })
      .click();
    await page.getByRole("button", { name: editorText.graph.fullscreenEnter, exact: true }).click();
    await fitGraphToView(page);
    await expect(page.getByText("order.notify.customer", { exact: true }).first()).toBeVisible();
  },
  dashboard: waitForText(screenshotExecutions[6].displayId),
  definitions: waitForText(screenshotDefinitions[4].name),
  executions: waitForText(screenshotExecutions[9].displayId),
  "execution-send-event": async (page) => {
    const detail = new ExecutionDetailPage(page);
    await expect(detail.heading(WAITING_EXECUTION_ID)).toBeVisible();
    await detail.openSendEvent();
    await expect(detail.sendEventDialog.root).toBeVisible();
  },
  schedules: waitForText(screenshotSchedules[2].name),
  "schedule-edit": async (page) => {
    await expect(page.getByRole("textbox").first()).toHaveValue(screenshotEditSchedule.name);
  },
  "project-members": waitForText(screenshotProjectMembers[0].displayName, false),
  "admin-users": waitForText(screenshotUsers[3].username),
  "admin-group-edit": waitForText(screenshotUsers[1].displayName, false),
  "admin-api-keys": waitForText(screenshotApiKeys[2].name),
  "admin-service-accounts": waitForText(screenshotServiceAccounts[2].name),
  "admin-user-resource-grants": async (page) => {
    await expect(page.getByRole("checkbox", { name: new RegExp(screenshotDefinitions[1].name) })).toBeChecked();
  }
};


/**
 * 画面 ID の場面を返す。
 * @param id manifest の画面 ID。
 * @returns 場面。
 * @throws {Error} 場面が無いとき（manifest に足して `screenshotScenes` に足し忘れた）。
 */
export function sceneFor(id: string): Scene {
  const scene = screenshotScenes[id];
  if (!scene) throw new Error(`${id}: 撮影の場面が無い。e2e/screenshots/scenes.ts の screenshotScenes に足す`);
  return scene;
}
