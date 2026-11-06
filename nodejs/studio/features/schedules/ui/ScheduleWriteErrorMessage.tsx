"use client";

import Link from "next/link";
import { useI18n } from "@/shared/i18n/uiTextContext";
import type { ScheduleWriteError } from "../scheduleErrors";

/** メンバー管理の画面。Project は遷移先で選ぶ（URL で Project を指定する機能は無い）。 */
const PROJECT_MEMBERS_ADMIN_PATH = "/admin/projects";

type ScheduleWriteErrorMessageProps = {
  /** 分類済みの失敗。 */
  failure: ScheduleWriteError;
  /** 分類が `other` / `fieldMessage` のときに出す文。 */
  fallbackMessage: string;
  /** 要素の ID（`aria-describedby` 用）。 */
  id?: string;
};

/**
 * 保存・手動実行の失敗を辞書の文で出す。
 * run-as の 422 にはメンバー管理へのリンクを、有効な行の 422 には「無効にすれば保存できる」を添える。
 * @param props 失敗と代わりの文。
 * @returns エラー表示。
 */
export function ScheduleWriteErrorMessage({ failure, fallbackMessage, id }: Readonly<ScheduleWriteErrorMessageProps>) {
  const text = useI18n().uiText.schedules.serverErrors;

  return (
    <p id={id} className="mt-1 text-sm text-md-error" role="alert">
      {messageFor(failure, fallbackMessage, text)}
      {failure.kind === "runAsUnusable" ? (
        <>
          {" "}
          <Link href={PROJECT_MEMBERS_ADMIN_PATH} className="underline">
            {text.membersLink}
          </Link>
        </>
      ) : null}
      {(failure.kind === "runAsUnusable" || failure.kind === "definitionUnavailable") && failure.disableHint
        ? ` ${text.disableHint}`
        : null}
    </p>
  );
}

/**
 * 失敗の種類から本文を選ぶ。
 * @param failure 分類済みの失敗。
 * @param fallbackMessage 辞書に無い失敗の文。
 * @param text 辞書。
 * @returns 本文。
 */
function messageFor(
  failure: ScheduleWriteError,
  fallbackMessage: string,
  text: ReturnType<typeof useI18n>["uiText"]["schedules"]["serverErrors"]
): string {
  switch (failure.kind) {
    case "runAsUnusable":
      return text.runAsUnusable;
    case "definitionUnavailable":
      return text.definitionUnavailable;
    case "runAsGrantDenied":
      return text.runAsGrantDenied;
    case "projectAccessDenied":
      return text.projectAccessDenied;
    case "notFound":
      return text.notFound;
    case "fieldMessage":
      return failure.message;
    case "other":
      return fallbackMessage;
  }
}
