# コアイベント・コマンド仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 2.0 |
| 更新日 | 2026-11-08 |
| 関連 | [fsm.md](fsm.md), [wait-cancel.md](wait-cancel.md), [../api-http.md](../api-http.md), [../../reference/glossary.md](../../reference/glossary.md), [concepts/execution-model.md](../../concepts/execution-model.md) |

---

## Normative 要約

- **MUST**: 外部からの操作は Command として HTTP で受け、認可と検証を通ったものだけを適用する。Command は Fact ではない。
- **MUST**: 状態の遷移を確定させるのは Fact（`Completed` / `Failed` / `Cancelled` / `Joined`）だけとする。
- **MUST**: `event_store` に残す監査イベントの種別は `EventStoreEventType` の 3 種（`WorkflowStarted` / `WorkflowCancelled` / `EventPublished`）とする。種別を増やすときは、`EventStoreEventType` と本書を同じ変更で更新する。
- **MUST**: Wait を再開させる外部入力は待機イベントとして受け、配送したら `EventPublished` を残す。
- **SHOULD**: 変更系の Command には `X-Idempotency-Key` を付け、同じ Command の二重適用を避ける（[api-http.md](../api-http.md)）。

語の区別は [用語集](../../reference/glossary.md#commandeventfact)、遷移の規則は [fsm.md](fsm.md)、Wait と Cancel の振る舞いは [wait-cancel.md](wait-cancel.md) を正とする。

---

## 1. 4 つの概念

| | Command | Fact | 監査イベント | 待機イベント |
| --- | --- | --- | --- | --- |
| 何か | API が受ける要求 | Engine の遷移のキー | `event_store` の行 | Wait を再開させる入力 |
| 出どころ | 利用者・ServiceAccount・Scheduler | 状態の実行結果、Join の合流 | Execution への Command を適用したとき | 外部システム、利用者 |
| 正本 | 各 HTTP エンドポイント | `Fact`（`Statevia.Core.Engine.FSM`） | `EventStoreEventType` | 定義の `wait.events`（`WaitEventRouteTable`） |

キャンセルの**要求**は Command で、遷移を確定させる Fact ではない。状態が実際に止まったときに Fact `Cancelled` が記録される（[wait-cancel.md](wait-cancel.md)）。

---

## 2. Command

Execution に関わる Command と、監査イベントの対応は次のとおり。冪等キー・認可・応答コードは [api-http.md](../api-http.md) を正とする。

| Command | HTTP | 残る監査イベント |
| --- | --- | --- |
| Start | `POST /v1/executions`、定期実行の発火、`POST /v1/schedules/{id}/run` | `WorkflowStarted` |
| Cancel | `POST /v1/executions/{id}/cancel` | `WorkflowCancelled`（キャンセルの処理が終わった後） |
| 待機イベントの配送 | `POST /v1/executions/{id}/events`、`POST /v1/executions/{id}/nodes/{nodeId}/resume`、`POST /v1/events` | `EventPublished` |

定義の publish、定期実行や Project の保存など、Execution を動かさない Command は監査イベントを残さない。

すでに終端した Execution への Command、二重に送られた Command の扱いは [wait-cancel.md](wait-cancel.md) と [api-http.md](../api-http.md) に従う。冪等キーで同じ Command と判定したときは、監査イベントを重ねて残さない。

---

## 3. Fact

| Fact | 意味 |
| --- | --- |
| `Completed` | 状態が正常に終わった。Wait の解消後の正常終了も `Completed` |
| `Failed` | 状態の実行器が例外を投げた |
| `Cancelled` | 協調的なキャンセルで状態が実際に止まった |
| `Joined` | Join の待つ枝がそろい、Join の状態が実行できるようになった |

Fact は実行グラフのノードの `fact` に記録される（[execution-graph.md](execution-graph.md)）。Fact をどう遷移へ結び付けるかは [fsm.md](fsm.md) を正とする。

---

## 4. 監査イベント

### 4.1 event_store の列

監査イベントは Execution ごとに `seq` で並ぶ append-only の行として `event_store` に残る。列の定義は [database-schema.md](../../reference/database-schema.md) を正とする。

| 列 | 内容 |
| --- | --- |
| `event_id` | イベントの一意 ID |
| `execution_id` / `seq` | どの Execution の何番目か |
| `type` | `EventStoreEventType` の値（enum 名をそのまま保存する） |
| `occurred_at` | 発生日時 |
| `actor_kind` / `actor_id` | 起こした者。いまは定期実行の発火による `WorkflowStarted` だけが `scheduler` とスケジュールの ID を書く。ほかの経路は空 |
| `correlation_id` / `causation_id` | 列はあるが、いまは書かない |
| `schema_version` | payload の版 |
| `payload_json` | 種別ごとの payload（下記） |

### 4.2 種別と payload

| 種別 | 残るとき | payload の主な項目 |
| --- | --- | --- |
| `WorkflowStarted` | Execution の開始。Hosted の物理 Fork の子を開始するときも残る | `definitionId`、`definitionVersionId`、`definitionVersion`、`tenantId`、`startedByPrincipalId`、`permissionSetHash`、`securityModelVersion`、`evaluationMode`。子の開始では `parentExecutionId`、`forkNodeId`、`branchState`、`joinState` を持つ |
| `WorkflowCancelled` | キャンセルの処理が終わった後。開始前の Execution を取り消したときも残る | `tenantId` |
| `EventPublished` | 待機イベントを配送した後 | `tenantId`、`name`（イベント名）。ノードを指定した再開では `nodeId` も持つ |

種別名の `Workflow` は旧語で、保存済みのデータと一致させるために残している（[用語集](../../reference/glossary.md#旧語として残る-workflow)）。タイムラインの GET（`GET /v1/executions/{id}/events`）の形は [api-http.md](../api-http.md)、物理 Fork の親子を合成する規則は [fork-join.md](fork-join.md) を参照する。

---

## 5. 待機イベント

待機イベントは、定義の Wait が受け付けるイベント名を持つ外部入力である。配送の経路は 3 つある。

- `POST /v1/executions/{id}/events`: Execution を指定して、イベント名で配送する
- `POST /v1/executions/{id}/nodes/{nodeId}/resume`: Wait のノードを指定して再開する（再開の正本）
- `POST /v1/events`: topic と key に一致する購読へまとめて配送する

再開の後に状態が正常に終わると Fact `Completed` が記録され、遷移先はイベント名で `WaitEventRouteTable` を引いて決まる（[wait-cancel.md](wait-cancel.md)）。待機イベントそのものは Fact ではない。

---

## 6. 本書の範囲外

以前の版にあった 24 種のイベント一覧、12 種のコマンド一覧、それらを適用する reducer の規則は実装されておらず、Version 2.0 で外した。監査の改ざん検知と Actor の拡充は [audit-and-repro.md](../platform/audit-and-repro.md) の段階的な目標である。
