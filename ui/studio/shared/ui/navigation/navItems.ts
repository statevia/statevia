import {
  FileCode,
  FolderKanban,
  KeyRound,
  LayoutDashboard,
  Mail,
  Play,
  Plug,
  UserCog,
  Users,
  UsersRound,
  type LucideIcon,
} from "lucide-react";

/**
 * ナビ項目の表示条件。
 * - `all`: ログイン済みの全員
 * - `projectAdmin`: 選択中 Project で `admin`
 * - `tenantAdmin`: テナント管理者
 *
 * 表示制御は見た目だけであり、認可の正本は `app/admin/layout.tsx` と Service API。
 */
export type NavVisibility = "all" | "projectAdmin" | "tenantAdmin";

/** グループ ID。i18n `navigation.groups.<id>` と一致させる。 */
export type NavGroupId = "workflow" | "project" | "tenantAccess" | "tenantSettings";

/** 項目 ID。i18n `navigation.<id>` と一致させる。 */
export type NavItemId =
  | "dashboard"
  | "definitions"
  | "executions"
  | "projectMembers"
  | "adminUsers"
  | "adminGroups"
  | "adminApiKeys"
  | "adminProjects"
  | "adminActionBindings"
  | "adminNotificationSmtp";

/** サイドメニューの 1 項目。 */
export type NavItem = Readonly<{
  /** i18n キーを兼ねる ID。 */
  id: NavItemId;
  /** 遷移先。 */
  href: string;
  /** 選択状態の判定に使う接頭辞。完全一致または `prefix + "/"` で始まるときに一致とみなす。 */
  matchPrefixes: readonly string[];
  /** 表示条件。 */
  visibility: NavVisibility;
  /** 項目アイコン。 */
  icon: LucideIcon;
}>;

/** サイドメニューのグループ。表示順は配列順。 */
export type NavGroup = Readonly<{
  /** i18n キーを兼ねる ID。 */
  id: NavGroupId;
  /** グループ内の項目。表示順は配列順。 */
  items: readonly NavItem[];
}>;

/**
 * サイドメニューの唯一の定義。画面を追加するときはここへ追記する。
 * 既存 3 種の `NavVisibility` で表せない表示条件は、種類を追加して `navSelection.ts` の判定表を更新する。
 */
export const NAV_GROUPS: readonly NavGroup[] = [
  {
    id: "workflow",
    items: [
      { id: "dashboard", href: "/dashboard", matchPrefixes: ["/dashboard"], visibility: "all", icon: LayoutDashboard },
      { id: "definitions", href: "/definitions", matchPrefixes: ["/definitions"], visibility: "all", icon: FileCode },
      { id: "executions", href: "/executions", matchPrefixes: ["/executions"], visibility: "all", icon: Play },
    ],
  },
  {
    id: "project",
    items: [
      {
        id: "projectMembers",
        href: "/projects/members",
        matchPrefixes: ["/projects/members"],
        visibility: "projectAdmin",
        icon: Users,
      },
    ],
  },
  {
    id: "tenantAccess",
    items: [
      { id: "adminUsers", href: "/admin/users", matchPrefixes: ["/admin/users"], visibility: "tenantAdmin", icon: UserCog },
      { id: "adminGroups", href: "/admin/groups", matchPrefixes: ["/admin/groups"], visibility: "tenantAdmin", icon: UsersRound },
      { id: "adminApiKeys", href: "/admin/api-keys", matchPrefixes: ["/admin/api-keys"], visibility: "tenantAdmin", icon: KeyRound },
    ],
  },
  {
    id: "tenantSettings",
    items: [
      {
        id: "adminProjects",
        href: "/admin/projects",
        matchPrefixes: ["/admin/projects"],
        visibility: "tenantAdmin",
        icon: FolderKanban,
      },
      {
        id: "adminActionBindings",
        href: "/admin/action-bindings",
        matchPrefixes: ["/admin/action-bindings"],
        visibility: "tenantAdmin",
        icon: Plug,
      },
      {
        id: "adminNotificationSmtp",
        href: "/admin/notification-smtp",
        matchPrefixes: ["/admin/notification-smtp"],
        visibility: "tenantAdmin",
        icon: Mail,
      },
    ],
  },
];
