import type { NavGroup, NavItem, NavVisibility } from "./navItems";

/** 表示条件の判定に使う利用者の状態。 */
export type NavVisibilityContext = Readonly<{
  /** テナント管理者か（サーバー側の `auth/me` 由来。失敗時は false）。 */
  isTenantAdmin: boolean;
  /** 選択中 Project で `admin` か。Project 一覧の未ロード時は false。 */
  isProjectAdmin: boolean;
  /** `projects.create` を持つか。グループ権限の未ロード時は false。 */
  canCreateProject: boolean;
}>;

const visibilityRules: Record<NavVisibility, (context: NavVisibilityContext) => boolean> = {
  all: () => true,
  projectAdmin: (context) => context.isProjectAdmin,
  projectCreate: (context) => context.canCreateProject,
  tenantAdmin: (context) => context.isTenantAdmin,
};

/**
 * 表示条件で項目を絞り、項目が 0 件になったグループを除く。
 * @param groups ナビ定義。
 * @param context 利用者の状態。
 * @returns 表示対象のグループ（順序は定義どおり）。
 */
export function selectVisibleGroups(groups: readonly NavGroup[], context: NavVisibilityContext): NavGroup[] {
  return groups
    .map((group) => ({ ...group, items: group.items.filter((item) => visibilityRules[item.visibility](context)) }))
    .filter((group) => group.items.length > 0);
}

/**
 * 接頭辞が pathname に一致するときの一致長を返す。
 * `/definitions-foo` を `/definitions` に一致させないよう、境界は完全一致か `prefix + "/"` に限る。
 * @param prefix 判定する接頭辞。
 * @param pathname 現在の URL パス。
 * @returns 一致長。不一致は 0。
 */
function matchLength(prefix: string, pathname: string): number {
  return pathname === prefix || pathname.startsWith(`${prefix}/`) ? prefix.length : 0;
}

/**
 * 現在の URL に対応する項目 ID を最長接頭辞一致で決める。
 * @param groups 表示対象のグループ。
 * @param pathname 現在の URL パス。
 * @returns 選択項目の ID。どの項目にも一致しなければ null。
 */
export function resolveActiveItemId(groups: readonly NavGroup[], pathname: string): NavItem["id"] | null {
  const best = groups
    .flatMap((group) => group.items)
    .map((item) => ({ id: item.id, length: Math.max(...item.matchPrefixes.map((prefix) => matchLength(prefix, pathname))) }))
    .filter((candidate) => candidate.length > 0)
    .reduce<{ id: NavItem["id"]; length: number } | null>(
      (current, candidate) => (current === null || candidate.length > current.length ? candidate : current),
      null,
    );
  return best?.id ?? null;
}
