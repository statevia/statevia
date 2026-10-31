"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { apiDelete, apiGet, apiPut } from "@/shared/api/client";
import {
  buildNotificationSmtpPutBody,
  notificationSmtpFormFromResponse,
  type NotificationSmtpFieldError,
  type TenantNotificationSmtp
} from "../notificationSmtp";

/** 通信失敗の種別。文言は画面側の辞書で出す。 */
export type NotificationSmtpFailure = "load" | "save" | "delete";

/** 送信中の操作。保存と削除でボタン文言を分ける。 */
export type NotificationSmtpPending = "save" | "delete";

/**
 * 通知 SMTP 画面の読込、検証、全体置換、削除。
 * パスワードは GET から状態へ戻さない。
 * @returns フォームと操作。
 */
export function useAdminNotificationSmtpPage() {
  const [loading, setLoading] = useState(true);
  const [pending, setPending] = useState<NotificationSmtpPending | null>(null);
  const pendingRef = useRef(false);
  const [configured, setConfigured] = useState(false);
  const [passwordConfigured, setPasswordConfigured] = useState(false);
  const [host, setHost] = useState("");
  const [port, setPort] = useState("");
  const [user, setUser] = useState("");
  const [password, setPassword] = useState("");
  const [defaultFrom, setDefaultFrom] = useState("");
  const [fieldError, setFieldError] = useState<NotificationSmtpFieldError | null>(null);
  const [failure, setFailure] = useState<NotificationSmtpFailure | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const applyResponse = useCallback((response: TenantNotificationSmtp) => {
    const form = notificationSmtpFormFromResponse(response);
    setConfigured(response.configured);
    setPasswordConfigured(response.passwordConfigured);
    setHost(form.host);
    setPort(form.port);
    setUser(form.user);
    setPassword(form.password);
    setDefaultFrom(form.defaultFrom);
  }, []);

  const load = useCallback(async () => {
    setFailure(null);
    setErrorMessage(null);
    try {
      const response = await apiGet<TenantNotificationSmtp>("/admin/notification-smtp");
      applyResponse(response);
    } catch (error) {
      setFailure("load");
      setErrorMessage(error instanceof Error ? error.message : null);
    } finally {
      setLoading(false);
    }
  }, [applyResponse]);

  useEffect(() => {
    void load();
  }, [load]);

  const save = useCallback(async () => {
    if (pendingRef.current) {
      return;
    }
    const result = buildNotificationSmtpPutBody({ host, port, user, password, defaultFrom });
    if (!result.ok) {
      setFieldError(result.field);
      setFailure(null);
      setErrorMessage(null);
      return;
    }

    pendingRef.current = true;
    setPending("save");
    setFieldError(null);
    setFailure(null);
    setErrorMessage(null);
    try {
      await apiPut<TenantNotificationSmtp>("/admin/notification-smtp", result.body);
      setPassword("");
      await load();
    } catch (error) {
      setPassword("");
      setFailure("save");
      setErrorMessage(error instanceof Error ? error.message : null);
    } finally {
      pendingRef.current = false;
      setPending(null);
    }
  }, [defaultFrom, host, load, password, port, user]);

  const confirmDelete = useCallback(async () => {
    if (!configured || pendingRef.current) {
      return;
    }
    pendingRef.current = true;
    setPending("delete");
    setFieldError(null);
    setFailure(null);
    setErrorMessage(null);
    try {
      await apiDelete("/admin/notification-smtp");
      setDeleteOpen(false);
      await load();
    } catch (error) {
      setFailure("delete");
      setErrorMessage(error instanceof Error ? error.message : null);
    } finally {
      pendingRef.current = false;
      setPending(null);
    }
  }, [configured, load]);

  return {
    loading,
    pending,
    configured,
    passwordConfigured,
    host,
    setHost,
    port,
    setPort,
    user,
    setUser,
    password,
    setPassword,
    defaultFrom,
    setDefaultFrom,
    fieldError,
    failure,
    errorMessage,
    deleteOpen,
    requestDelete: () => setDeleteOpen(true),
    cancelDelete: () => setDeleteOpen(false),
    reload: load,
    save,
    confirmDelete
  };
}
