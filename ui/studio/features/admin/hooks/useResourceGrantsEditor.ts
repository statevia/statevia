"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { listAdminProjects } from "@/features/projects/api";
import { isApiNotFound } from "@/features/projects/httpStatus";
import type { ProjectSummary } from "@/features/projects/types";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import {
  buildReplaceResourceGrantsBody,
  type PrincipalResourceGrants,
  type ResourceGrantDefinitionCandidate,
  type ResourceGrantLimitErrors,
  type ResourceGrantSubject
} from "../resourceGrants";
import { getResourceGrants, listDefinitionCandidates, replaceResourceGrants } from "../resourceGrantsApi";

/**
 * 許可の読込状態。
 * - `notFound`: 404（存在しない主体や他テナント）
 * - `error`: それ以外の失敗。再試行できる
 */
export type ResourceGrantsLoadState = "loading" | "ready" | "notFound" | "error";

/** 実行リソース許可の編集状態。ReactNode は含まない。 */
export type ResourceGrantsEditorModel = {
  /** 許可の読込状態。 */
  loadState: ResourceGrantsLoadState;
  /** 最後に読んだ（または保存した）許可。`ready` 以外は null。 */
  savedGrants: PrincipalResourceGrants | null;
  /** 許可と候補を読み直す。編集中の選択は現在値に戻る。 */
  reload: () => Promise<void>;
  /** project の候補（テナント内の全 Project）。読込前と失敗時は null。付与先の所属では絞らない。 */
  projectCandidates: ProjectSummary[] | null;
  /** 定義の候補（管理者自身が所属する Project の未削除定義）。読込前と失敗時は null。 */
  definitionCandidates: ResourceGrantDefinitionCandidate[] | null;
  /** 定義候補が 1 ページに収まらず欠けているか。 */
  definitionCandidatesTruncated: boolean;
  /** 候補のどちらかの取得に失敗したか。true の間は保存できない。 */
  candidatesLoadFailed: boolean;
  /** 選択中の project ID。候補に無い付与済み ID も含む。 */
  selectedProjectIds: ReadonlySet<string>;
  /** 選択中の定義 ID。候補に無い付与済み ID も含む。 */
  selectedDefinitionIds: ReadonlySet<string>;
  /**
   * project の選択を切り替える。
   * @param projectId Project ID。
   */
  toggleProject: (projectId: string) => void;
  /**
   * 定義の選択を切り替える。
   * @param definitionId 定義の resourceId。
   */
  toggleDefinition: (definitionId: string) => void;
  /** 送信前に止めた上限超過。 */
  limitErrors: ResourceGrantLimitErrors;
  /** サーバーが返したエラー（422 など）。無ければ null。 */
  submitError: string | null;
  /** 保存できる状態か（許可と候補を読めていて、保存中でない）。 */
  canSave: boolean;
  /** 保存中か。 */
  saving: boolean;
  /** 成功通知。無ければ null。 */
  toast: ToastState | null;
  /** 成功通知を閉じる。 */
  clearToast: () => void;
  /**
   * 両配列を PUT する。上限を超えるときは送らない。失敗したら許可を読み直して表示を合わせる。
   * @param event フォームの submit。
   */
  submit: (event: FormEvent<HTMLFormElement>) => Promise<void>;
};

/**
 * 集合の要素を 1 つ切り替えた新しい集合を返す。
 * @param current 現在の集合。
 * @param id 切り替える ID。
 * @returns 新しい集合。
 */
function toggled(current: ReadonlySet<string>, id: string): ReadonlySet<string> {
  const next = new Set(current);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  return next;
}

/**
 * ユーザーと ServiceAccount で共通の、実行リソース許可の読込・選択・置換。
 * 候補は Project 一覧と定義一覧をそれぞれ 1 回だけ読む。付与先の所属は確かめず、判定は PUT の 422 に任せる。
 * 候補を読めない間は、付与済みを落とした本文を送らないよう保存させない。
 * @param subject 対象の主体。参照が変わると読み直すため、呼び出し側で安定させる。
 * @returns 描画用の状態と操作。
 */
export function useResourceGrantsEditor(subject: ResourceGrantSubject): ResourceGrantsEditorModel {
  const { locale, uiText } = useI18n();
  const text = uiText.admin.resourceGrants;
  const [loadState, setLoadState] = useState<ResourceGrantsLoadState>("loading");
  const [savedGrants, setSavedGrants] = useState<PrincipalResourceGrants | null>(null);
  const [projectCandidates, setProjectCandidates] = useState<ProjectSummary[] | null>(null);
  const [definitionCandidates, setDefinitionCandidates] = useState<ResourceGrantDefinitionCandidate[] | null>(null);
  const [definitionCandidatesTruncated, setDefinitionCandidatesTruncated] = useState(false);
  const [candidatesLoadFailed, setCandidatesLoadFailed] = useState(false);
  const [selectedProjectIds, setSelectedProjectIds] = useState<ReadonlySet<string>>(new Set());
  const [selectedDefinitionIds, setSelectedDefinitionIds] = useState<ReadonlySet<string>>(new Set());
  const [limitErrors, setLimitErrors] = useState<ResourceGrantLimitErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const savingRef = useRef(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * 選択を許可の現在値に戻す。
   * @param grants 現在値。
   */
  const resetSelection = useCallback((grants: PrincipalResourceGrants) => {
    setSavedGrants(grants);
    setSelectedProjectIds(new Set(grants.projectIds));
    setSelectedDefinitionIds(new Set(grants.definitionIds));
    setLimitErrors({});
  }, []);

  /**
   * 許可だけを読み直す。失敗時は読込状態に反映する。
   * @returns 読めたら true。
   */
  const reloadGrants = useCallback(async (): Promise<boolean> => {
    try {
      resetSelection(await getResourceGrants(subject));
      setLoadState("ready");
      return true;
    } catch (error) {
      setSavedGrants(null);
      setLoadState(isApiNotFound(error) ? "notFound" : "error");
      return false;
    }
  }, [resetSelection, subject]);

  const reload = useCallback(async () => {
    setLoadState("loading");
    setSubmitError(null);
    setCandidatesLoadFailed(false);
    const [, candidates] = await Promise.all([
      reloadGrants(),
      Promise.all([listAdminProjects(), listDefinitionCandidates()]).then(
        ([projects, definitions]) => ({ projects, definitions }),
        () => null
      )
    ]);
    if (candidates === null) {
      setProjectCandidates(null);
      setDefinitionCandidates(null);
      setDefinitionCandidatesTruncated(false);
      setCandidatesLoadFailed(true);
      return;
    }
    setProjectCandidates(candidates.projects);
    setDefinitionCandidates(candidates.definitions.candidates);
    setDefinitionCandidatesTruncated(candidates.definitions.hasMore);
  }, [reloadGrants]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const toggleProject = useCallback((projectId: string) => {
    setSelectedProjectIds((current) => toggled(current, projectId));
  }, []);

  const toggleDefinition = useCallback((definitionId: string) => {
    setSelectedDefinitionIds((current) => toggled(current, definitionId));
  }, []);

  const canSave = loadState === "ready" && !candidatesLoadFailed && projectCandidates !== null && !saving;

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (savingRef.current || !canSave) return;

      const result = buildReplaceResourceGrantsBody({
        projectIds: selectedProjectIds,
        definitionIds: selectedDefinitionIds
      });
      if (!result.ok) {
        setLimitErrors(result.errors);
        setSubmitError(null);
        return;
      }

      savingRef.current = true;
      setSaving(true);
      setLimitErrors({});
      setSubmitError(null);
      setToast(null);
      try {
        resetSelection(await replaceResourceGrants(subject, result.body));
        setToast({ tone: "success", message: text.saved });
      } catch (error) {
        setSubmitError(toToastError(error, locale).message);
        // 422 では既存の許可がサーバーに残る契約なので、表示を保存済みの値へ合わせる。
        await reloadGrants();
      } finally {
        savingRef.current = false;
        setSaving(false);
      }
    },
    [canSave, locale, reloadGrants, resetSelection, selectedDefinitionIds, selectedProjectIds, subject, text]
  );

  return {
    loadState,
    savedGrants,
    reload,
    projectCandidates,
    definitionCandidates,
    definitionCandidatesTruncated,
    candidatesLoadFailed,
    selectedProjectIds,
    selectedDefinitionIds,
    toggleProject,
    toggleDefinition,
    limitErrors,
    submitError,
    canSave,
    saving,
    toast,
    clearToast: () => setToast(null),
    submit
  };
}
