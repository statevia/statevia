"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { apiGet, apiPatch, apiPost, apiPut } from "@/shared/api";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { isWithinMaxLength, matchesPattern } from "@/shared/lib/validation/primitives";
import { ASCII_LABEL_DISPLAY_NAME_MAX_LENGTH, ASCII_LABEL_PATTERN } from "@/shared/lib/validation/formRules";
import type { AdminUserListItem } from "../types";

type CreateUserBody = {
  username: string;
  email?: string;
  password: string;
  displayName?: string;
  isTenantAdmin: boolean;
};

/** ユーザー管理画面の描画用状態。ReactNode は含まない。 */
export type AdminUsersPageModel = {
  /** 取得済みユーザー。失敗時は null。 */
  users: AdminUserListItem[] | null;
  /** 一覧取得中か。 */
  loading: boolean;
  /** 作成・更新・取得のトースト。無ければ null。 */
  toast: ToastState | null;
  /** トーストを閉じる。 */
  dismissToast: () => void;
  /** 作成フォームのユーザー名。 */
  username: string;
  /** 作成フォームのメール。 */
  email: string;
  /** 作成フォームの初期パスワード。 */
  password: string;
  /** 作成フォームの表示名。 */
  displayName: string;
  /** テナント管理者として作成するか。 */
  isTenantAdmin: boolean;
  /** 作成送信中か。 */
  submitting: boolean;
  /**
   * ユーザー名を更新する。
   * @param value 入力値。
   */
  setUsername: (value: string) => void;
  /**
   * メールを更新する。
   * @param value 入力値。
   */
  setEmail: (value: string) => void;
  /**
   * 初期パスワードを更新する。
   * @param value 入力値。
   */
  setPassword: (value: string) => void;
  /**
   * 表示名を更新する。
   * @param value 入力値。
   */
  setDisplayName: (value: string) => void;
  /**
   * テナント管理者フラグを更新する。
   * @param value チェック状態。
   */
  setIsTenantAdmin: (value: boolean) => void;
  /**
   * ユーザーを作成し、成功時は一覧を取り直す。
   * @param event フォームの submit。
   */
  createUser: (event: FormEvent<HTMLFormElement>) => void;
  /**
   * 有効と無効を入れ替えて一覧を取り直す。
   * @param user 対象ユーザー。
   */
  toggleActive: (user: AdminUserListItem) => void;
  /**
   * パスワード更新ダイアログを開く。
   * @param user 対象ユーザー。
   */
  openPasswordDialog: (user: AdminUserListItem) => void;
  /** パスワード更新ダイアログを閉じ、入力を消す。 */
  closePasswordDialog: () => void;
  /** パスワード更新の対象。閉じていれば null。 */
  passwordTarget: AdminUserListItem | null;
  /** 新しいパスワード。 */
  newPassword: string;
  /** 確認用パスワード。 */
  confirmPassword: string;
  /** 確認欄が一致しないか。 */
  passwordMismatch: boolean;
  /** パスワード更新の送信中か。 */
  passwordSubmitting: boolean;
  /**
   * 新しいパスワードを更新し、不一致表示を消す。
   * @param value 入力値。
   */
  changeNewPassword: (value: string) => void;
  /**
   * 確認用パスワードを更新し、不一致表示を消す。
   * @param value 入力値。
   */
  changeConfirmPassword: (value: string) => void;
  /**
   * 確認欄が一致するときだけパスワードを更新する。
   * @param event ダイアログの submit。
   */
  updatePassword: (event: FormEvent<HTMLFormElement>) => void;
};

/**
 * テナントユーザーの一覧、作成、有効化、パスワード更新を持つ。
 *
 * 画面は戻り値を作成フォーム、一覧、パスワードダイアログへ渡す。loading / empty / error の出し分けは画面側に残す。
 *
 * @returns 描画用の状態とコマンド。
 */
export function useAdminUsersPage(): AdminUsersPageModel {
  const uiText = useUiText();
  const [users, setUsers] = useState<AdminUserListItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [isTenantAdmin, setIsTenantAdmin] = useState(false);
  const [passwordTarget, setPasswordTarget] = useState<AdminUserListItem | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordMismatch, setPasswordMismatch] = useState(false);
  const [passwordSubmitting, setPasswordSubmitting] = useState(false);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const list = await apiGet<AdminUserListItem[]>("/admin/users");
      setUsers(list);
    } catch (error) {
      setToast(toToastError(error));
      setUsers(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  const dismissToast = useCallback(() => {
    setToast(null);
  }, []);

  const createUser = useCallback(
    (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      void (async () => {
        setSubmitting(true);
        setToast(null);
        const trimmedEmail = email.trim();
        const body: CreateUserBody = {
          username: username.trim(),
          password,
          isTenantAdmin
        };
        if (trimmedEmail) {
          body.email = trimmedEmail;
        }
        const trimmedDisplay = displayName.trim();
        if (trimmedDisplay) {
          if (
            !isWithinMaxLength(trimmedDisplay, ASCII_LABEL_DISPLAY_NAME_MAX_LENGTH) ||
            !matchesPattern(trimmedDisplay, ASCII_LABEL_PATTERN)
          ) {
            setToast({ tone: "error", message: uiText.admin.users.displayNameInvalidFormat });
            setSubmitting(false);
            return;
          }
          body.displayName = trimmedDisplay;
        }

        try {
          await apiPost<AdminUserListItem>("/admin/users", body);
          setUsername("");
          setEmail("");
          setPassword("");
          setDisplayName("");
          setIsTenantAdmin(false);
          await loadUsers();
        } catch (error) {
          setToast(toToastError(error));
        } finally {
          setSubmitting(false);
        }
      })();
    },
    [displayName, email, isTenantAdmin, loadUsers, password, uiText.admin.users.displayNameInvalidFormat, username]
  );

  const toggleActive = useCallback(
    (user: AdminUserListItem) => {
      void (async () => {
        setToast(null);
        try {
          await apiPatch<AdminUserListItem>(`/admin/users/${user.userId}`, {
            isActive: !user.isActive
          });
          await loadUsers();
        } catch (error) {
          setToast(toToastError(error));
        }
      })();
    },
    [loadUsers]
  );

  const closePasswordDialog = useCallback(() => {
    setPasswordTarget(null);
    setNewPassword("");
    setConfirmPassword("");
    setPasswordMismatch(false);
  }, []);

  const openPasswordDialog = useCallback((user: AdminUserListItem) => {
    setPasswordTarget(user);
    setNewPassword("");
    setConfirmPassword("");
    setPasswordMismatch(false);
    setToast(null);
  }, []);

  const changeNewPassword = useCallback((value: string) => {
    setNewPassword(value);
    setPasswordMismatch(false);
  }, []);

  const changeConfirmPassword = useCallback((value: string) => {
    setConfirmPassword(value);
    setPasswordMismatch(false);
  }, []);

  const updatePassword = useCallback(
    (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (!passwordTarget) {
        return;
      }
      if (newPassword !== confirmPassword) {
        setPasswordMismatch(true);
        return;
      }

      setPasswordMismatch(false);
      setPasswordSubmitting(true);
      setToast(null);
      void (async () => {
        try {
          await apiPut(`/admin/users/${passwordTarget.userId}/password`, { newPassword });
          closePasswordDialog();
        } catch (error) {
          setToast(toToastError(error));
        } finally {
          setPasswordSubmitting(false);
        }
      })();
    },
    [closePasswordDialog, confirmPassword, newPassword, passwordTarget]
  );

  return {
    users,
    loading,
    toast,
    dismissToast,
    username,
    email,
    password,
    displayName,
    isTenantAdmin,
    submitting,
    setUsername,
    setEmail,
    setPassword,
    setDisplayName,
    setIsTenantAdmin,
    createUser,
    toggleActive,
    openPasswordDialog,
    closePasswordDialog,
    passwordTarget,
    newPassword,
    confirmPassword,
    passwordMismatch,
    passwordSubmitting,
    changeNewPassword,
    changeConfirmPassword,
    updatePassword
  };
}
