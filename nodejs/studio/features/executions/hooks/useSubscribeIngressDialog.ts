"use client";

import { useEffect, useMemo, useState, type FormEvent } from "react";
import { toToastError } from "@/shared/lib/errors";
import { useI18n } from "@/shared/i18n/uiTextContext";
import type { UiText } from "@/shared/i18n/uiText";
import { isTopicIdentifier } from "@/shared/lib/validation/formRules";
import {
  listEventSubscriptions,
  publishTenantEvent,
  type EventSubscriptionCandidate
} from "../api";

/** topic / key 候補の表示項目。 */
export type CandidateOption = {
  /** 入力へ入れる値。空 key は空文字。 */
  value: string;
  /** 一覧に出すラベル。空 key は i18n の emptyKey。 */
  label: string;
};

/** 集合配送ダイアログの振る舞い。 */
export type UseSubscribeIngressDialogOptions = {
  /** 開いているか。開いたときに入力と候補を取り直す。 */
  open: boolean;
  /** 閉じる。受理後にも呼ぶ。 */
  onClose: () => void;
  /** 204 受理後。一覧の再取得に使う。 */
  onAccepted: () => void;
};

/** 画面が描画に使う集合配送ダイアログの状態。ReactNode は含まない。 */
export type SubscribeIngressDialogModel = {
  /** このダイアログの i18n 文言。 */
  text: UiText["executionsPage"]["ingress"];
  /** topic 入力。 */
  topic: string;
  /** key 入力。 */
  key: string;
  /** テナント内購読から重複を除いた topic 候補。 */
  topicOptions: CandidateOption[];
  /** 現在の topic に一致する key 候補。空 key のラベルは emptyKey。 */
  keyOptions: CandidateOption[];
  /** 候補 GET が失敗したか。失敗しても手入力は残す。 */
  candidatesFailed: boolean;
  /** topic / key の形式エラー。無ければ null。 */
  fieldError: string | null;
  /** 発行 API の失敗メッセージ。無ければ null。 */
  formError: string | null;
  /** 発行中か。 */
  submitting: boolean;
  /**
   * topic 入力を更新する。
   * @param value 入力値。
   */
  setTopic: (value: string) => void;
  /**
   * key 入力を更新する。
   * @param value 入力値。
   */
  setKey: (value: string) => void;
  /**
   * フォーム送信。検証に通れば `POST /v1/events` し、受理後に onAccepted と onClose を呼ぶ。
   * @param event フォームの submit。
   */
  submit: (event: FormEvent) => void;
};

/**
 * 購読候補から topic の表示項目を作る。出現順を保って重複を除く。
 * @param candidates テナント内のアクティブ購読。
 * @returns topic 候補。
 */
function toTopicOptions(candidates: readonly EventSubscriptionCandidate[]): CandidateOption[] {
  const topics = [...new Set(candidates.map((candidate) => candidate.topic))];
  return topics.map((topic) => ({ value: topic, label: topic }));
}

/**
 * 入力中の topic に一致する key の表示項目を作る。
 * @param candidates テナント内のアクティブ購読。
 * @param topic 入力中の topic。前後空白は比較前に除く。
 * @param emptyKeyLabel 空 key のラベル。
 * @returns key 候補。
 */
function toKeyOptions(
  candidates: readonly EventSubscriptionCandidate[],
  topic: string,
  emptyKeyLabel: string
): CandidateOption[] {
  const trimmedTopic = topic.trim();
  return candidates
    .filter((candidate) => candidate.topic === trimmedTopic)
    .map((candidate) => ({
      value: candidate.key,
      label: candidate.key || emptyKeyLabel
    }));
}

/**
 * 実行一覧の集合配送ダイアログの状態と送信を持つ。
 *
 * 画面コンポーネントはこの戻り値を描画する。開閉の出し分けとレイアウトは呼び出し側に残す。
 *
 * @param options 開閉と受理後のコールバック。
 * @returns 描画用の状態とコマンド。
 */
export function useSubscribeIngressDialog({
  open,
  onClose,
  onAccepted
}: UseSubscribeIngressDialogOptions): SubscribeIngressDialogModel {
  const { uiText, locale } = useI18n();
  const text = uiText.executionsPage.ingress;
  const [topic, setTopic] = useState("");
  const [key, setKey] = useState("");
  const [candidates, setCandidates] = useState<EventSubscriptionCandidate[]>([]);
  const [candidatesFailed, setCandidatesFailed] = useState(false);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTopic("");
    setKey("");
    setFieldError(null);
    setFormError(null);
    setCandidatesFailed(false);
    let cancelled = false;
    void listEventSubscriptions()
      .then((response) => {
        if (!cancelled) setCandidates(response.subscriptions);
      })
      .catch(() => {
        if (!cancelled) {
          setCandidates([]);
          setCandidatesFailed(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [open]);

  const topicOptions = useMemo(() => toTopicOptions(candidates), [candidates]);
  const keyOptions = useMemo(
    () => toKeyOptions(candidates, topic, text.emptyKey),
    [candidates, topic, text.emptyKey]
  );

  /**
   * 検証後に集合配送する。失敗時は formError にメッセージを残し、ダイアログは開いたままにする。
   */
  async function submitForm() {
    const trimmedTopic = topic.trim();
    const trimmedKey = key.trim();
    if (!isTopicIdentifier(trimmedTopic)) {
      setFieldError(text.invalidTopic);
      return;
    }
    if (trimmedKey.length > 0 && !isTopicIdentifier(trimmedKey)) {
      setFieldError(text.invalidKey);
      return;
    }
    setFieldError(null);
    setFormError(null);
    setSubmitting(true);
    try {
      await publishTenantEvent({ topic: trimmedTopic, key: trimmedKey });
      onAccepted();
      onClose();
    } catch (error) {
      setFormError(toToastError(error, locale).message);
    } finally {
      setSubmitting(false);
    }
  }

  /**
   * フォーム送信。
   * @param event フォームの submit。
   */
  function submit(event: FormEvent) {
    event.preventDefault();
    void submitForm();
  }

  return {
    text,
    topic,
    key,
    topicOptions,
    keyOptions,
    candidatesFailed,
    fieldError,
    formError,
    submitting,
    setTopic,
    setKey,
    submit
  };
}
