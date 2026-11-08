# 用語集

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.1 |
| 更新日 | 2026-11-08 |
| 関連 | [../concepts/README.md](../concepts/README.md), [../architecture/domain-model-boundaries.md](../architecture/domain-model-boundaries.md) |

---

Statevia の主な用語と、それぞれを API・YAML・コードでどう書くかの一覧。同じ概念に別の語を使わないこと、似た語でも別の概念なら区別することを目的にする。

読者は API / UI / 運用の利用者と、コードを読むコントリビュータ。意味の正本は各 Specification で、ここでは要約とリンクだけを置く。

## 読み方

- **用語**: このプロジェクトで使う語。日本語の UI 文言は「UI」列に書く。
- **書き方**: API の JSON・定義 YAML・DB の列・コードの識別子での表記。
- **使わない語・区別**: 同じ意味で使わない同義語と、紛らわしい別の概念。

## 定義

| 用語 | UI | 意味 | 書き方 | 使わない語・区別 |
| --- | --- | --- | --- | --- |
| Definition | 定義 | ワークフローを記述した設計の正本。publish すると版が追加される | `/v1/definitions`、`definitions` テーブル、`Definition*` | Spec / Template / Blueprint は使わない |
| Definition Version | 版 | publish ごとに追加される不変の定義。実行はどれか 1 つの版に固定される | `definition_version_id`、`definition_versions` テーブル | 既存版の上書きは無い（append-only） |
| ワークフロー（Workflow） | — | 定義が記述する処理の流れ。定義 YAML のルートキー `workflow:` と、`workflow.invoke` Action の名前に使う | `workflow:`、`CompiledWorkflowDefinition` | 実行中のインスタンスは Execution と呼ぶ（[旧語として残る Workflow](#旧語として残る-workflow)） |
| states 形式 / nodes 形式 | — | 定義 YAML の 2 つの書き方。nodes 形式はエディタ向けで、実行前に states 形式へ正規化される | `StateWorkflowDefinitionLoader`、`NodesWorkflowDefinitionLoader` | — |
| State | — | 定義上の状態。名前（StateName）を持ち、任意で Action を持つ | 定義 YAML の `name`、`StateDefinition` | 実行グラフの Node とは別（[State と Node](#state-と-node)） |
| Compile | — | publish 時に構文検証・Action 解決を行い、実行可能なグラフへ変換すること | `DefinitionCompilerService` | — |

詳細は [specifications/definition.md](../specifications/definition.md)。

## 実行

| 用語 | UI | 意味 | 書き方 | 使わない語・区別 |
| --- | --- | --- | --- | --- |
| Execution | 実行 | 定義の 1 つの版に従って進む状態機械のインスタンス | `/v1/executions`、`executionId`、`executions` テーブル、`Execution*` | Workflow / Run は使わない。Instance は Engine がメモリ上に持つ実行状態 `ExecutionInstance` だけに使う |
| 実行ステータス | ステータス | Execution のライフサイクル。内部の投影値と HTTP の値で綴りが違う | 投影（`executions.status`）は `Running` / `Completed` / `Failed` / `Cancelled` / `Unknown`（`ExecutionProjectionStatuses`）。HTTP は `ACTIVE` / `COMPLETED` / `FAILED` / `CANCELED` / `UNKNOWN`（`ExecutionStatusMapper`） | 投影の `Running` は HTTP では `ACTIVE`、`Cancelled` は `CANCELED`。Fact の `Cancelled` は遷移のキーで、実行ステータスではない |
| Execution Context | — | 1 つの Execution の実行時データ。定義 YAML のパス式（`$.input` / `$.vars` / `$.states` など）の評価の起点になる | `WorkflowExecutionContext`（.NET の `ExecutionContext` と衝突しないための型名） | Execution そのものではない。型名の `Workflow` は旧語ではない |
| Fact | — | 完了・失敗・キャンセル・合流など、状態で実際に起きた結果。遷移は Fact だけで確定する | `Fact.Completed` / `Failed` / `Cancelled` / `Joined`、定義 YAML の `on`、`CompiledFactTransition` | キャンセル**要求**は Fact ではない。監査イベント・待機イベントとも別（[Command・Event・Fact](#commandeventfact)） |
| Fork / Join | — | 並列分岐と合流を表す制御ノード。状態そのものではない | 定義 YAML の `fork` / `join` | — |
| 物理子 Execution | — | Hosted Runtime が Fork の分岐ごとに作る子の Execution。親子の関係は `execution_branches` が正本 | `execution_branches`、`ForkChildExecutionCoordinator` | `workflow.invoke` で起動する子ワークフローとは別（[2 種類の子 Execution](#2-種類の子-execution)） |
| Wait | 待機 | 外部イベントや再開を待つ、永続化される待機 | `EventWait`、`waitKey`、`execution_waits` テーブル | — |
| 待機イベント | — | Wait を再開させる外部からの入力。配送すると監査イベント `EventPublished` が残る | `POST /v1/executions/{id}/events`、`POST /v1/events`、`POST /v1/executions/{id}/nodes/{nodeId}/resume`、`ExecutionWaitEventService` | Fact ではない（再開後に状態が出す結果が Fact）。監査イベントの記録そのものとも別 |
| Cancel | — | 協調的なキャンセル。要求は Command として受け付け、遷移は状態がキャンセルを処理した Fact まで保留される | `POST /v1/executions/{id}/cancel` | Abort / Kill は使わない（強制終了はしない） |
| Resume | — | 待機中の Execution を再開すること | `ExecutionWaitEventService` | — |
| Schedule | 定期実行 | 決まった時刻に Execution を開始する設定。発火時の Start は run-as の ServiceAccount が呼び出し元になる | `/v1/schedules`、`ExecutionSchedule*` | Job は使わない。`JobKey` はシステムが作る定期実行（滞留した実行のレポートなど）を見分けるキー |

詳細は [execution-model.md](../concepts/execution-model.md) と [specifications/execution/](../specifications/execution/)。

## 状態とグラフ

| 用語 | UI | 意味 | 書き方 | 使わない語・区別 |
| --- | --- | --- | --- | --- |
| ExecutionGraph | — | 実行時グラフ。ノードと遷移（edge）を持つ | `GET /v1/executions/{id}/graph`、`ExecutionGraph` | — |
| Node | ノード | 実行グラフ上の 1 つの実行単位。同じ State を複数回訪れると、訪問ごとに Node ができる | `nodes[*].nodeId`、`ExecutionNode` | Step / Stage は使わない |
| nodeId | — | ランタイムが採番する実行ノードの ID（opaque） | `nodeId` | 定義上の状態名 `nodeName` とは別 |
| nodeName | — | Node に対応する定義上の状態名（StateName と同じ値） | `nodeName` | 旧キー `stateName` は使わない |
| nodeType | — | Node の種別 | `nodeType`（`Start` / `Task` / `Fork` / `Join` / `Wait` / `End`） | Node は独自の状態文字列を持たない。終わったかどうかと結果は `completedAt` と `fact` で表す。IDLE / READY などの語は実装に無い |

詳細は [specifications/execution/execution-graph.md](../specifications/execution/execution-graph.md)。

## Action と Module

| 用語 | UI | 意味 | 書き方 | 使わない語・区別 |
| --- | --- | --- | --- | --- |
| Action | — | State に紐づく実行可能な単位 | 定義 YAML の `action`、`actionId`、`IActionExecutor` | Task / Activity は使わない（`Task` は C# の型と `nodeType` の値 `Task` にだけ使う）。エンジンの副作用の総称（`fsm.md` の Effect）を Action と呼ばない |
| actionId | — | Action の論理 ID。`{moduleId}.{actionName}` の形で、版を含まない | `actionId` | — |
| Module | — | Action を束ねた配布単位。zip にまとめ、署名・信頼レベル付与の対象になる | `module.json`、`ModuleHost`、`IModuleSource` | Plugin / Package は使わない |
| Catalog | — | 登録済み Action の一覧。publish 時に Action ID を照合する | `IActionCatalog`、`ActionDescriptor` | — |
| TrustLevel | — | 署名と検証結果から決まる Module の信頼レベル | `TrustLevel` | — |
| Execution Mode | — | Action をどの隔離レベルで動かすかの契約（InProcess / OutOfProcess / Container 等） | `ActionExecutionMode` | 実装は Backend と呼ぶ |
| Backend | — | Execution Mode を満たす具体的な実装。1 つの Mode に複数あり得る | `IActionExecutionBackend` | — |
| Policy | — | TrustLevel・環境・テナント下限から Execution Mode を決める規則。下限は緩められない | `IActionExecutionPolicy`、`IExecutionPolicyProvider` | — |
| Action Host | — | OutOfProcess で Action を動かす gRPC のプロセス | `dotnet/service/action-host` | — |

詳細は [actions.md](../concepts/actions.md) と [specifications/actions/](../specifications/actions/)。

## イベントと永続化

| 用語 | UI | 意味 | 書き方 | 使わない語・区別 |
| --- | --- | --- | --- | --- |
| Command | — | 外部からの操作要求（実行の Start / Cancel / 待機イベントの配送、定義の publish など）。監査イベントを残すのは実行への Start / Cancel / 待機イベントの配送だけ | `command_dedup` | Fact とは別（要求であって結果ではない） |
| 監査イベント（Event） | イベント | Execution に起きたことを `event_store` に残した記録。種別は 3 つだけ | `event_store.type` の値 `WorkflowStarted` / `WorkflowCancelled` / `EventPublished`（`EventStoreEventType`） | Fact ではない。`WorkflowCancelled` はキャンセルの処理が終わった後に残る。種別名の `Workflow` は旧語（[旧語として残る Workflow](#旧語として残る-workflow)）。`execution_events` テーブル（列 `execution_event_id`）は別のテーブルで、今の実装は書き込まない |
| Read Model | — | UI と `GET /v1/executions*` が返す、表示用の正本 | `executions`、`execution_graph_snapshots` | Operational Projection とは別 |
| Operational Projection | — | Worker の運用のための投影。Read API の正本ではない | `execution_cursors`、`execution_waits` | Read Model とは別 |
| 冪等キー | — | 同じ Command やイベント配送を二重に適用しないための記録 | `command_dedup`、`event_delivery_dedup` | — |
| スキーマグループ | — | DB スキーマの文書で、テーブルをまとめて呼ぶ名前 | `KnowledgeSpace`（定義・Project など）/ `ExecutionSpace`（実行の記録など）。[database-schema.md](database-schema.md) の表の列 | コードの型・名前空間・DB スキーマではない |

詳細は [durability.md](../concepts/durability.md) と [specifications/data-integration.md](../specifications/data-integration.md)。

## テナントと認証

| 用語 | UI | 意味 | 書き方 | 使わない語・区別 |
| --- | --- | --- | --- | --- |
| Tenant | テナント | データとアクセスを分ける最上位の境界。認証後も常にテナント単位で検証する | `X-Tenant-Id`（値は `tenant_key`）、`TenantId` | Organization / Workspace は使わない |
| Project | プロジェクト | テナント内で定義をまとめ、Principal の所属で認可する単位。定義は必ずどれか 1 つの Project に属する | `projectId`、`/v1/projects` | — |
| Principal | プリンシパル | 認証された呼び出し主体。User と ServiceAccount がある | `principal_id`、`PrincipalId` | 「主体」と訳さない。Identity / Subject は使わない（JWT の `sub` は互換のための別名） |
| ServiceAccount | — | 人ではない Principal。API キーと定期実行の run-as に使う | `ServiceAccount`、`runAsPrincipalId` | — |
| Actor | — | イベントを起こした者の記録。Principal のほか、system / scheduler / external がある | `actor_kind`、`actor_id` | Principal と同じではない（scheduler などを含む） |
| Permission Key | — | 操作ごとの権限の名前 | [permission-keys.md](permission-keys.md) | — |

詳細は [platform.md](../concepts/platform.md) と [specifications/platform/security-runtime.md](../specifications/platform/security-runtime.md)。

## 紛らわしい組

### State と Node

State は**定義上**の状態で、名前で識別する。Node は**実行グラフ上**の実行単位で、ランタイムが採番する `nodeId` で識別する。同じ State を複数回訪れると、訪問ごとに Node ができる（Studio のキャンバスは同じ `nodeName` の訪問を 1 つにまとめて表示する）。実行グラフの `edges[*].from` / `to` が指すのは `nodeId` で、状態名ではない。

### 2 種類の子 Execution

| | 物理子 Execution（Fork） | 子ワークフロー（`workflow.invoke`） |
| --- | --- | --- |
| 作る契機 | Hosted Runtime が Fork を展開するとき | 定義の State が `statevia.action.builtin.workflow.invoke` を呼ぶとき |
| 定義 | 親と同じ定義 | `input.definitionId` で指定した別の定義 |
| 親への戻り | Join が子の完了を待つ | 親は子の終端を待たない（開始直後の状態を返す） |
| UI の見え方 | GET の graph / events で親と合成した論理 1 実行 | 別の Execution |

### Command・Event・Fact

Command は外部からの**要求**、Fact は状態で実際に起きた**結果**、監査イベントは Execution への要求の受理と処理を `event_store` に残した**記録**。キャンセルの要求は Command であり、遷移を確定させる Fact ではない。

| | Command | Fact | 監査イベント | 待機イベント |
| --- | --- | --- | --- | --- |
| 何か | API が受ける要求 | Engine の遷移のキー | `event_store` の行 | Wait を再開させる入力 |
| 値 | Start / Cancel / 待機イベントの配送 / 定義の publish など | `Completed` / `Failed` / `Cancelled` / `Joined` | `WorkflowStarted` / `WorkflowCancelled` / `EventPublished` | 定義の Wait が受け付けるイベント名 |
| 残る場所 | `command_dedup`（冪等キー） | 実行グラフの `fact`、遷移の評価 | `event_store` | 配送すると `EventPublished` が残る |

定義の publish や定期実行の保存など、Execution を動かさない Command は監査イベントを残さない。

## 旧語として残る Workflow

以前は実行中のインスタンスも Workflow と呼んでいたが、現在は Execution に統一している（DB は `RenameWorkflowsToExecutions` で移行済み）。次の名前は、保存済みのデータや公開済みの契約と一致させるため、旧語のまま残っている。

| 名前 | 場所 | 意味 |
| --- | --- | --- |
| `WorkflowStarted` / `WorkflowCancelled` | `event_store.type` の値（`EventStoreEventType`） | Execution の開始とキャンセル |
| `workflowId` | `workflow.invoke` Action の output | 起動した子 Execution の ID |
| `WorkflowId` | 過去のマイグレーション | 移行前の列名（履歴なので変えない） |
| `ChildWorkflowRunner` / `ChildWorkflowRequest` / `ChildWorkflowResult` | Service API の内部型 | `workflow.invoke` で子 Execution を起動する処理 |

新しい名前で、実行中のインスタンスを Workflow と呼ぶことはない。
