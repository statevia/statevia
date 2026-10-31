"use client";

import { useCallback, useEffect, useState } from "react";
import { apiGet } from "@/shared/api";
import type { AdminGroupListItem } from "../types";

/** ServiceAccount フォームの所属グループ選択。作成と詳細で共通。 */
export type ServiceAccountGroupSelection = {
  /** 選べるグループ。読込前と失敗時は null。 */
  groups: AdminGroupListItem[] | null;
  /** グループ一覧の取得に失敗したか。 */
  groupsLoadFailed: boolean;
  /** 選択中のグループ ID。 */
  selectedGroupIds: ReadonlySet<string>;
  /**
   * 選択をまとめて置き換える（送信後のクリアや現在値への戻し）。
   * @param groupIds 新しい選択。
   */
  replaceSelectedGroupIds: (groupIds: Iterable<string>) => void;
  /**
   * グループの選択を切り替える。
   * @param groupId グループ ID。
   */
  toggleGroup: (groupId: string) => void;
};

/**
 * `GET /v1/admin/groups` をマウント時に 1 回だけ読み、選択状態を持つ。
 * 失敗しても自動では再試行しない（フォーム側で理由を出す）。アンマウント後の結果は捨てる。
 * @returns グループ一覧と選択状態。
 */
export function useServiceAccountGroupSelection(): ServiceAccountGroupSelection {
  const [groups, setGroups] = useState<AdminGroupListItem[] | null>(null);
  const [groupsLoadFailed, setGroupsLoadFailed] = useState(false);
  const [selectedGroupIds, setSelectedGroupIds] = useState<ReadonlySet<string>>(new Set());

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const rows = await apiGet<AdminGroupListItem[]>("/admin/groups");
        if (!cancelled) setGroups(rows);
      } catch {
        if (!cancelled) setGroupsLoadFailed(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const replaceSelectedGroupIds = useCallback((groupIds: Iterable<string>) => {
    setSelectedGroupIds(new Set(groupIds));
  }, []);

  const toggleGroup = useCallback((groupId: string) => {
    setSelectedGroupIds((current) => {
      const next = new Set(current);
      if (next.has(groupId)) {
        next.delete(groupId);
      } else {
        next.add(groupId);
      }
      return next;
    });
  }, []);

  return { groups, groupsLoadFailed, selectedGroupIds, replaceSelectedGroupIds, toggleGroup };
}
