import {
  definitionsUiTextJa,
  type DefinitionsFeatureUiText,
} from "@/features/definitions/i18n";
import {
  authUiTextJa,
  type AuthFeatureUiText,
} from "@/features/auth/i18n";
import {
  adminUiTextJa,
  type AdminFeatureUiText,
} from "@/features/admin/i18n";
import {
  dashboardUiTextJa,
  type DashboardFeatureUiText,
} from "@/features/dashboard/i18n";
import {
  definitionEditorUiTextJa,
  type DefinitionEditorFeatureUiText,
} from "@/features/definition-editor/i18n";
import {
  executionsUiTextJa,
  type ExecutionsFeatureUiText,
} from "@/features/executions/i18n";
import {
  projectsUiTextJa,
  type ProjectsFeatureUiText,
} from "@/features/projects/i18n";
import {
  schedulesUiTextJa,
  type SchedulesFeatureUiText,
} from "@/features/schedules/i18n";
import { actionCatalogUiTextJa, type ActionCatalogUiText } from "./actionCatalogUiText";

export type UiText = DefinitionsFeatureUiText &
  AuthFeatureUiText &
  AdminFeatureUiText &
  DashboardFeatureUiText &
  DefinitionEditorFeatureUiText &
  ExecutionsFeatureUiText &
  ProjectsFeatureUiText &
  SchedulesFeatureUiText & {
  actionCatalog: ActionCatalogUiText;
  actionLinks: {
    aria: {
      navigation: string;
    };

  },
  navigation: {
    dashboard: string;
    definitions: string;
    executions: string;
    schedules: string;
    projectMembers: string;
    projectCreate: string;
    adminUsers: string;
    adminGroups: string;
    adminApiKeys: string;
    adminServiceAccounts: string;
    adminActionBindings: string;
    adminNotificationSmtp: string;
    adminProjects: string;
    account: string;
    logout: string;
    /** サイドメニューのグループ見出し。キーは `NavGroupId` と一致させる。 */
    groups: {
      workflow: string;
      project: string;
      tenantAccess: string;
      tenantSettings: string;
    };
    /** サイドメニュー `<nav>` の aria-label。 */
    ariaSideNav: string;
    openMenu: string;
    closeMenu: string;
    collapse: string;
    expand: string;
    userMenu: string;

  },
  entities: {
    definition: string;
    execution: string;
    node: string;

  },
  lists: {
    executions: string;
    definitions: string;
    nodeCount: (count: number) => string;

  },
  actions: {
    load: string;
    loading: string;
    reload: string;
    cancel: string;
    resume: string;
    retry: string;
    save: string;
    sendEvent: string;
    openDetail: string;
    closeToast: string;
    viewList: string;
    viewGraph: string;

  },
  pagination: {
    prev: string;
    next: string;

  },
  labels: {
    status: string;
    nodeId: string;
    definitionId: string;
    displayId: string;
    resourceId: string;
    graphId: string;
    input: string;
    definitionEditor: string;

  },
  status: {
    cancelledDisplay: string;
    edgeTypeNext: string;
    edgeTypeResume: string;
    edgeTypeCancel: string;

  },
  pageState: {
    loading: string;
    empty: string;
    error: string;

  },
  errorPrefixes: {
    unauthorized401: string;
    forbidden403: string;
    conflict409: string;
    unprocessable422: string;
    server500: string;

  },
  tenantMissingBanner: {
    noticeParts: (
      loadLabel: string,
      cancelLabel: string,
      resumeLabel: string
    ) => {
      beforePrimaryEnv: string;
      betweenEnvs: string;
      afterSecondaryEnv: string;
    };

  };
};

export const uiText: UiText = {
  ...definitionsUiTextJa,
  ...authUiTextJa,
  ...adminUiTextJa,
  ...dashboardUiTextJa,
  ...definitionEditorUiTextJa,
  ...executionsUiTextJa,
  ...projectsUiTextJa,
  ...schedulesUiTextJa,
  actionCatalog: actionCatalogUiTextJa,
  actionLinks: {
    aria: {
      navigation: "画面導線",
    },

  },
  navigation: {
    dashboard: "ダッシュボード",
    definitions: "定義",
    executions: "実行",
    schedules: "定期実行",
    projectMembers: "メンバー",
    projectCreate: "プロジェクトを作成",
    adminUsers: "ユーザー管理",
    adminGroups: "グループ管理",
    adminApiKeys: "API キー",
    adminServiceAccounts: "サービスアカウント",
    adminActionBindings: "アクションバインディング",
    adminNotificationSmtp: "通知メールの設定",
    adminProjects: "プロジェクト",
    account: "アカウント",
    logout: "ログアウト",
    groups: {
      workflow: "ワークフロー",
      project: "プロジェクト",
      tenantAccess: "テナント管理：アクセス",
      tenantSettings: "テナント管理：連携・設定",
    },
    ariaSideNav: "メインメニュー",
    openMenu: "メニューを開く",
    closeMenu: "メニューを閉じる",
    collapse: "メニューを畳む",
    expand: "メニューを広げる",
    userMenu: "ユーザーメニュー",

  },
  entities: {
    definition: "定義",
    execution: "実行",
    node: "ノード",

  },
  lists: {
    executions: "実行一覧",
    definitions: "定義一覧",
    nodeCount: (count: number) => `${count} 件`,

  },
  actions: {
    load: "読み込む",
    loading: "読み込み中...",
    reload: "再読み込み",
    cancel: "キャンセル",
    resume: "再開",
    retry: "再試行",
    save: "保存",
    sendEvent: "イベント送信",
    openDetail: "詳細",
    closeToast: "通知を閉じる",
    viewList: "リスト",
    viewGraph: "グラフ",

  },
  pagination: {
    prev: "前へ",
    next: "次へ",

  },
  labels: {
    status: "ステータス",
    nodeId: "ノードID",
    definitionId: "定義ID",
    displayId: "ID",
    resourceId: "UUID",
    graphId: "グラフID",
    input: "入力データ",
    definitionEditor: "定義エディタ",

  },
  status: {
    // 表示上は Cancelled に統一。内部状態値は既存仕様のまま扱う。
    cancelledDisplay: "Cancelled",
    edgeTypeNext: "Next",
    edgeTypeResume: "Resume",
    edgeTypeCancel: "Cancel",

  },
  pageState: {
    loading: "読み込み中...",
    empty: "表示できるデータがありません。",
    error: "データの取得に失敗しました。",

  },
  errorPrefixes: {
    unauthorized401: "401 認証が必要です",
    forbidden403: "403 権限不足またはテナント未指定",
    conflict409: "409 状態競合",
    unprocessable422: "422 入力不正",
    server500: "500 サーバーエラー",

  },
  tenantMissingBanner: {
    noticeParts: (loadLabel: string, cancelLabel: string, resumeLabel: string) => ({
      beforePrimaryEnv: `テナントが未指定です。${loadLabel} / ${cancelLabel} / ${resumeLabel} が失敗する場合は `,
      betweenEnvs: " を設定するか、サーバーで ",
      afterSecondaryEnv: " を設定してください。",
    }),

  },
};

export const uiTextJa: UiText = uiText;

/** グループ管理画面用 i18n 切片。 */
export type AdminGroupsUiText = UiText["admin"]["groupManagement"];

/**
 * グループ管理画面用 i18n 切片を取り出す。
 */
export function getAdminGroupManagementUiText(text: UiText): AdminGroupsUiText {
  return text.admin.groupManagement;
}
