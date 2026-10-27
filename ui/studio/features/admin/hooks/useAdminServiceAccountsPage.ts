"use client";

import { useCallback, useEffect, useState } from "react";
import { listServiceAccounts } from "../serviceAccountsApi";
import type { AdminServiceAccount } from "../types";

/** ServiceAccount 一覧画面の描画用状態。ReactNode は含まない。 */
export type AdminServiceAccountsPageModel = {
  /** 取得済みの ServiceAccount。失敗時は null（403 / 401 を空一覧に見せない）。 */
  serviceAccounts: AdminServiceAccount[] | null;
  /** 一覧の取得中か。 */
  loading: boolean;
  /** 一覧を取り直す。 */
  reload: () => Promise<void>;
};

/**
 * ServiceAccount 一覧を 1 回の GET で読む。
 * グループ名は表示しないため、グループ一覧は読まない（件数は `groupIds` から出す）。
 * loading / empty / error の出し分けは画面側に残す。
 * @returns 描画用の状態と再読込。
 */
export function useAdminServiceAccountsPage(): AdminServiceAccountsPageModel {
  const [serviceAccounts, setServiceAccounts] = useState<AdminServiceAccount[] | null>(null);
  const [loading, setLoading] = useState(true);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      setServiceAccounts(await listServiceAccounts());
    } catch {
      setServiceAccounts(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { serviceAccounts, loading, reload };
}
