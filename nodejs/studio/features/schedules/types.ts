/**
 * 定期実行（`/v1/schedules`）の画面用の型。HTTP の camelCase に合わせる。
 * 契約は `docs/specifications/api-http.md` §3.12。
 */

/** 重なりの扱い。`skip` は同じスケジュール由来の実行が残っていれば Start しない。 */
export type ScheduleOverlapPolicy = "skip" | "allow";

/**
 * `GET /v1/schedules` の 1 件。機微な `input` を持たない（一覧で取りに行かない）。
 */
export type ScheduleListItem = {
  /** スケジュール ID。 */
  scheduleId: string;
  /** スケジュール名。 */
  name: string;
  /** 定義のリソース UUID。 */
  definitionId: string;
  /** ピンした定義版の UUID。null は発火時点の最新版。 */
  definitionVersionId: string | null;
  /** run-as の Principal ID。 */
  runAsPrincipalId: string;
  /** cron 式（5 フィールド）。 */
  cronExpression: string;
  /** タイムゾーン（IANA 名）。 */
  timeZone: string;
  /** 重なりの扱い。 */
  overlapPolicy: ScheduleOverlapPolicy;
  /** 有効か。 */
  enabled: boolean;
  /** 次枠（UTC）。 */
  nextFireAt: string;
  /** 作成日時（ISO 8601）。 */
  createdAt: string;
  /** 更新日時（ISO 8601）。 */
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
  /** ServiceAccount の Principal ID。 */
  principalId: string;
  /** ServiceAccount の名前。 */
  name: string;
};

/** 定義の候補と、一覧で定義名を引く対応表の 1 件。 */
export type ScheduleDefinitionOption = {
  /** 定義のリソース UUID。スケジュールの `definitionId` と同じ値。 */
  resourceId: string;
  /** 定義の表示用 ID。 */
  displayId: string;
  /** 定義名。 */
  name: string;
};

/** 定義候補の 1 ページ。 */
export type ScheduleDefinitionOptionPage = {
  /** ページ内の定義候補。 */
  options: ScheduleDefinitionOption[];
  /** 1 ページに収まらず欠けがあるか。 */
  hasMore: boolean;
};

/** 手動実行で開始した実行。詳細へのリンクに使う。 */
export type ScheduleRunResult = {
  /** 開始した実行の表示用 ID。 */
  displayId: string;
};
