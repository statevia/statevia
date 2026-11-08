# FSM 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.5 |
| 更新日 | 2026-11-08 |
| 関連 | [concepts/execution-model.md](../../concepts/execution-model.md), [wait-cancel.md](wait-cancel.md), [events-and-commands.md](events-and-commands.md), [../../reference/glossary.md](../../reference/glossary.md) |

---

## Normative 要約

- **MUST**: 遷移は `(状態, 事実) → 結果` で評価する。リクエスト（キャンセル要求等）は事実ではない。
- **MUST**: 事実（`Completed` / `Failed` / `Cancelled` / `Joined`）以外で遷移してはならない。
- **SHOULD**: Wait 解消後の正常終了は FSM 上 `Completed` 事実として扱う。
- **MUST**: `Failed` 事実でも `on.Failed` を `Completed` と同じ規則で評価する。遷移が無いときだけ実行を終端失敗とし、`Cancelled` とは記録しない（下記「失敗事実と on.Failed」）。
- **MUST**: 実行器の無い状態に遷移したら、`on.Failed` を辿らずに実行を Failed にする（下記「実行器の無い状態」）。

背景は [execution-model](../../concepts/execution-model.md)、語の区別は [用語集](../../reference/glossary.md) を参照。

**Version 1.5（2026-11-08）**: 実装されていない状態機械の章（ノード状態 IDLE / READY など、24 種のイベント、reducer と優先順位の関数）を外し、用語とステータスを実装に合わせた。

---

statevia は事実駆動型有限状態機械を使用します。

## コアコンセプト

遷移は以下によって評価されます：

(状態, 事実) -> 遷移結果

事実は実際の実行結果を表します：

- Completed（完了）
- Failed（失敗）
- Cancelled（キャンセル済み）
- Joined（結合済み）

リクエスト（例：キャンセルリクエスト）は事実ではありません。

### Wait 状態と事実（実装に準拠）

YAML の **`wait.events`**（例: `approve` → `Approved`）は、外部から Resume された **イベント名**と突き合わせ、`IEventProvider.WaitForEventAsync` が再開するために使う識別子である。コンパイル後の正本は **`WaitEventRouteTable`**。

待機が解けて状態の `ExecuteAsync` が正常終了したあと、エンジンが JoinTracker / 通常の成功経路に渡す事実は **`Completed` 固定**である（Join 互換）。一方、**次状態の解決**は、Engine がそのノードの再開に使ったイベント名で route table の Next を引く（単一イベントの旧定義で Next が空のときのみ `on.Completed` へフォールバック）。状態の output の形には依らない。遷移先が決まらない Wait は実行を Failed にする（[wait-cancel.md](wait-cancel.md)）。

したがって:

- **公開 DSL** では遷移先を `wait.events` に書く（複数イベント可）。
- **`on.<イベント名>` は評価されない**（イベント名と FSM の事実名を混同しないこと）。
- 旧 `wait.event` + `on.Completed.next` は Loader が `events` へ正規化する。

### 失敗事実と on.Failed

状態の `ExecuteAsync` が例外を投げると、ノードは事実 `Failed` で記録される。エンジンは状態の事実を次の順で扱う（`ExecutionEngine.ProcessFact`）。

1. 実行が終端失敗していれば、遷移しない（並行していた状態が停止して返す `Cancelled` も含む）。
2. 事実が `Cancelled`（明示 Cancel や Action の取り消し）なら、実行を Cancelled で終える。
3. 実行が取り消されていれば、遷移しない。
4. `Completed` と `Failed` は同じ遷移評価を通る。`on.Failed` の `next` / `cases` / `default` / `end` を辿る（Nodes の `error` は `on.Failed.next`）。
5. `Failed` で遷移が無いときだけ、実行を終端失敗にする。並行する状態の Action は止めるが、実行は Cancelled とは記録しない。投影の状態は FAILED、`cancel_requested` は false。

状態の完了は Join に記録しない。Join への記録は、遷移で Join に入ったときに行う（[fork-join.md](fork-join.md)）。そのため、枝の中で `on.Failed` を辿り当該 Join へ入った枝も、Hosted と Engine 単体のどちらでも Join に記録される（Hosted では子が Completed で終わる）。

### 実行器の無い状態

Join 以外の状態は、`IStateExecutorFactory.GetExecutor` で実行器を取って動かす。実行器が取れない（null が返る）状態に遷移したとき、エンジンは次のように扱う（`ExecutionEngine.ScheduleStateAsync`）。

- 実行を Failed にし、並行する状態の Action を止める。実行は Cancelled とは記録しない。
- その状態のノードは作らない。`on.Failed` / `error` も辿らない。状態の業務の失敗ではなく、定義と実行器の登録の食い違いであるため。
- ログ（EventId 50022、Error）に `ExecutionId` / `DefinitionName` / `StateName` を残す。

`DefinitionCompiler` は既定で、コンパイル時にこの食い違いを検出する（[engine-standalone-guide.md](../../guides/engine-standalone-guide.md)）。Service API では、Action の割り当てと登録の検証がコンパイルより前に済んでいるため、実行時にこの経路へは入らない。

## FSM の特性

- 決定論的遷移
- 暗黙の状態遷移なし
- 自己遷移は禁止
- 遷移は O(1) ルックアップテーブルに事前コンパイルされる

## 遷移結果

遷移結果は以下を行うことができます：

- 新しい状態を開始する
- 現在の状態を終了する
- Fork または Join ロジックをトリガーする

---

## 用語

語の意味と書き方の正本は [用語集](../../reference/glossary.md)。本書では次のように使う。

- **State**: 定義上の状態。Action を持ちうる。遷移表のキーの片方
- **Fact**: 状態で実際に起きた結果。遷移表のキーのもう片方（`Completed` / `Failed` / `Cancelled` / `Joined`）
- **Command**: API が受ける要求（Start / Cancel / 待機イベントの配送など）。Fact ではなく、遷移を直接起こさない（[events-and-commands.md](events-and-commands.md)）
- **Action**: State に紐づくプラットフォームの実行単位。エンジンの副作用の総称ではない
- **Node**: 実行グラフ上の 1 回の状態の実行。同じ State を何度も訪れると、訪問ごとに Node ができる

## ステータス

### Execution

Execution のライフサイクルは、内部の投影値と HTTP の値の二層で表す。

| 投影（`executions.status`） | HTTP | 意味 |
| --- | --- | --- |
| `Running` | `ACTIVE` | 終端していない |
| `Completed` | `COMPLETED` | 正常に終わった |
| `Failed` | `FAILED` | 失敗で終わった |
| `Cancelled` | `CANCELED` | キャンセルで終わった |
| `Unknown` | `UNKNOWN` | スナップショットが無いなど、判定できない |

投影値は Engine のスナップショットから写し（`ExecutionProjectionOrchestrator.MapStatus`）、HTTP の値は API 層で変換する（`ExecutionStatusMapper`）。終端（`Completed` / `Failed` / `Cancelled`）からは戻らない。キャンセルの要求を受けたかどうかは、ステータスとは別の `cancel_requested` で持つ。

失敗とキャンセルが重なったときにどちらで終わるかは、上の「失敗事実と on.Failed」の順で決まる。キャンセルの受け付けと収束は [wait-cancel.md](wait-cancel.md) を正とする。

### Node

Node は独自の状態文字列を持たない。実行グラフでは次の項目で表す（[execution-graph.md](execution-graph.md)）。

- `nodeType`: `Start` / `Task` / `Fork` / `Join` / `Wait` / `End`
- `completedAt`: 終わっていなければ `null`
- `fact`: 終わったときの Fact
- `canceledByExecution`: 実行全体のキャンセルで止まったか
