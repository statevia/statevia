/** サイドメニューの畳み状態を保存する cookie 名。サーバー描画で読み、初回のちらつきを防ぐ。 */
export const SIDE_NAV_COOKIE_KEY = "ui-sidenav";

/** `ui-theme` と同じ属性（1 年保持）。 */
const SIDE_NAV_COOKIE_ATTRS = "Path=/; Max-Age=31536000; SameSite=Lax";

/** レール表示（アイコンのみ）を表す cookie 値。 */
const RAIL_VALUE = "rail";

/** 展開表示を表す cookie 値。 */
const EXPANDED_VALUE = "expanded";

/**
 * cookie 値からレール表示かを決める。外部入力のため `"rail"` 以外はすべて展開として扱う。
 * @param rawValue cookie の生値。
 * @returns レール表示なら true。
 */
export function resolveSideNavCollapsed(rawValue: string | undefined): boolean {
  return rawValue === RAIL_VALUE;
}

/**
 * 畳み状態を cookie へ保存する（クライアント専用）。
 * @param collapsed レール表示なら true。
 */
export function storeSideNavCollapsed(collapsed: boolean): void {
  document.cookie = `${SIDE_NAV_COOKIE_KEY}=${collapsed ? RAIL_VALUE : EXPANDED_VALUE}; ${SIDE_NAV_COOKIE_ATTRS}`;
}
