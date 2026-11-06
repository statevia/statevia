/**
 * 定期実行（`/v1/schedules`）の画面用の型。HTTP の camelCase に合わせる。
 * 契約は `docs/specifications/api-http.md` §3.12。
 */

/** 重なりの扱い。`skip` は同じスケジュール由来の実行が残っていれば Start しない。 */
export type ScheduleOverlapPolicy = "skip" | "allow";

/**
 * `GET /v1/schedules` の 1 行。機微な `input` を持たない（一覧で取りに行かない）。
 */
export type ScheduleListItem = {
  scheduleId: string;
  name: string;
  /** 定義のリソース UUID。 */
  definitionId: string;
  /** ピンした定義版の UUID。null は発火時点の最新版。 */
  definitionVersionId: string | null;
  runAsPrincipalId: string;
  cronExpression: string;
  timeZone: string;
  overlapPolicy: ScheduleOverlapPolicy;
  enabled: boolean;
  /** 次枠（UTC）。 */
  nextFireAt: string;
  createdAt: string;
  updatedAt: string;
};

/** `GET /v1/schedules/{id}` の応答。編集フォームでだけ使う。 */
export type ScheduleDetail = ScheduleListItem & {
  createdByPrincipalId: string;
  /** Start に渡す input。未設定は null。 */
  input: unknown;
};

/** run-as の候補。有効な ServiceAccount だけを入れる。 */
export type RunAsCandidate = {
  principalId: string;
  name: string;
};

/** 定義の候補と、一覧で定義名を引く対応表の 1 件。 */
export type ScheduleDefinitionOption = {
  /** 定義のリソース UUID。スケジュールの `definitionId` と同じ値。 */
  resourceId: string;
  displayId: string;
  name: string;
};

/** 定義候補の 1 ページ。 */
export type ScheduleDefinitionOptionPage = {
  options: ScheduleDefinitionOption[];
  /** 1 ページに収まらず欠けがあるか。 */
  hasMore: boolean;
};

/** 手動実行で開始した実行。詳細へのリンクに使う。 */
export type ScheduleRunResult = {
  displayId: string;
};
