"use client";

import Link from "next/link";
import { ChevronDown, FolderOpen, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import type { NavGroup, NavGroupId, NavItem } from "./navItems";

/**
 * サイドメニューの表示モード。
 * - `expanded`: 見出し・ラベル付きの固定列（`lg` 以上）
 * - `rail`: アイコンだけの細い列（`lg` 以上で畳んだとき）
 * - `drawer`: 重ねて開くドロワー（`lg` 未満）。中身は `expanded` と同じで、畳むボタンを出さない
 */
export type SideNavMode = "expanded" | "rail" | "drawer";

/** グループ開閉を保存する localStorage キー。閉じたグループ ID の配列を JSON で持つ。 */
const CLOSED_GROUPS_STORAGE_KEY = "statevia.sidenav.groups";

type SideNavProps = Readonly<{
  /** 表示モード。 */
  mode: SideNavMode;
  /** 表示対象のグループ（`selectVisibleGroups` で絞り込み済み）。 */
  groups: readonly NavGroup[];
  /** 選択中の項目 ID。無ければ null。 */
  activeItemId: NavItem["id"] | null;
  /** 先頭に置く Project 切替。所属が無いときは null を渡す。 */
  projectSlot?: ReactNode;
  /** 畳む / 広げるボタン、およびレール表示の Project アイコンを押したとき。 */
  onToggleCollapse?: () => void;
  /** 項目を押したとき（ドロワーを閉じる用途）。 */
  onNavigate?: () => void;
}>;

/**
 * localStorage から閉じたグループを読む。プライベートモード等で使えない場合や不正値は空集合とする。
 * @returns 閉じたグループ ID の集合。
 */
function readClosedGroups(): Set<string> {
  try {
    const raw = globalThis.localStorage?.getItem(CLOSED_GROUPS_STORAGE_KEY);
    const parsed: unknown = raw ? JSON.parse(raw) : [];
    return new Set(Array.isArray(parsed) ? parsed.filter((value): value is string => typeof value === "string") : []);
  } catch {
    return new Set();
  }
}

/**
 * 閉じたグループを localStorage へ保存する。失敗しても保存しないだけとする。
 * @param closedGroups 閉じたグループ ID の集合。
 */
function storeClosedGroups(closedGroups: ReadonlySet<string>): void {
  try {
    globalThis.localStorage?.setItem(CLOSED_GROUPS_STORAGE_KEY, JSON.stringify([...closedGroups]));
  } catch {
    // 保存できない環境では開閉が再読み込みで戻るだけで、機能には影響しない。
  }
}

/**
 * グループ化したサイドメニュー。状態は開閉だけを持ち、表示モードと選択項目は親から受け取る。
 * 現在地を含むグループは、表示時と現在地が別グループへ移ったときに開く。開いた後は他のグループと同じく見出しで閉じられる。
 */
export function SideNav({ mode, groups, activeItemId, projectSlot, onToggleCollapse, onNavigate }: SideNavProps) {
  const uiText = useUiText();
  const [closedGroups, setClosedGroups] = useState<ReadonlySet<string>>(() => new Set());
  const activeGroupId = groups.find((group) => group.items.some((item) => item.id === activeItemId))?.id ?? null;

  // サーバー描画と一致させるため、保存値は描画後に読む。
  useEffect(() => {
    setClosedGroups(readClosedGroups());
  }, []);

  // 現在地のグループを開く。保存値は書き換えず、表示中の状態だけを変える（上の読み込み後に実行される）。
  useEffect(() => {
    if (activeGroupId === null) return;
    setClosedGroups((current) => {
      if (!current.has(activeGroupId)) return current;
      const next = new Set(current);
      next.delete(activeGroupId);
      return next;
    });
  }, [activeGroupId]);

  const toggleGroup = (groupId: NavGroupId) => {
    const next = new Set(closedGroups);
    if (next.has(groupId)) {
      next.delete(groupId);
    } else {
      next.add(groupId);
    }
    storeClosedGroups(next);
    setClosedGroups(next);
  };

  const isRail = mode === "rail";

  return (
    <nav
      aria-label={uiText.navigation.ariaSideNav}
      className={`flex h-full flex-col gap-2 overflow-y-auto py-3 text-sm ${isRail ? "items-center px-1" : "px-3"}`}
    >
      {projectSlot ? <ProjectSlot isRail={isRail} onExpand={onToggleCollapse}>{projectSlot}</ProjectSlot> : null}

      {groups.map((group) => {
        const isOpen = isRail || !closedGroups.has(group.id);
        const headingId = `sidenav-group-${group.id}`;
        const groupLabel = uiText.navigation.groups[group.id];
        return (
          <section key={group.id} aria-labelledby={headingId} className={`flex w-full flex-col gap-1 ${isRail ? "items-center border-t border-md-outline-variant pt-2" : ""}`}>
            {isRail ? (
              <h2 id={headingId} className="sr-only">{groupLabel}</h2>
            ) : (
              <h2 id={headingId}>
                <button
                  type="button"
                  aria-expanded={isOpen}
                  onClick={() => toggleGroup(group.id)}
                  className="flex w-full items-center justify-between rounded-md px-2 py-1 text-xs font-semibold text-md-on-surface-variant hover:bg-md-surface-container"
                >
                  <span>{groupLabel}</span>
                  <ChevronDown aria-hidden="true" className={`h-4 w-4 transition-transform ${isOpen ? "" : "-rotate-90"}`} />
                </button>
              </h2>
            )}
            {isOpen ? (
              <ul className={`flex flex-col gap-0.5 ${isRail ? "items-center" : ""}`}>
                {group.items.map((item) => (
                  <li key={item.id}>
                    <SideNavLink item={item} label={uiText.navigation[item.id]} isActive={item.id === activeItemId} isRail={isRail} onNavigate={onNavigate} />
                  </li>
                ))}
              </ul>
            ) : null}
          </section>
        );
      })}

      {mode === "drawer" || !onToggleCollapse ? null : (
        <div className={`mt-auto flex pt-2 ${isRail ? "justify-center" : "justify-end"}`}>
          <button
            type="button"
            onClick={onToggleCollapse}
            aria-label={isRail ? uiText.navigation.expand : uiText.navigation.collapse}
            title={isRail ? uiText.navigation.expand : uiText.navigation.collapse}
            className="rounded-md p-2 text-md-on-surface-variant hover:bg-md-surface-container"
          >
            {isRail ? <PanelLeftOpen aria-hidden="true" className="h-5 w-5" /> : <PanelLeftClose aria-hidden="true" className="h-5 w-5" />}
          </button>
        </div>
      )}
    </nav>
  );
}

type ProjectSlotProps = Readonly<{
  isRail: boolean;
  onExpand?: () => void;
  children: ReactNode;
}>;

/**
 * Project 切替の置き場所。レール表示では切替を置けないため、押すとメニューを広げるアイコンにする。
 */
function ProjectSlot({ isRail, onExpand, children }: ProjectSlotProps) {
  const uiText = useUiText();
  if (!isRail) {
    return <div className="border-b border-md-outline-variant pb-3">{children}</div>;
  }
  return (
    <button
      type="button"
      onClick={onExpand}
      aria-label={uiText.projects.switcher.label}
      title={uiText.projects.switcher.label}
      className="rounded-md p-2 text-md-on-surface-variant hover:bg-md-surface-container"
    >
      <FolderOpen aria-hidden="true" className="h-5 w-5" />
    </button>
  );
}

type SideNavLinkProps = Readonly<{
  item: NavItem;
  label: string;
  isActive: boolean;
  isRail: boolean;
  onNavigate?: () => void;
}>;

/**
 * サイドメニューの 1 リンク。レール表示ではラベルを `aria-label` とツールチップで持つ。
 */
function SideNavLink({ item, label, isActive, isRail, onNavigate }: SideNavLinkProps) {
  const Icon = item.icon;
  const stateClassName = isActive
    ? "bg-md-secondary-container font-semibold text-md-on-surface"
    : "text-md-on-surface-variant hover:bg-md-surface-container hover:text-md-on-surface";
  return (
    <Link
      href={item.href}
      onClick={onNavigate}
      aria-current={isActive ? "page" : undefined}
      aria-label={isRail ? label : undefined}
      title={isRail ? label : undefined}
      className={`flex items-center gap-2 rounded-md ${isRail ? "p-2" : "px-2 py-1.5"} ${stateClassName}`}
    >
      <Icon aria-hidden="true" className="h-5 w-5 shrink-0" />
      {isRail ? null : <span>{label}</span>}
    </Link>
  );
}
