"use client";

import { useId, useState } from "react";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { isWithinMaxLength, matchesPattern } from "@/shared/lib/validation/primitives";
import { EVENT_NAME_MAX_LENGTH, EVENT_NAME_PATTERN } from "@/shared/lib/validation/formRules";
import { Button } from "@/shared/ui/Button";
import { Dialog } from "@/shared/ui/Dialog";
import { inputClassName } from "@/shared/ui/FormField";
import { Notice } from "@/shared/ui/Notice";
import type { SendEventTarget } from "../hooks/useSendEvent";
import { isSubscribeInternalResume } from "../lib/waitResumeEvents";
import type { WaitTarget } from "../lib/waitTargets";

const EXECUTION_OPTION = "execution";

/** 届け先の枠の中のラベル。1 列目にラジオ、2 列目に見出しと説明を縦に積む。 */
const OPTION_LABEL_CLASS = "grid cursor-pointer grid-cols-[auto_minmax(0,1fr)] items-start gap-x-3 gap-y-0.5";
const RADIO_CLASS = "row-span-3 mt-0.5 h-4 w-4 accent-[var(--md-sys-color-primary)]";
const OPTION_TITLE_CLASS = "text-sm font-semibold text-md-on-surface";
const OPTION_HINT_CLASS = "col-start-2 text-xs text-md-on-surface-variant";

/** イベント送信ダイアログの props。 */
export type SendEventDialogProps = {
  /** 実行の表示用 ID（説明文に出す）。 */
  executionDisplayId: string;
  /** 届け先の候補（待っているノード）。最新の実行から作り直したものを渡す。 */
  waits: readonly WaitTarget[];
  /** 開いたときに選んでおくノード ID。null なら先頭の待機（待機が無ければイベント名の指定）。 */
  initialNodeId: string | null;
  /** 送信中か。送信中は閉じられない。 */
  sending: boolean;
  /** 閉じる。 */
  onClose: () => void;
  /**
   * 送る。
   * @param target 届け先とイベント名。
   * @returns 受理されたら null（呼び出し側が閉じる）。失敗したらダイアログに出すエラーの文言。
   */
  onSend: (target: SendEventTarget) => Promise<string | null>;
};

/**
 * 実行の詳細から、待機中の実行へイベントを届けるダイアログ（要件 5）。
 * @remarks 届け先は、待っているノード（そのノードだけへ届ける。`resume`）か、イベント名を指定して実行へ送る（`events`）かの 1 つ。
 * 開くたびに状態を作り直すため、呼び出し側は開いている間だけ描画する。
 * 送信に失敗してもダイアログは閉じず、エラーを中に出す。開いている間に待機が解けて選んでいたノードが候補から消えたら、送信できなくする。
 * イベント名は今の規則（半角英字で始まり、英数字と `.` `-` `_`、64 文字まで）で検証し、不正なら API へ送らない。
 */
export function SendEventDialog({
  executionDisplayId,
  waits,
  initialNodeId,
  sending,
  onClose,
  onSend
}: Readonly<SendEventDialogProps>) {
  const { uiText, locale } = useI18n();
  const text = uiText.executionDashboard.sendEvent;
  const dateTimeLocale = getDateTimeLocale(locale);
  const radioName = useId();
  const executionInputId = useId();
  const [selected, setSelected] = useState<string>(
    () => initialNodeId ?? waits.at(0)?.nodeId ?? EXECUTION_OPTION
  );
  const [eventByNodeId, setEventByNodeId] = useState<Record<string, string>>({});
  const [executionEventName, setExecutionEventName] = useState("");
  const [executionEventTouched, setExecutionEventTouched] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const selectedWait = waits.find((wait) => wait.nodeId === selected) ?? null;
  const trimmedExecutionEventName = executionEventName.trim();
  const executionEventError = validateEventName(trimmedExecutionEventName, uiText.executionDashboard.validation);

  const description = describe(waits, executionDisplayId, text);
  const target = resolveTarget(selected, selectedWait, eventByNodeId, trimmedExecutionEventName, executionEventError);

  const formatSince = (iso: string | null) => (iso ? formatDateTimeLocalized(iso, dateTimeLocale) : null);

  const handleSubmit = async () => {
    if (selected === EXECUTION_OPTION) setExecutionEventTouched(true);
    if (!target || sending) return;
    setSendError(null);
    const error = await onSend(target);
    if (error) setSendError(error);
  };

  const preview =
    selected === EXECUTION_OPTION
      ? text.previewExecution
      : selectedWait &&
        text.previewNode(
          selectedWait.nodeName,
          waits.filter((wait) => wait.nodeId !== selectedWait.nodeId).map((wait) => wait.nodeName)
        );

  return (
    <Dialog
      open
      onClose={onClose}
      title={text.title}
      description={description}
      size="lg"
      dismissible={!sending}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={sending}>
            {text.cancel}
          </Button>
          <Button variant="lead" onClick={() => void handleSubmit()} loading={sending} disabled={!target}>
            {text.submit}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <fieldset className="flex flex-col gap-2.5">
          <legend className="mb-2 text-sm font-semibold text-md-on-surface">{text.targetLabel}</legend>
          {waits.map((wait) => {
            const checked = selected === wait.nodeId;
            const singleEvent = wait.events.length === 1 ? (wait.events.at(0) ?? null) : null;
            const subscribeOnly = isSubscribeInternalResume(wait.events);
            return (
              <div key={wait.nodeId} className={optionCardClassName(checked)}>
                <label className={OPTION_LABEL_CLASS}>
                  <input
                    type="radio"
                    name={radioName}
                    value={wait.nodeId}
                    checked={checked}
                    onChange={() => setSelected(wait.nodeId)}
                    className={RADIO_CLASS}
                  />
                  <span className={OPTION_TITLE_CLASS}>{text.nodeOptionLabel(wait.nodeName, singleEvent)}</span>
                  <span className={OPTION_HINT_CLASS}>{text.nodeOptionHint(formatSince(wait.waitingSince))}</span>
                  {subscribeOnly ? (
                    <span className={`${OPTION_HINT_CLASS} whitespace-pre-line`}>
                      {uiText.nodeDetail.waiting.subscribeOnlyHint}
                    </span>
                  ) : null}
                </label>
                {checked && wait.events.length > 1 ? (
                  <div className="mt-2.5 ml-7">
                    <select
                      aria-label={text.nodeEventSelectLabel(wait.nodeName)}
                      className={inputClassName({ className: "font-mono" })}
                      value={eventByNodeId[wait.nodeId] ?? wait.events.at(0) ?? ""}
                      onChange={(event) =>
                        setEventByNodeId((previous) => ({ ...previous, [wait.nodeId]: event.target.value }))
                      }
                    >
                      {wait.events.map((eventName) => (
                        <option key={eventName} value={eventName}>
                          {eventName}
                        </option>
                      ))}
                    </select>
                  </div>
                ) : null}
              </div>
            );
          })}
          <div className={optionCardClassName(selected === EXECUTION_OPTION)}>
            <label className={OPTION_LABEL_CLASS}>
              <input
                type="radio"
                name={radioName}
                value={EXECUTION_OPTION}
                checked={selected === EXECUTION_OPTION}
                onChange={() => setSelected(EXECUTION_OPTION)}
                className={RADIO_CLASS}
              />
              <span className={OPTION_TITLE_CLASS}>{text.executionOptionLabel}</span>
              <span className={OPTION_HINT_CLASS}>{text.executionOptionHint}</span>
            </label>
            <div className="mt-2.5 ml-7">
              <input
                id={executionInputId}
                aria-label={text.eventNameLabel}
                aria-invalid={executionEventTouched && executionEventError ? true : undefined}
                aria-describedby={executionEventTouched && executionEventError ? `${executionInputId}-error` : undefined}
                className={inputClassName({
                  error: executionEventTouched && !!executionEventError,
                  className: "font-mono"
                })}
                value={executionEventName}
                placeholder={text.eventNamePlaceholder}
                autoComplete="off"
                onFocus={() => setSelected(EXECUTION_OPTION)}
                onChange={(event) => setExecutionEventName(event.target.value)}
                onBlur={() => setExecutionEventTouched(trimmedExecutionEventName.length > 0)}
              />
              {executionEventTouched && executionEventError ? (
                <p id={`${executionInputId}-error`} role="alert" className="mt-1 text-xs text-md-error">
                  {executionEventError}
                </p>
              ) : null}
            </div>
          </div>
        </fieldset>

        {preview ? <Notice tone="info" title={preview} /> : null}
        {sendError ? <Notice tone="error" title={sendError} /> : null}
      </div>
    </Dialog>
  );
}

/**
 * 選んでいる届け先の枠のクラス。選んでいる枠だけ主色の枠と淡い塗りにする。
 * @param checked 選んでいるか。
 */
function optionCardClassName(checked: boolean): string {
  return [
    "rounded-card border px-4 py-3 transition-colors",
    checked ? "border-md-primary bg-md-primary/5 ring-1 ring-md-primary" : "border-md-outline-variant"
  ].join(" ");
}

/**
 * イベント名を検証する。
 * @param eventName trim 済みのイベント名。
 * @param messages 検証の文言。
 * @returns エラーの文言。空・正しいときは null（空は送信できないことで示す）。
 */
function validateEventName(
  eventName: string,
  messages: { eventNameTooLong: string; eventNameInvalidFormat: string }
): string | null {
  if (eventName.length === 0) return null;
  if (!isWithinMaxLength(eventName, EVENT_NAME_MAX_LENGTH)) return messages.eventNameTooLong;
  if (!matchesPattern(eventName, EVENT_NAME_PATTERN)) return messages.eventNameInvalidFormat;
  return null;
}

/**
 * 選択から送る届け先を作る。送れないとき（候補から消えた・イベント名が空か不正）は null。
 */
function resolveTarget(
  selected: string,
  selectedWait: WaitTarget | null,
  eventByNodeId: Readonly<Record<string, string>>,
  executionEventName: string,
  executionEventError: string | null
): SendEventTarget | null {
  if (selected === EXECUTION_OPTION) {
    if (executionEventName.length === 0 || executionEventError) return null;
    return { kind: "execution", eventName: executionEventName };
  }
  if (!selectedWait) return null;
  const eventName = eventByNodeId[selectedWait.nodeId] ?? selectedWait.events.at(0);
  return eventName ? { kind: "node", nodeId: selectedWait.nodeId, eventName } : null;
}

/**
 * 待機の数に合わせた説明文を作る。
 */
function describe(
  waits: readonly WaitTarget[],
  executionDisplayId: string,
  text: {
    descriptionNone: (executionId: string) => string;
    descriptionSingle: (executionId: string, nodeName: string) => string;
    descriptionMulti: (executionId: string, count: number) => string;
  }
): string {
  const [first] = waits;
  if (!first) return text.descriptionNone(executionDisplayId);
  if (waits.length === 1) return text.descriptionSingle(executionDisplayId, first.nodeName);
  return text.descriptionMulti(executionDisplayId, waits.length);
}
