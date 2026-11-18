# 監査・再現性の構想（Level 2: ハッシュチェーン）

| 項目 | 値 |
| --- | --- |
| 種別 | Future |
| Version | 1.1 |
| 更新日 | 2026-11-18 |
| Focus | 完全監査可能 / 再現性 / BI |
| 関連 | [product-roadmap.md](product-roadmap.md), [../specifications/platform/execution-security-snapshot.md](../specifications/platform/execution-security-snapshot.md), [../reference/glossary.md](../reference/glossary.md) |

---

**本書は未実装の設計で、現行の契約ではない。** 2026-11-18 の時点で、hash chain（`prev_hash` / `event_hash`）、reducer による再現、本書に出てくる監査イベントの一覧（`COMMAND_ACCEPTED` / `COMMAND_REJECTED`、`ACTION_DISPATCHED`、`NODE_WAITING` など）はどれも実装に無い。実装にあるのは append-only の `event_store` と、actor・相関 ID の列までである。実装にある監査イベントは `WorkflowStarted` / `WorkflowCancelled` / `EventPublished` の 3 種だけで、語は [用語集](../reference/glossary.md) を正とする。監査の正本の設計は別途検討中で、決まった内容は `specifications/` に書く。

**Version 1.1（2026-11-18）**: `specifications/platform/audit-and-repro.md` から移した。現行の契約と誤解されないよう、種別を Future にし、規範表現（MUST / SHOULD）を外した。本文の設計は変えていない。

## 構想の要約

- 監査イベントを改ざん検知できる hash chain で連結する（Level 2 設計）。
- 同じイベント列から同じ状態を再現できるようにする（reducer の純粋性）。
- EventEnvelope は [events-and-commands.md](../specifications/execution/events-and-commands.md) に合わせ、監査に使う項目を足して固定する。

---

## 0. 目的

本構想は以下を実現するためのルールを描く。

- **改ざん検知**（hash chain）
- **完全監査**（誰が・いつ・何を・なぜ）
- **再現性**（同じイベント列 → 同じ状態）
- **BI分析**（イベントから指標を投影可能）

---

## 1. 非交渉ルール（監査の土台）

1. **WriteはEventのみ**

- 状態（executions/node_states）は Event からの投影（派生）

2. **Eventは不変（append-only）**

- Update/Delete はしない
- 訂正は「訂正イベント」を追加で表現する

3. **決定性（Determinism）**

- Reducerは純粋関数
- 同一 executionId についてイベント順序は一意に決まる

4. **因果関係の保存**

- correlationId（外部要求単位）
- causationId（直前原因イベント）

---

## 2. EventEnvelope（監査強化版）

EventEnvelope は [events-and-commands.md](../specifications/execution/events-and-commands.md) に合わせ、監査に使う項目を足して固定する。

### 2.1 常に持たせる項目

- eventId: UUID
- executionId: string
- type: string（固定一覧）
- occurredAt: RFC3339
- actor:
  - kind: system | user | scheduler | external
  - id?: string
- schemaVersion: 1
- payload: object

### 2.2 あると望ましい項目（監査・追跡性）

- correlationId?: string
- causationId?: UUID
- tenantId?: string
- request:
  - ip?: string
  - userAgent?: string
  - requestId?: string
  - endpoint?: string
- policyVersion?: string（ポリシーバージョン）
- tags?: string[]

> 監査の“強さ”は「イベントに十分な文脈があるか」で決まる。
> payloadに業務データを詰めすぎず、監査メタは envelope 側に寄せる。

---

## 3. Hash Chain（改ざん検知）

### 3.1 追加フィールド（DB保持）

- prev_hash: string（hex, 64）
- event_hash: string（hex, 64）
- hash_alg: "sha256"（固定）
- canonical: string（canonical JSON、任意で保存しても良い）

```sql
alter table events
  add column if not exists prev_hash text null,
  add column if not exists event_hash text null,
  add column if not exists hash_alg text not null default 'sha256';

-- execution内の順序で prev_hash を辿る想定のため、execution_id + seq は重要
create index if not exists idx_events_execution_seq2 on events(execution_id, seq);

-- event_hash は将来の検索/検証で使う
create index if not exists idx_events_execution_hash on events(execution_id, event_hash);

-- 可能なら「イベント更新禁止」をDB権限で縛る（運用で）
```

### 3.2 チェーンの定義（execution単位）

- 同一 executionId のイベント列に対して、順序に従い hash を連結する
- 最初のイベントの prev_hash は **GENESIS_HASH** とする

GENESIS_HASH = "000000...000"（64桁の0）
event_hash = SHA256( prev_hash + "\n" + canonical_json(envelope_without_hash_fields) )

### 3.3 重要：hash対象に含めるもの / 含めないもの

**含める**

- EventEnvelope（eventId, executionId, type, occurredAt, actor, correlationId, causationId, schemaVersion, payload, tenantId 等）
- 監査メタ（request等）も含める（改ざん検知の対象）

**含めない**

- prev_hash, event_hash, hash_alg（自分自身を含めると循環するため）

### 3.4 検証

任意の executionId について、seq順に

- prev_hash が直前 event_hash と一致すること
- 再計算した event_hash が一致すること
  を検証できること。

---

## 4. Canonical JSON（決定的シリアライズ）

### 4.1 目的

JSONは通常「キー順」が保証されないため、hash計算には **canonical** が欠かせない。

### 4.2 ルール（固定）

- オブジェクトのキーは **辞書順（昇順）** にソート
- 配列の順序は保持（そのまま）
- 数値は JSON の数値表現のまま（文字列化しない）
- null/boolean/string は標準JSON
- 小数の丸めなど「勝手な正規化」はしない
- 文字コードはUTF-8、改行はLF

> canonical_json は「キーを再帰的にソートして JSON.stringify」相当で良い。

---

## 5. 受理/拒否も監査対象にする案

### 5.1 案のイベント（監査の実務で効く）

- COMMAND_ACCEPTED
- COMMAND_REJECTED

拒否理由（409/422）を event として残すことで

- 「誰が何をしようとして拒否されたか」
  が後から完全に追える。

※ ただし core-event 固定一覧に追加する場合は v1.1 で実施（破壊的変更回避）。

---

## 6. 再現性（Reproducibility）

### 6.1 原則

Replay は **外部副作用を呼ばず**、イベント列のみで状態を再現する。

### 6.2 非決定要素の封じ込め

- 現在時刻 → occurredAt に固定（reducerは Date.now を使わない）
- 乱数 → 必要なら seed をイベント payload に残す
- 外部API → 結果/要約/参照IDをイベントに残す（次項）

### 6.3 外部副作用の記録（Action Runner向け）

Slack送信等は以下の“事実”をイベント化する（Action領域のイベントとして別トピックでも良い）

- ACTION_DISPATCHED（何を送るつもりだったか）
- ACTION_SUCCEEDED（外部応答の要約、messageId等）
- ACTION_FAILED（エラーコード、リトライ回数）

これにより

- 再現時に外部を呼ばずとも「同じ結果」を説明できる。

---

## 7. BI投影（最低限の指標）

イベントから以下を投影可能にする（Read DB / DWH）

- 実行数、成功率、失敗率、キャンセル率
- 平均実行時間（EXECUTION_STARTED -> 終端）
- 待機時間（NODE_WAITING の総時間）
- ノードタイプ別の成功率/失敗率
- Cancel要求から確定までの時間（cancelRequestedAt -> canceledAt）

---

## 8. 運用ガイド（Level 2）

- DBの events テーブルは update/delete をさせない（権限でも縛る）
- hash chain 検証ジョブ（夜間）を用意し、異常検知したらアラート
- export（CSV/Parquet）時も event_hash を含めると監査が強くなる
