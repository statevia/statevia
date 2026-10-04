"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { apiDelete, apiGet, apiPost } from "@/shared/api";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { isWithinMaxLength, matchesPattern } from "@/shared/lib/validation/primitives";
import { ASCII_LABEL_DEFAULT_MAX_LENGTH, ASCII_LABEL_PATTERN } from "@/shared/lib/validation/formRules";
import type { AdminApiKeyListItem, CreatedAdminApiKey, PermissionDefinitionDto } from "../types";

type CreateApiKeyBody = {
  name: string;
  allowedScopes: string[];
  expiresAt?: string;
};

/** API キー画面の描画用状態。ReactNode は含まない。 */
export type AdminApiKeysPageModel = {
  /** 取得済みの API キー。失敗時は null。 */
  apiKeys: AdminApiKeyListItem[] | null;
  /** 一覧と権限の取得中か。 */
  loading: boolean;
  /** 発行・失効・取得のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを閉じる。 */
  dismissToast: () => void;
  /** 発行フォームの表示名。 */
  name: string;
  /** 有効期限の datetime-local 値。空なら無期限。 */
  expiresAt: string;
  /** 選択中のスコープ。 */
  selectedScopes: ReadonlySet<string>;
  /** テナント管理者と非推奨を除いた権限。 */
  assignablePermissions: PermissionDefinitionDto[];
  /** 発行送信中か。 */
  submitting: boolean;
  /** 失効中の API キー ID。無ければ null。 */
  revokingId: string | null;
  /** 直前に発行した平文キー。閉じていれば null。 */
  issuedKey: CreatedAdminApiKey | null;
  /** 平文キーをコピー済みか。 */
  copied: boolean;
  /**
   * 表示名を更新する。
   * @param value 入力値。
   */
  setName: (value: string) => void;
  /**
   * 有効期限を更新する。
   * @param value datetime-local の値。
   */
  setExpiresAt: (value: string) => void;
  /**
   * スコープの選択を切り替える。
   * @param permissionKey 権限キー。
   */
  toggleScope: (permissionKey: string) => void;
  /**
   * API キーを発行し、成功時は平文を一度だけ保持する。
   * @param event フォームの submit。
   */
  createApiKey: (event: FormEvent<HTMLFormElement>) => void;
  /**
   * 有効なキーを失効し、一覧を取り直す。
   * @param apiKey 対象キー。
   */
  revokeApiKey: (apiKey: AdminApiKeyListItem) => void;
  /** 平文キーをクリップボードへコピーする。 */
  copyPlainKey: () => void;
  /** 発行直後の平文表示を閉じる。 */
  dismissIssuedKey: () => void;
};

/**
 * API キーの一覧、発行、失効を持つ。
 *
 * 画面は戻り値を発行表示、作成フォーム、一覧へ渡す。loading / empty / error の出し分けは画面側に残す。
 *
 * @returns 描画用の状態とコマンド。
 */
export function useAdminApiKeysPage(): AdminApiKeysPageModel {
  const uiText = useUiText();
  const [apiKeys, setApiKeys] = useState<AdminApiKeyListItem[] | null>(null);
  const [permissions, setPermissions] = useState<PermissionDefinitionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [name, setName] = useState("");
  const [expiresAt, setExpiresAt] = useState("");
  const [selectedScopes, setSelectedScopes] = useState<Set<string>>(new Set());
  const [issuedKey, setIssuedKey] = useState<CreatedAdminApiKey | null>(null);
  const [copied, setCopied] = useState(false);

  const assignablePermissions = useMemo(
    () => permissions.filter((permission) => permission.permissionKey !== "tenant.admin" && !permission.isDeprecated),
    [permissions]
  );

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const [keys, permissionList] = await Promise.all([
        apiGet<AdminApiKeyListItem[]>("/admin/api-keys"),
        apiGet<PermissionDefinitionDto[]>("/admin/permissions")
      ]);
      setApiKeys(keys);
      setPermissions(permissionList);
    } catch (error) {
      setToast(toToastError(error));
      setApiKeys(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const dismissToast = useCallback(() => {
    setToast(null);
  }, []);

  const toggleScope = useCallback((permissionKey: string) => {
    setSelectedScopes((current) => {
      const next = new Set(current);
      if (next.has(permissionKey)) {
        next.delete(permissionKey);
      } else {
        next.add(permissionKey);
      }
      return next;
    });
  }, []);

  const createApiKey = useCallback(
    (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      void (async () => {
        setSubmitting(true);
        setToast(null);
        setCopied(false);
        const trimmedName = name.trim();
        if (
          !isWithinMaxLength(trimmedName, ASCII_LABEL_DEFAULT_MAX_LENGTH) ||
          !matchesPattern(trimmedName, ASCII_LABEL_PATTERN)
        ) {
          setToast({ tone: "error", message: uiText.admin.apiKeys.nameInvalidFormat });
          setSubmitting(false);
          return;
        }
        const body: CreateApiKeyBody = {
          name: trimmedName,
          allowedScopes: [...selectedScopes]
        };
        if (expiresAt.trim()) {
          body.expiresAt = new Date(expiresAt).toISOString();
        }

        try {
          const created = await apiPost<CreatedAdminApiKey>("/admin/api-keys", body);
          setIssuedKey(created);
          setName("");
          setExpiresAt("");
          setSelectedScopes(new Set());
          await load();
        } catch (error) {
          setToast(toToastError(error));
        } finally {
          setSubmitting(false);
        }
      })();
    },
    [expiresAt, load, name, selectedScopes, uiText.admin.apiKeys.nameInvalidFormat]
  );

  const revokeApiKey = useCallback(
    (apiKey: AdminApiKeyListItem) => {
      void (async () => {
        setRevokingId(apiKey.apiKeyId);
        setToast(null);
        try {
          await apiDelete(`/admin/api-keys/${apiKey.apiKeyId}`);
          await load();
        } catch (error) {
          setToast(toToastError(error));
        } finally {
          setRevokingId(null);
        }
      })();
    },
    [load]
  );

  const copyPlainKey = useCallback(() => {
    if (!issuedKey) {
      return;
    }
    void (async () => {
      try {
        await navigator.clipboard.writeText(issuedKey.plainKey);
        setCopied(true);
      } catch {
        setToast({ tone: "error", message: uiText.executionTimeline.errorUnknown });
      }
    })();
  }, [issuedKey, uiText.executionTimeline.errorUnknown]);

  const dismissIssuedKey = useCallback(() => {
    setIssuedKey(null);
    setCopied(false);
  }, []);

  return {
    apiKeys,
    loading,
    toast,
    dismissToast,
    name,
    expiresAt,
    selectedScopes,
    assignablePermissions,
    submitting,
    revokingId,
    issuedKey,
    copied,
    setName,
    setExpiresAt,
    toggleScope,
    createApiKey,
    revokeApiKey,
    copyPlainKey,
    dismissIssuedKey
  };
}
