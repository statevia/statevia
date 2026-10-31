# ログキー命名規約

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.1 |
| 更新日 | 2026-10-25 |
| 関連 | [logging-operations.md](../specifications/platform/logging-operations.md) |

---

**Version 1.1（2026-10-25）**: EventId を5桁（層・領域・連番）で割り当て、コードの番号を本表へ移す。採番を検査するテストを足す。新規ログの重大度の選び方を追加する。

コントリビュータと運用者向けの辞書。出力契約の正本は [logging-operations.md](../specifications/platform/logging-operations.md)。

## 目的

Service API / Worker / Application / Engine の構造化ログで、同じ概念に同じキー名を使う。運用者が ingest 時に `TenantId` を保管キーへ写すときも、本辞書のキー名を正とする。stdout の行は調査用であり、監査の正本は `event_store` である。

## 出力手段（必須）

- **`ILogger.LogInformation` / `LogWarning` / `LogError` 等を直接呼ばない。**
- `[LoggerMessage]`（ソース生成）の partial メソッド経由で出す（例: `ExecutionServiceLogMessages`、`ForkExpansionLogMessages`）。
- 新規カテゴリは `*LogMessages.cs` にまとめ、EventId は次節に従う。

## EventId

`[LoggerMessage]` の EventId は `int` である。logging overlay の JsonConsole は、この整数だけを `"EventId"` に書く。英字のコードはこのフィールドに出ない。

Id は `10000` 以上 `99999` 以下の5桁とする。左からの意味は次のとおり。

| 位置 | 名前 | 範囲 | 意味 |
| --- | --- | --- | --- |
| 1桁目 | 層 | 1–9 | その行をどの境界の言葉で読むか |
| 2–3桁目 | 領域 | 00–99 | 層の中の機能。新しい機能は新しい領域にする |
| 4–5桁目 | 連番 | 01–99 | 領域内の出来事。桁そのものに意味は持たせない |

`00` は領域の代表として空け、出来事は `01` から振る。リポジトリ全体で整数は一つにする。欠番と、表から外した旧番号は再利用しない。連番が 80 に達したら、次の出来事は新しい領域へ置く。6桁にはしない。番号だけを移すとき、Message の文言は維持する。

コードの番号は本表のとおりである。Version 1.1 より前の4桁以下の番号は使わない。旧番号でログを絞り込んでいたクエリは、本表の帯へ置き換える。採番の重複、5桁の範囲、下2桁 `00` の禁止、領域表の帯からの逸脱は、`dotnet/tests/Statevia.Architecture.Tests` の `LoggerMessageEventIdTests` が検出する。新しい領域を使うときは、先に本表へ行を足す。

| 1桁目 | 層 | 読むときの境界 |
| --- | --- | --- |
| 1 | ホスト入出力 | HTTP、未処理例外、起動、ログインロック |
| 3 | テナント業務 | 実行、Fork、定義コンパイル、テナント管理、Project メンバー |
| 5 | Engine | `ExecutionId` で追う行。Engine にテナント概念は足さない |
| 6 | Module と隔離実行 | 読込、署名、供給元、Action Host、Docker sandbox |
| 7 | 周期 Runtime | worker、待機、リース回復、スケジュールループ、投影キュー |

2、4、8、9 は空いている。3桁目だけを小分類にしない。2–3桁目をまとめて領域番号にする。表に無い領域番号は、新規機能が使う。

連番は、領域の行に書いた型の順で、各型の `[LoggerMessage]` の出現順に `01` から振る。

| 領域 | 帯 | 型（この順で連番） |
| --- | --- | --- |
| HTTP | `10001`–`10099` | `RequestLoggingLogMessages` |
| API 例外 | `10101`–`10199` | `ApiExceptionFilterLogMessages` |
| Trace | `10201`–`10299` | `TraceContextEnrichmentLogMessages` |
| テナント起動 | `10301`–`10399` | `TenantBootstrapLogMessages` |
| ログインロック | `11001`–`11099` | `LoginFailureLockStoreLogMessages`、`LoginFailureLockPurgeHostedServiceLogMessages` |
| 開発用通知 | `12001`–`12099` | `DevelopmentNotificationLogMessages` |
| 実行ライフサイクル | `30001`–`30099` | `ExecutionServiceLogMessages` |
| スケジュール実行 | `30101`–`30199` | `ExecutionScheduleDispatchLogMessages` |
| システムスケジュール | `30201`–`30299` | `SystemScheduleEnsureLogMessages` |
| Fork 展開 | `30301`–`30399` | `ForkExpansionLogMessages` |
| 定義コンパイル | `30401`–`30499` | `DefinitionCompilerLogMessages` |
| テナント管理 | `31001`–`31099` | `TenantAdministrationLogMessages` |
| Project メンバー | `31101`–`31199` | `ProjectMemberLogMessages` |
| 実行エンジン | `50001`–`50099` | `ExecutionEngine.LogMessages` |
| EventProvider | `50101`–`50199` | `EventProvider.LogMessages` |
| Module 読込 | `60001`–`60099` | `ModuleHost` |
| ファイル供給 | `60101`–`60199` | `FilesystemModuleSource` |
| OCI 供給 | `60201`–`60299` | `OciModuleSource` |
| S3 供給 | `60301`–`60399` | `S3ModuleSource` |
| Git 供給 | `60401`–`60499` | `GitModuleSource` |
| Git HTTP 取得 | `60501`–`60599` | `HttpGitArtifactFetcher` |
| OCI 取得 | `60601`–`60699` | `OrasOciArtifactFetcher` |
| S3 取得 | `60701`–`60799` | `AwsS3ArtifactFetcher` |
| 供給の合成 | `60801`–`60899` | `CompositeModuleSource` |
| Module 署名 | `60901`–`60999` | `ModuleSignatureVerifier` |
| Modules パス | `61001`–`61099` | `ResolvedModulePathProvider` |
| Action Host 読込 | `62001`–`62099` | `ActionHostModuleLoader` |
| Action Host 探索 | `62101`–`62199` | `FilesystemModuleDiscoverer` |
| Action Host 実行 | `62201`–`62299` | `ActionHostExecutor` |
| Docker sandbox | `63001`–`63099` | `DockerSandboxRuntimeLog` |
| Module 変更検知 | `64001`–`64099` | `ModuleLoadHostedService` |
| work item worker | `70001`–`70099` | `ExecutionWorkItemWorkerLogMessages` |
| Delay 待機 | `70101`–`70199` | `DelayWaitSchedulerLogMessages` |
| ownership recovery | `70201`–`70299` | `ExecutionOwnershipRecoveryLogMessages` |
| スケジュールループ | `70301`–`70399` | `ExecutionScheduleDispatcherLogMessages` |
| 実行投影キュー | `70401`–`70499` | `ExecutionProjectionUpdateQueueLogMessages` |

## 新規ログの重大度

新しい `[LoggerMessage]` のレベルは次で選ぶ。HTTP の 4xx 完了は Information のままである。ログインのパスワード誤りごとに `Username` は出さない。ロックのストア書き込み失敗は Error のままである。

| 内容 | レベル |
| --- | --- |
| 正常なライフサイクル、協調 Cancel、HTTP の 4xx 完了 | Information |
| 続行できる再試行、設定の既定値適用、ログインロックへの到達 | Warning |
| 運用者が見る想定外、`Fact=Failed` の実行終端、ログインロックのストア障害 | Error |

`serializable_persist_retry` は現在 Information である。再試行の行に合わせる変更は、番号の移行とは別にする。ログインロックへの到達は、現在はログを出していない。

## Message テンプレート（Service API）

- **ラベル**（`=` の左）: **PascalCase**（例: `TraceId`, `TenantId`）
- **プレースホルダと引数名**（`{…}` 内および partial メソッド引数）: **camelCase**（例: `{traceId}`, 引数 `traceId`）

例: `TraceId={traceId} TenantId={tenantId}`

## 標準キー一覧

| 概念 | ラベル（PascalCase） | 主な出力箇所 | 備考 |
|------|----------------------|--------------|------|
| 相関 ID | `TraceId` | API / Engine | `traceparent` / `X-Trace-Id` から解決 |
| テナント | `TenantId` | API / Worker / Application | 解決済み `tenants.tenant_id`（UUID）。未解決パスでは欠落。Org 写像の入力。`TenantKey` は使わない |
| ワークフロー ID | `ExecutionId` | API enrich / 実行系 / Engine | route `{id}` 由来（display ID の場合あり） |
| 親ワークフロー ID | `ParentExecutionId` | Fork 展開 | 物理子展開時の親 |
| Fork ノード ID | `ForkNodeId` | Fork 展開 | 親実行グラフ上の Fork 到達インスタンス |
| 状態名 | `StateName` | Engine | State 実行ログ・Warning |
| 定義名 | `DefinitionName` | Engine | 実行開始・完了・実行終端 Cancel・Cancel 次遷移抑制 |
| 状態事実 | `Fact` | Engine | 状態完了・実行終端 Cancel・Cancel 次遷移抑制 |
| 定義 ID | `DefinitionId` | API enrich | route `{id}` 由来（display ID の場合あり） |
| グラフ定義 ID | `GraphDefinitionId` | API enrich | route `{graphId}` |
| HTTP メソッド | `Method` | API | - |
| パス | `Path` | API | クエリなし |
| クエリ | `Query` / `QueryForLog` | API | マスク後 |
| ステータス | `StatusCode` | API | - |
| 経過時間 | `ElapsedMs` | API / Engine / Worker watchdog | ミリ秒 |
| ライフサイクルスロット | `ActiveLifecycleSlots` | Worker | 使用中の Start / Resume スロット数 |
| 同時処理上限 | `MaxConcurrency` | Worker | `Statevia:Runtime:Worker:MaxConcurrency` |
| work item ID | `WorkItemId` | Worker | 恒久失敗・lease 喪失 |
| work item 種別 | `WorkItemKind` | Worker | Start / Resume / Cancel |
| 恒久失敗理由 | `FailureReason` | Worker | `restore_invalid` / `definition_version_missing` / `max_attempts`。YAML 全文は出さない |
| 例外型 | `ExceptionType` / `ErrorType` | API / Engine | - |
| 操作主体 Principal | `ActorPrincipalId` | 管理者パスワード更新、Project メンバー変更 | 監査。平文・ハッシュは出さない |
| 操作対象ユーザー | `TargetUserId` | 管理者パスワード更新 | `users.user_id` |
| 操作対象 Principal | `TargetPrincipalId` | Project メンバー変更 | `principals.principal_id`。シークレットは出さない |
| Project | `ProjectId` | Project メンバー変更 | `projects.project_id` |
| テナントキー | `TenantKey` | ログイン失敗ロック | JWT 前の外部キー。パスワードは出さない |
| ユーザー名 | `Username` | ログイン失敗ロック | ロック主体。パスワード・ハッシュは出さない |
| 停滞実行件数 | `StuckExecutionCount` | スケジュール点検 | Running かつ待機なしで、実効 Action タイムアウト以上更新が無い件数 |
| 失敗スケジュール件数 | `FailedScheduleRunCount` | スケジュール点検 | 直前の点検以降に失敗したテナントスケジュール枠の件数 |

## Engine

Engine（`ExecutionEngine.LogMessages`）はラベル・プレースホルダ・引数を **PascalCase**（`ExecutionId={ExecutionId}`）で維持する。

## 補足

- ログ基盤側でキーを小文字化する運用は許容する。
