import { PROJECT_IDS, screenshotAdmin, screenshotProjects } from "./data/common";
import {
  ORDER_DEFINITION_ID,
  screenshotDefinitionsPage,
  screenshotNodesSchema,
  screenshotOrderDefinition
} from "./data/definitions";
import {
  WAITING_EXECUTION_ID,
  screenshotExecutionCounts,
  screenshotExecutionsPage,
  screenshotOrderGraphDefinition,
  screenshotWaitingExecution,
  screenshotWaitingExecutionEvents,
  screenshotWaitingExecutionGraph
} from "./data/executions";
import {
  GROUP_IDS,
  RESOURCE_GRANT_USER_ID,
  screenshotApiKeys,
  screenshotGroups,
  screenshotOperatorsGroup,
  screenshotPermissions,
  screenshotServiceAccounts,
  screenshotUserResourceGrants,
  screenshotUsers
} from "./data/admin";
import { screenshotMemberCandidates, screenshotProjectMembers } from "./data/projects";
import { EDIT_SCHEDULE_ID, screenshotEditSchedule, screenshotSchedules } from "./data/schedules";

/**
 * 1 つの API の応答を返す関数。
 * @param query 要求のクエリ。一覧の limit や絞り込みで応答を変えるときに読む。
 * @returns JSON の本文。
 */
export type ScreenshotResponder = (query: URLSearchParams) => unknown;

/**
 * 画面が呼ぶ API の応答。キーは `/api/core` を除いたパス（クエリは含めない）。GET だけを扱う。
 * @remarks 実行の SSE（`.../stream`）は撮影の仕組みが空のストリームで答えるので、ここには書かない。
 */
export type ScreenshotResponses = Readonly<Record<string, ScreenshotResponder>>;

/** 撮る画面 1 つ。 */
export type ScreenshotEntry = {
  /** 画面の ID。画像のファイル名（`docs/images/screenshots/<id>.webp`）にも使う。 */
  id: string;
  /** 開くパス（実際の ID を埋めたもの）。 */
  route: string;
  /** 画像を参照する文書（リポジトリ相対パス）。 */
  docs: readonly string[];
  /** 代替テキスト。何が写っているかを書く。 */
  alt: { ja: string; en: string };
  /** 画面が呼ぶ API の応答。全画面に共通の応答（`commonResponses`）に足す。 */
  responses: ScreenshotResponses;
};

/** 撮影でログインしている利用者。`GET /v1/auth/me` の応答としてブラウザ側とサーバー側の両方で返す。 */
export const screenshotUser = screenshotAdmin;

/** すべての画面が呼ぶ API の応答（画面の外枠の所属 Project）。 */
export const commonResponses: ScreenshotResponses = {
  "/projects": () => screenshotProjects
};

const definitionsPage: ScreenshotResponder = (query) => screenshotDefinitionsPage(Number(query.get("limit") ?? "20"));

const waitingExecutionResponses: ScreenshotResponses = {
  [`/executions/${WAITING_EXECUTION_ID}`]: () => screenshotWaitingExecution,
  [`/executions/${WAITING_EXECUTION_ID}/graph`]: () => screenshotWaitingExecutionGraph,
  [`/executions/${WAITING_EXECUTION_ID}/events`]: () => screenshotWaitingExecutionEvents,
  [`/graphs/${ORDER_DEFINITION_ID}`]: () => screenshotOrderGraphDefinition
};

const README = "README.md";
const UI_GUIDE = "docs/guides/ui-user-guide.md";

/**
 * 文書に載せる画面の一覧。README 用の 2 枚と、UI ガイドの節ごとの代表画面。
 * @remarks 画面の状態（グラフ表示への切り替え、ダイアログを開くなど）は撮影の spec が `id` ごとに作る。
 * 応答はすべて架空のデータで、資格情報・トークン・実在の個人情報を含めない。
 */
export const screenshotManifest: readonly ScreenshotEntry[] = [
  {
    id: "execution-graph",
    route: `/executions/${WAITING_EXECUTION_ID}`,
    docs: [README],
    alt: {
      ja: "実行詳細のグラフ表示。受注の並列処理で、分岐の片方の出荷が完了し、もう片方が在庫移動のイベントを待っている",
      en: "Execution graph of a parallel order workflow: one branch has shipped and the other is waiting for a stock transfer event"
    },
    responses: waitingExecutionResponses
  },
  {
    id: "definition-editor",
    route: `/definitions/${ORDER_DEFINITION_ID}/edit`,
    docs: [README],
    alt: {
      ja: "定義エディタのグラフ表示。受付・検証・並列出荷・合流・通知のノードが矢印でつながっている",
      en: "Definition editor in graph view, showing intake, validation, parallel shipping, join and notification nodes"
    },
    responses: {
      [`/definitions/${ORDER_DEFINITION_ID}`]: () => screenshotOrderDefinition,
      "/definitions/schema/nodes": () => screenshotNodesSchema
    }
  },
  {
    id: "dashboard",
    route: "/dashboard",
    docs: [UI_GUIDE],
    alt: {
      ja: "ダッシュボード。実行中・待機中・直近 24 時間の失敗と完了の件数、対応が必要な実行、直近の実行が並ぶ",
      en: "Dashboard with counts of running, waiting, failed and completed executions, executions needing attention, and recent executions"
    },
    responses: {
      "/executions": screenshotExecutionsPage,
      "/executions/counts": screenshotExecutionCounts
    }
  },
  {
    id: "definitions",
    route: "/definitions",
    docs: [UI_GUIDE],
    alt: {
      ja: "定義一覧。定義ごとに名前・登録日時・最後の実行の状態が並ぶ",
      en: "Definition list with each definition's name, creation time and the status of its last execution"
    },
    responses: { "/definitions": definitionsPage }
  },
  {
    id: "executions",
    route: "/executions",
    docs: [UI_GUIDE],
    alt: {
      ja: "実行一覧。状態ごとの件数と、実行中・完了・失敗・キャンセルの実行が並ぶ",
      en: "Execution list with counts by status and running, completed, failed and canceled executions"
    },
    responses: {
      "/executions": screenshotExecutionsPage,
      "/executions/counts": screenshotExecutionCounts
    }
  },
  {
    id: "execution-send-event",
    route: `/executions/${WAITING_EXECUTION_ID}`,
    docs: [UI_GUIDE],
    alt: {
      ja: "実行詳細のイベント送信ダイアログ。待機中のノードに StockTransferred を送る",
      en: "Send event dialog on the execution detail page, sending StockTransferred to the waiting node"
    },
    responses: waitingExecutionResponses
  },
  {
    id: "schedules",
    route: "/schedules",
    docs: [UI_GUIDE],
    alt: {
      ja: "定期実行の一覧。名前・定義・cron・タイムゾーン・次の実行・有効かが並ぶ",
      en: "Schedule list with name, definition, cron, time zone, next run and whether each schedule is enabled"
    },
    responses: {
      "/schedules": () => screenshotSchedules,
      "/definitions": definitionsPage
    }
  },
  {
    id: "schedule-edit",
    route: `/schedules/${EDIT_SCHEDULE_ID}`,
    docs: [UI_GUIDE],
    alt: {
      ja: "定期実行の編集。定義・cron・タイムゾーン・同時実行・実行するサービスアカウント・入力データの欄がある",
      en: "Schedule edit form with definition, cron, time zone, overlap policy, run-as service account and input fields"
    },
    responses: {
      [`/schedules/${EDIT_SCHEDULE_ID}`]: () => screenshotEditSchedule,
      "/admin/service-accounts": () => screenshotServiceAccounts,
      "/definitions": definitionsPage
    }
  },
  {
    id: "project-members",
    route: "/projects/members",
    docs: [UI_GUIDE],
    alt: {
      ja: "プロジェクトメンバー。ユーザーとサービスアカウントがロールつきで並び、追加の欄がある",
      en: "Project members page listing users and service accounts with their roles, and a form to add members"
    },
    responses: {
      [`/projects/${PROJECT_IDS.orders}/members`]: () => screenshotProjectMembers,
      [`/projects/${PROJECT_IDS.orders}/member-candidates`]: () => screenshotMemberCandidates
    }
  },
  {
    id: "admin-users",
    route: "/admin/users",
    docs: [UI_GUIDE],
    alt: {
      ja: "ユーザー管理。ユーザーごとに表示名・ユーザー名・メール・管理者か・有効かが並ぶ",
      en: "User management with each user's display name, username, email, admin flag and active status"
    },
    responses: { "/admin/users": () => screenshotUsers }
  },
  {
    id: "admin-group-edit",
    route: `/admin/groups/${GROUP_IDS.operators}`,
    docs: [UI_GUIDE],
    alt: {
      ja: "グループの編集。operators グループのメンバーと権限を選ぶ",
      en: "Group edit page for the operators group, choosing members and permissions"
    },
    responses: {
      [`/admin/groups/${GROUP_IDS.operators}`]: () => screenshotOperatorsGroup,
      "/admin/users": () => screenshotUsers,
      "/admin/permissions": () => screenshotPermissions
    }
  },
  {
    id: "admin-api-keys",
    route: "/admin/api-keys",
    docs: [UI_GUIDE],
    alt: {
      ja: "API キー。発行の欄と、キーの先頭・権限・有効期限・最終利用が並ぶ一覧",
      en: "API keys page with an issue form and a list of key prefixes, scopes, expiry and last use"
    },
    responses: {
      "/admin/api-keys": () => screenshotApiKeys,
      "/admin/permissions": () => screenshotPermissions
    }
  },
  {
    id: "admin-service-accounts",
    route: "/admin/service-accounts",
    docs: [UI_GUIDE],
    alt: {
      ja: "サービスアカウント。作成の欄と、名前・所属グループ・有効かが並ぶ一覧",
      en: "Service accounts page with a create form and a list of names, groups and active status"
    },
    responses: {
      "/admin/service-accounts": () => screenshotServiceAccounts,
      "/admin/groups": () => screenshotGroups
    }
  },
  {
    id: "admin-user-resource-grants",
    route: `/admin/users/${RESOURCE_GRANT_USER_ID}/resource-grants`,
    docs: [UI_GUIDE],
    alt: {
      ja: "ユーザーの操作できる定義。Project と定義を選んで、閲覧・開始・再開・キャンセルできる範囲を絞る",
      en: "Resource grants for a user, choosing the projects and definitions they can view, start, resume and cancel"
    },
    responses: {
      [`/admin/users/${RESOURCE_GRANT_USER_ID}/resource-grants`]: () => screenshotUserResourceGrants,
      "/admin/projects": () => screenshotProjects,
      "/definitions": definitionsPage
    }
  }
];
