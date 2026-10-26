"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import { fetchAuthMe } from "./fetchAuthMe";

/** 実行の開始・取消・再開に要るグループ権限。Service API の `executions.write`。 */
export const EXECUTIONS_WRITE_PERMISSION = "executions.write";

/** 定義の作成・更新に要るグループ権限。Service API の `definitions.write`。 */
export const DEFINITIONS_WRITE_PERMISSION = "definitions.write";

/** Project の作成に要るグループ権限。Service API の `projects.create`。`tenant.admin` は catalog 展開で持つ。 */
export const PROJECTS_CREATE_PERMISSION = "projects.create";

/**
 * 呼び出し元のグループ権限（semantic permission key）。
 * @remarks ボタンの表示を決めるためだけに使う。認可の正本は Service API で、ここを根拠に操作を許可しない。
 */
export type PermissionsContextValue = {
  /** `/api/auth/me` の取得が終わったか。失敗しても true になる。 */
  loaded: boolean;
  /**
   * 権限キーを持つか。
   * @param permissionKey 例: `executions.write`。
   * @returns 取得前・取得失敗時は false。
   */
  has: (permissionKey: string) => boolean;
};

const PermissionsContext = createContext<PermissionsContextValue | null>(null);

type PermissionsProviderProps = {
  children: ReactNode;
  /** 指定時は API を呼ばず、この権限キーを使う。テスト用。 */
  initialPermissions?: readonly string[];
};

/**
 * 権限キーの集合からコンテキスト値を作る。
 * @param permissions 権限キー。
 * @param loaded 取得済みか。
 * @returns コンテキスト値。
 */
function usePermissionsValue(permissions: readonly string[], loaded: boolean): PermissionsContextValue {
  return useMemo(() => {
    const keys = new Set(permissions);
    return { loaded, has: (permissionKey: string) => keys.has(permissionKey) };
  }, [loaded, permissions]);
}

/**
 * `/api/auth/me` から権限を読むプロバイダ。ログイン画面では取得しない。
 * 取得に失敗したときは権限なしとして扱う（ボタンを出さない側に倒す）。
 * @param props 子要素。
 * @returns プロバイダ。
 */
function LivePermissionsProvider({ children }: Readonly<{ children: ReactNode }>) {
  const pathname = usePathname();
  const [permissions, setPermissions] = useState<readonly string[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (pathname === "/login") {
      setLoaded(true);
      return;
    }
    let cancelled = false;
    void fetchAuthMe()
      .then((me) => {
        if (!cancelled) setPermissions(me?.permissions ?? []);
      })
      .catch(() => {
        if (!cancelled) setPermissions([]);
      })
      .finally(() => {
        if (!cancelled) setLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [pathname]);

  const value = usePermissionsValue(permissions, loaded);
  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>;
}

/**
 * 固定の権限キーを渡すプロバイダ。API は呼ばない。
 * @param props 権限キーと子要素。
 * @returns プロバイダ。
 */
function StaticPermissionsProvider({
  children,
  initialPermissions,
}: Readonly<{ children: ReactNode; initialPermissions: readonly string[] }>) {
  const value = usePermissionsValue(initialPermissions, true);
  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>;
}

/**
 * 呼び出し元のグループ権限を画面で共有する。
 * @param props `initialPermissions` があるときは API を呼ばない。
 * @returns プロバイダ。
 */
export function PermissionsProvider({ children, initialPermissions }: Readonly<PermissionsProviderProps>) {
  if (initialPermissions) {
    return <StaticPermissionsProvider initialPermissions={initialPermissions}>{children}</StaticPermissionsProvider>;
  }
  return <LivePermissionsProvider>{children}</LivePermissionsProvider>;
}

/**
 * グループ権限コンテキストを返す。
 * @returns 取得状態と権限判定。
 * @throws PermissionsProvider の外で呼んだとき。
 */
export function usePermissions(): PermissionsContextValue {
  const value = useContext(PermissionsContext);
  if (!value) {
    throw new Error("PermissionsProvider が必要です");
  }
  return value;
}
