import type { ScheduleDetail, ScheduleListItem } from "@/features/schedules/types";
import { screenshotAdmin } from "./common";
import { screenshotDefinitions } from "./definitions";
import { screenshotServiceAccounts } from "./admin";

/** 定期実行の編集で開く定期実行の ID（毎月の請求締め）。 */
export const EDIT_SCHEDULE_ID = "00000000-0000-4000-8000-000000000602";

/** `GET /v1/schedules` の応答。定義は定義一覧の resourceId、実行するサービスアカウントは principalId で指す。 */
export const screenshotSchedules: ScheduleListItem[] = [
  {
    scheduleId: "00000000-0000-4000-8000-000000000601",
    name: "weekday-delay-check",
    definitionId: screenshotDefinitions[3].resourceId,
    definitionVersionId: null,
    runAsPrincipalId: screenshotServiceAccounts[0].principalId,
    cronExpression: "0 9 * * 1-5",
    timeZone: "Asia/Tokyo",
    overlapPolicy: "skip",
    enabled: true,
    nextFireAt: "2026-11-13T00:00:00Z",
    createdAt: "2026-10-21T00:00:00Z",
    updatedAt: "2026-11-12T00:00:02Z"
  },
  {
    scheduleId: EDIT_SCHEDULE_ID,
    name: "monthly-invoice-close",
    definitionId: screenshotDefinitions[2].resourceId,
    definitionVersionId: null,
    runAsPrincipalId: screenshotServiceAccounts[0].principalId,
    cronExpression: "0 18 28 * *",
    timeZone: "Asia/Tokyo",
    overlapPolicy: "skip",
    enabled: true,
    nextFireAt: "2026-11-28T09:00:00Z",
    createdAt: "2026-10-25T00:00:00Z",
    updatedAt: "2026-10-28T09:00:03Z"
  },
  {
    scheduleId: "00000000-0000-4000-8000-000000000603",
    name: "hourly-order-intake",
    definitionId: screenshotDefinitions[0].resourceId,
    definitionVersionId: null,
    runAsPrincipalId: screenshotServiceAccounts[1].principalId,
    cronExpression: "0 * * * *",
    timeZone: "UTC",
    overlapPolicy: "allow",
    enabled: false,
    nextFireAt: "2026-11-12T02:00:00Z",
    createdAt: "2026-11-01T00:00:00Z",
    updatedAt: "2026-11-08T03:00:00Z"
  }
];

/** `GET /v1/schedules/{scheduleId}` の応答（毎月の請求締め。入力データつき）。 */
export const screenshotEditSchedule: ScheduleDetail = {
  ...screenshotSchedules[1],
  createdByPrincipalId: screenshotAdmin.principalId,
  input: { closingDay: 28, currency: "JPY" }
};
