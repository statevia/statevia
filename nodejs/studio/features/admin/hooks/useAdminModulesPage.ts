"use client";

import { useCallback, useEffect, useState } from "react";
import { listAdminModules } from "../modulesApi";
import type { AdminModule } from "../types";

/** アクションモジュール一覧画面の描画用状態。ReactNode は含まない。 */
export type AdminModulesPageModel = {
  /** 取得済みの Module。失敗時は null（403 / 401 を空一覧に見せない）。 */
  modules: AdminModule[] | null;
  /** 一覧の取得中か。 */
  loading: boolean;
  /** 一覧を取り直す。読み直すだけで、Module の再読み込みは呼ばない。 */
  reload: () => Promise<void>;
};

/**
 * アクションモジュールのロード結果を 1 回の GET で読む。
 * loading / empty / error の出し分けは画面側に残す。
 * @returns 描画用の状態と再読込。
 */
export function useAdminModulesPage(): AdminModulesPageModel {
  const [modules, setModules] = useState<AdminModule[] | null>(null);
  const [loading, setLoading] = useState(true);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      setModules(await listAdminModules());
    } catch {
      setModules(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { modules, loading, reload };
}
