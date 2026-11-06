"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { ASCII_LABEL_DEFAULT_MAX_LENGTH } from "@/shared/lib/validation/formRules";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { useScheduleForm, type ScheduleFormModel, type ScheduleSubmitFailure } from "../hooks/useScheduleForm";
import type { ScheduleField, ScheduleFieldErrorKind } from "../schedules";
import { SCHEDULE_DEFINITION_OPTIONS_LIMIT } from "../schedulesApi";
import type { ScheduleOverlapPolicy } from "../types";
import { ScheduleWriteErrorMessage } from "./ScheduleWriteErrorMessage";

type ScheduleFormPageClientProps = {
  /** 編集する ID。省略時は作成。 */
  scheduleId?: string;
};

type FormText = ReturnType<typeof useI18n>["uiText"]["schedules"]["form"];

/** 入力欄の見た目。 */
const INPUT_CLASS = "w-full rounded-lg border border-md-outline bg-md-surface px-3 py-2 text-sm";

/** 選べる overlap の値。 */
const OVERLAP_POLICIES: readonly ScheduleOverlapPolicy[] = ["skip", "allow"];

/**
 * 項目エラーの文言を選ぶ。`format` は欄ごとに文が違う。
 * @param field 欄。
 * @param kind 項目エラー。
 * @param text 辞書。
 * @returns 文言。
 */
function fieldErrorMessage(field: ScheduleField, kind: ScheduleFieldErrorKind, text: FormText): string {
  switch (kind) {
    case "required":
      return text.errors.required;
    case "tooLong":
      return text.errors.nameTooLong(ASCII_LABEL_DEFAULT_MAX_LENGTH);
    case "cannotClear":
      return text.errors.inputCannotClear;
    case "versionRequired":
      return text.errors.versionRequired;
    case "format":
      return formatErrorMessage(field, text);
  }
}

/**
 * 形式違反の文言を欄ごとに選ぶ。
 * @param field 欄。
 * @param text 辞書。
 * @returns 文言。
 */
function formatErrorMessage(field: ScheduleField, text: FormText): string {
  switch (field) {
    case "name":
      return text.errors.nameFormat;
    case "cronExpression":
      return text.errors.cronFormat;
    case "definitionVersion":
      return text.errors.versionFormat;
    case "input":
      return text.errors.inputFormat;
    default:
      return text.errors.required;
  }
}

/**
 * API の失敗を出す場所を決める。欄に結び付くものは欄の下、それ以外はフォームの上部に出す。
 * 定義を変えた PATCH の 404 / 403 は、移し先の定義の問題なので定義欄に出す。
 * @param submitFailure 保存の失敗。
 * @returns 欄、またはフォーム上部（`top`）。
 */
function failurePlacement(submitFailure: ScheduleSubmitFailure): ScheduleField | "top" {
  const { failure, definitionChanged } = submitFailure;
  switch (failure.kind) {
    case "runAsUnusable":
    case "runAsGrantDenied":
      return "runAsPrincipalId";
    case "definitionUnavailable":
      return "definitionId";
    case "fieldMessage":
      return failure.field;
    case "notFound":
    case "projectAccessDenied":
      return definitionChanged ? "definitionId" : "top";
    case "other":
      return "top";
  }
}

type FieldProps = {
  /** 入力要素の ID。 */
  id: string;
  /** ラベル。 */
  label: string;
  /** 補足。 */
  hint?: string;
  /** 欄。項目エラーと API の失敗の置き場所に使う。 */
  field: ScheduleField;
  /** フォームの状態。 */
  form: ScheduleFormModel;
  /** 入力要素。 */
  children: ReactNode;
};

/**
 * ラベル・補足・項目エラー・API の失敗を 1 欄にまとめる。
 * @param props 欄の情報と入力要素。
 * @returns 1 欄。
 */
function Field({ id, label, hint, field, form, children }: Readonly<FieldProps>) {
  const text = useI18n().uiText.schedules.form;
  const kind = form.fieldErrors[field];
  const failure = form.submitFailure;

  return (
    <div className="mb-4">
      <label htmlFor={id} className="mb-1 block text-sm font-medium">
        {label}
      </label>
      {children}
      {hint ? <p className="mt-1 text-xs text-md-on-surface-variant">{hint}</p> : null}
      {kind === undefined ? null : (
        <p className="mt-1 text-sm text-md-error" role="alert">
          {fieldErrorMessage(field, kind, text)}
        </p>
      )}
      {failure !== null && failurePlacement(failure) === field ? (
        <ScheduleWriteErrorMessage failure={failure.failure} fallbackMessage={failure.fallbackMessage} />
      ) : null}
    </div>
  );
}

type ScheduleFormProps = {
  /** フォームの状態。 */
  form: ScheduleFormModel;
};

/**
 * 定義の選択欄。現在の定義が候補に無いとき（削除済み・1 ページの欠け）は、その ID の選択肢を足す。
 * @param props フォームの状態。
 * @returns 選択欄。
 */
function DefinitionSelect({ form }: Readonly<ScheduleFormProps>) {
  const text = useI18n().uiText.schedules.form;
  const options = form.definitionOptions?.options ?? [];
  const currentId = form.current?.definitionId;
  const currentMissing = currentId !== undefined && !options.some((option) => option.resourceId === currentId);

  return (
    <>
      <select
        id="schedule-definition"
        className={INPUT_CLASS}
        value={form.values.definitionId}
        onChange={(event) => form.setValue("definitionId", event.target.value)}
      >
        <option value="">{text.definitionPlaceholder}</option>
        {currentMissing ? <option value={currentId}>{text.currentDefinitionOption(currentId)}</option> : null}
        {options.map((option) => (
          <option key={option.resourceId} value={option.resourceId}>
            {`${option.name} (${option.displayId})`}
          </option>
        ))}
      </select>
      {form.definitionOptions === null ? (
        <p className="mt-1 text-sm text-md-error">{text.definitionsLoadFailed}</p>
      ) : null}
      {form.definitionOptions?.hasMore ? (
        <p className="mt-1 text-xs text-md-on-surface-variant">
          {text.definitionsTruncated(SCHEDULE_DEFINITION_OPTIONS_LIMIT)}
        </p>
      ) : null}
    </>
  );
}

/**
 * run-as の選択欄。候補は有効な ServiceAccount だけ。現在の run-as が候補に無いとき（無効化された等）は、その ID の選択肢を足す。
 * 候補を読めなくても Principal ID の手入力欄は出さない。
 * @param props フォームの状態。
 * @returns 選択欄。
 */
function RunAsSelect({ form }: Readonly<ScheduleFormProps>) {
  const text = useI18n().uiText.schedules.form;
  const candidates = form.runAsCandidates ?? [];
  const currentId = form.current?.runAsPrincipalId;
  const currentMissing = currentId !== undefined && !candidates.some((candidate) => candidate.principalId === currentId);

  return (
    <>
      <select
        id="schedule-run-as"
        className={INPUT_CLASS}
        value={form.values.runAsPrincipalId}
        onChange={(event) => form.setValue("runAsPrincipalId", event.target.value)}
      >
        <option value="">{text.runAsPlaceholder}</option>
        {currentMissing ? <option value={currentId}>{text.currentRunAsOption(currentId)}</option> : null}
        {candidates.map((candidate) => (
          <option key={candidate.principalId} value={candidate.principalId}>
            {candidate.name}
          </option>
        ))}
      </select>
      {form.runAsCandidates === null ? <p className="mt-1 text-sm text-md-error">{text.runAsLoadFailed}</p> : null}
      {form.runAsCandidates?.length === 0 ? (
        <p className="mt-1 text-xs text-md-on-surface-variant">{text.runAsEmpty}</p>
      ) : null}
    </>
  );
}

/**
 * 作成・編集フォーム。`queue` と版 UUID は出さない。タイムゾーンは空のまま送らない。
 * @param props フォームの状態。
 * @returns フォーム。
 */
function ScheduleForm({ form }: Readonly<ScheduleFormProps>) {
  const { uiText } = useI18n();
  const text = uiText.schedules.form;
  const overlapText = uiText.schedules.overlap;
  const failure = form.submitFailure;

  return (
    <form
      noValidate
      onSubmit={(event) => void form.submit(event)}
      className="rounded-xl border border-md-outline bg-md-surface p-4 shadow-xs"
    >
      {failure !== null && failurePlacement(failure) === "top" ? (
        <div className="mb-3">
          <ScheduleWriteErrorMessage failure={failure.failure} fallbackMessage={failure.fallbackMessage} />
        </div>
      ) : null}
      {form.unchanged ? (
        <output className="mb-3 block text-sm text-md-on-surface-variant">{text.unchanged}</output>
      ) : null}
      <Field id="schedule-name" label={text.nameLabel} hint={text.nameHint} field="name" form={form}>
        <input
          id="schedule-name"
          className={INPUT_CLASS}
          value={form.values.name}
          onChange={(event) => form.setValue("name", event.target.value)}
        />
      </Field>
      <Field id="schedule-definition" label={text.definitionLabel} field="definitionId" form={form}>
        <DefinitionSelect form={form} />
      </Field>
      <Field
        id="schedule-version"
        label={text.versionLabel}
        hint={form.isEdit ? text.versionHintEdit : text.versionHintCreate}
        field="definitionVersion"
        form={form}
      >
        <input
          id="schedule-version"
          inputMode="numeric"
          className={INPUT_CLASS}
          value={form.values.definitionVersion}
          onChange={(event) => form.setValue("definitionVersion", event.target.value)}
        />
      </Field>
      <Field id="schedule-cron" label={text.cronLabel} hint={text.cronHint} field="cronExpression" form={form}>
        <input
          id="schedule-cron"
          className={`${INPUT_CLASS} font-mono`}
          value={form.values.cronExpression}
          onChange={(event) => form.setValue("cronExpression", event.target.value)}
        />
      </Field>
      <Field id="schedule-time-zone" label={text.timeZoneLabel} hint={text.timeZoneHint} field="timeZone" form={form}>
        <input
          id="schedule-time-zone"
          className={INPUT_CLASS}
          value={form.values.timeZone}
          onChange={(event) => form.setValue("timeZone", event.target.value)}
        />
      </Field>
      <Field id="schedule-overlap" label={text.overlapLabel} field="overlapPolicy" form={form}>
        <select
          id="schedule-overlap"
          className={INPUT_CLASS}
          value={form.values.overlapPolicy}
          onChange={(event) => {
            const selected = OVERLAP_POLICIES.find((policy) => policy === event.target.value);
            if (selected !== undefined) form.setValue("overlapPolicy", selected);
          }}
        >
          {OVERLAP_POLICIES.map((policy) => (
            <option key={policy} value={policy}>
              {overlapText[policy]}
            </option>
          ))}
        </select>
      </Field>
      <Field id="schedule-run-as" label={text.runAsLabel} field="runAsPrincipalId" form={form}>
        <RunAsSelect form={form} />
      </Field>
      <Field
        id="schedule-input"
        label={text.inputLabel}
        hint={form.isEdit ? `${text.inputHint} ${text.inputHintEdit}` : text.inputHint}
        field="input"
        form={form}
      >
        <textarea
          id="schedule-input"
          rows={6}
          className={`${INPUT_CLASS} font-mono`}
          value={form.values.inputText}
          onChange={(event) => form.setValue("inputText", event.target.value)}
        />
      </Field>
      <div className="mb-4">
        <label className="flex items-center gap-2 text-sm font-medium">
          <input
            type="checkbox"
            checked={form.values.enabled}
            onChange={(event) => form.setValue("enabled", event.target.checked)}
          />
          {text.enabledLabel}
        </label>
      </div>
      <button
        type="submit"
        disabled={form.saving}
        className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
      >
        {submitLabel(form, text)}
      </button>
    </form>
  );
}

/**
 * 送信ボタンの文言。
 * @param form フォームの状態。
 * @param text 辞書。
 * @returns 文言。
 */
function submitLabel(form: ScheduleFormModel, text: FormText): string {
  if (form.saving) return text.saving;
  return form.isEdit ? text.submitSave : text.submitCreate;
}

/**
 * 定期実行の作成・編集画面。編集は単票 GET で `input` をフォームに出す（一覧では取りに行かない）。
 * 単票の 404（不在・所属外）と 403（実行リソース許可の外）は一覧へ戻るリンクだけを出す。
 * テナント管理者以外は `app/schedules/layout.tsx` が戻す。
 * @param props 編集する ID。省略時は作成。
 * @returns 作成・編集画面。
 */
export function ScheduleFormPageClient({ scheduleId }: Readonly<ScheduleFormPageClientProps>) {
  const text = useI18n().uiText.schedules.form;
  const form = useScheduleForm(scheduleId);
  const title = form.isEdit ? text.editTitle : text.createTitle;
  const backLink = (
    <Link href="/schedules" className="hover:underline">
      {text.backToList}
    </Link>
  );

  switch (form.loadState) {
    case "loading":
      return (
        <PageShell title={title} secondaryActions={backLink}>
          <PageState state="loading" />
        </PageShell>
      );
    case "notFound":
      return (
        <PageShell title={title} secondaryActions={backLink}>
          <PageState state="empty" message={text.notFound} />
        </PageShell>
      );
    case "forbidden":
      return (
        <PageShell title={title} secondaryActions={backLink}>
          <PageState state="empty" message={text.forbidden} />
        </PageShell>
      );
    case "error":
      return (
        <PageShell title={title} secondaryActions={backLink}>
          <PageState state="error" message={text.loadFailed} onRetry={() => void form.reload()} />
        </PageShell>
      );
    case "ready":
      return (
        <PageShell title={title} secondaryActions={backLink}>
          <Toast toast={form.toast} onClose={form.clearToast} />
          <ScheduleForm form={form} />
        </PageShell>
      );
  }
}
