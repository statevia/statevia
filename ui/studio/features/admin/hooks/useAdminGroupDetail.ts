"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { apiGet, apiPut } from "@/shared/api";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import type { AdminGroupDetail, AdminUserListItem, PermissionDefinitionDto } from "../types";

/** グループ詳細画面の振る舞い。 */
export type UseAdminGroupDetailOptions = {
  /** 編集対象のグループ ID。 */
  groupId: string;
};

/** グループ詳細の描画用状態。ReactNode は含まない。 */
export type AdminGroupDetailModel = {
  /** グループ。取得失敗時は null。 */
  group: AdminGroupDetail | null;
  /** メンバー候補のユーザー。 */
  users: AdminUserListItem[];
  /** テナント管理者と非推奨を除いた権限。 */
  assignablePermissions: PermissionDefinitionDto[];
  /** 選択中のユーザー ID。 */
  selectedUserIds: ReadonlySet<string>;
  /** 選択中の権限キー。 */
  selectedPermissionKeys: ReadonlySet<string>;
  /** 詳細の取得中か。 */
  loading: boolean;
  /** メンバー保存中か。 */
  savingMembers: boolean;
  /** 権限保存中か。 */
  savingPermissions: boolean;
  /** 取得・保存のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを閉じる。 */
  dismissToast: () => void;
  /**
   * メンバー候補の選択を切り替える。
   * @param userId ユーザー ID。
   */
  toggleUser: (userId: string) => void;
  /**
   * 権限の選択を切り替える。
   * @param permissionKey 権限キー。
   */
  togglePermission: (permissionKey: string) => void;
  /** 選択中のメンバーを保存する。 */
  saveMembers: () => void;
  /** 選択中の権限を保存する。 */
  savePermissions: () => void;
};

/**
 * グループのメンバーと権限の読み込み、選択、保存を持つ。
 *
 * 画面は戻り値をメンバー欄と権限欄へ渡す。loading と取得失敗の出し分けは画面側に残す。
 *
 * @param options 編集対象のグループ ID。
 * @returns 描画用の状態とコマンド。
 */
export function useAdminGroupDetail({ groupId }: UseAdminGroupDetailOptions): AdminGroupDetailModel {
  const [group, setGroup] = useState<AdminGroupDetail | null>(null);
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [permissions, setPermissions] = useState<PermissionDefinitionDto[]>([]);
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(new Set());
  const [selectedPermissionKeys, setSelectedPermissionKeys] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [savingMembers, setSavingMembers] = useState(false);
  const [savingPermissions, setSavingPermissions] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  const assignablePermissions = useMemo(
    () => permissions.filter((permission) => permission.permissionKey !== "tenant.admin" && !permission.isDeprecated),
    [permissions]
  );

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const [groupDetail, userList, permissionList] = await Promise.all([
        apiGet<AdminGroupDetail>(`/admin/groups/${groupId}`),
        apiGet<AdminUserListItem[]>("/admin/users"),
        apiGet<PermissionDefinitionDto[]>("/admin/permissions")
      ]);
      setGroup(groupDetail);
      setUsers(userList);
      setPermissions(permissionList);
      setSelectedUserIds(new Set(groupDetail.memberUserIds));
      setSelectedPermissionKeys(new Set(groupDetail.permissionKeys));
    } catch (error) {
      setToast(toToastError(error));
      setGroup(null);
    } finally {
      setLoading(false);
    }
  }, [groupId]);

  useEffect(() => {
    void load();
  }, [load]);

  const dismissToast = useCallback(() => {
    setToast(null);
  }, []);

  const toggleUser = useCallback((userId: string) => {
    setSelectedUserIds((current) => {
      const next = new Set(current);
      if (next.has(userId)) {
        next.delete(userId);
      } else {
        next.add(userId);
      }
      return next;
    });
  }, []);

  const togglePermission = useCallback((permissionKey: string) => {
    setSelectedPermissionKeys((current) => {
      const next = new Set(current);
      if (next.has(permissionKey)) {
        next.delete(permissionKey);
      } else {
        next.add(permissionKey);
      }
      return next;
    });
  }, []);

  const saveMembers = useCallback(() => {
    void (async () => {
      setSavingMembers(true);
      setToast(null);
      try {
        const updated = await apiPut<AdminGroupDetail>(`/admin/groups/${groupId}/members`, {
          userIds: [...selectedUserIds]
        });
        setGroup(updated);
        setSelectedUserIds(new Set(updated.memberUserIds));
      } catch (error) {
        setToast(toToastError(error));
      } finally {
        setSavingMembers(false);
      }
    })();
  }, [groupId, selectedUserIds]);

  const savePermissions = useCallback(() => {
    void (async () => {
      setSavingPermissions(true);
      setToast(null);
      try {
        const updated = await apiPut<AdminGroupDetail>(`/admin/groups/${groupId}/permissions`, {
          permissionKeys: [...selectedPermissionKeys]
        });
        setGroup(updated);
        setSelectedPermissionKeys(new Set(updated.permissionKeys));
      } catch (error) {
        setToast(toToastError(error));
      } finally {
        setSavingPermissions(false);
      }
    })();
  }, [groupId, selectedPermissionKeys]);

  return {
    group,
    users,
    assignablePermissions,
    selectedUserIds,
    selectedPermissionKeys,
    loading,
    savingMembers,
    savingPermissions,
    toast,
    dismissToast,
    toggleUser,
    togglePermission,
    saveMembers,
    savePermissions
  };
}
