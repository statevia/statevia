"use client";

import { useCallback, useRef, useState, type FormEvent } from "react";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { createProject } from "../api";
import {
  buildCreateProjectBody,
  isSlugConflictError,
  type ProjectCreateFieldErrors,
} from "../projectCreate";
import type { ProjectSummary } from "../types";

/**
 * 作成成功後に呼び出し側が行う後処理（所属の再取得と選択など）。
 * @param project 作成した Project。
 * @returns 後処理が成功したら true。false なら「作成済み・再読み込み」を知らせる。
 */
export type ProjectCreatedHandler = (project: ProjectSummary) => Promise<boolean>;

/**
 * Project 作成フォームの入力、検証、送信。
 * 送信中は二重送信しない。失敗時に自動で再送しない（201 のあと再送すると slug 重複になるため）。
 * @param onCreated 作成成功後の後処理。
 * @returns フォームの状態と操作。
 */
export function useProjectCreateForm(onCreated: ProjectCreatedHandler) {
  const uiText = useUiText();
  const text = uiText.projects.create;
  const [slug, setSlug] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [description, setDescription] = useState("");
  const [fieldErrors, setFieldErrors] = useState<ProjectCreateFieldErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (submittingRef.current) return;

      const result = buildCreateProjectBody({ slug, displayName, description });
      if (!result.ok) {
        setFieldErrors(result.errors);
        return;
      }

      submittingRef.current = true;
      setSubmitting(true);
      setFieldErrors({});
      setToast(null);
      try {
        const project = await createProject(result.body);
        setSlug("");
        setDisplayName("");
        setDescription("");
        // 作成は確定している。後処理の失敗は POST の失敗として扱わず、再読み込みを促すだけにする。
        const followedUp = await onCreated(project).catch(() => false);
        setToast(
          followedUp
            ? { tone: "success", message: text.created(project.displayName) }
            : { tone: "info", message: text.createdReloadRequired(project.displayName) },
        );
      } catch (error) {
        if (isSlugConflictError(error)) {
          setFieldErrors({ slug: "duplicate" });
        } else {
          setToast(toToastError(error));
        }
      } finally {
        submittingRef.current = false;
        setSubmitting(false);
      }
    },
    [description, displayName, onCreated, slug, text],
  );

  return {
    slug,
    setSlug,
    displayName,
    setDisplayName,
    description,
    setDescription,
    fieldErrors,
    submitting,
    toast,
    clearToast: () => setToast(null),
    submit,
  };
}
