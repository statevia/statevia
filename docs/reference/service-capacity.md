# サービス容量（暫定指針）

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 0.15 |
| 更新日 | 2026-09-18 |
| 関連 | [capacity-load-testing.md](../guides/capacity-load-testing.md), [environment-variables.md](environment-variables.md), [data-integration.md](../specifications/data-integration.md), [wait-cancel.md](../specifications/execution/wait-cancel.md) |

---

**Version 0.15（2026-09-18）**: 1×16 で L1 / L2 / L3 / C1 / D1 を再計測。L1 は 512 Completed・約 58 exec/s。L2 は 16/16。C1 は Cancel 128/128。D1 は 2 回とも 300s タイムアウト（不合格）。

**Version 0.14（2026-09-18）**: L1O 1×16・512 件を初計測。512 Completed、完了レートは約 7.5 exec/s。

**Version 0.13（2026-09-18）**: L1O（builtin sleep 500ms のスロット占有）をハーネスに追加。

**Version 0.12（2026-09-18）**: 分離 compose の Worker `MaxConcurrency` 未設定時を 16 にする。v0.5 暫定上限は当時 4 の計測のまま。

**Version 0.11（2026-09-18）**: 書き込み削減後に Worker プロセス × スロット格子を再計測。L1 最高は約 51 exec/s（3×8）。1×64 は完了。3×64 のみタイムアウト。

参照構成で測った処理件数・レートの **目安** です。製品 SLA、可用性 %、RPO/RTO、テナント硬クォータは扱いません。手順は [負荷・耐久計測ガイド](../guides/capacity-load-testing.md) です。

## 免責

- 値は **HW・トポロジ・設定・git 版** に依存する暫定指針です。契約上の上限ではありません。
- **Start の HTTP 受理レート** と **実行完了レート** は一致しません。Worker が queued Start を処理するまで完了しません。
- 単体テスト・シナリオテストの件数は、本表の根拠にしません。
- 生ログや生 CSV は公開しません。要約だけを本表に載せます。
- L1 はランプ中に 5xx / 失敗 / p95 × N 超過が起きていません。v0.5 表の値は **閾値未超過の最高ステップ** であり、破断点ではありません。v0.6 格子のタイムアウトセルは破断点です。
- L2 は Resume 失敗（非 2xx）がゼロの最高ステップを上限とします。投影がすでに終端の遅い Resume は **204** です。split-runtime では 32 件ウェーブが WAITING 待ちでタイムアウトしたため、16 は破断点の直前ステップです。

## 計測スタンプ

| 項目 | 値 |
| --- | --- |
| 計測日（UTC） | 2026-09-09（split-runtime 再計測） |
| git | `fdb57c2`（ハーネスが読んだ HEAD。計測時の Scheduler イメージは再ビルド済み） |
| HW ラベル | `ref-dev`（Windows、論理プロセッサ 16、メモリ約 80 GiB） |
| トポロジ | `split-runtime`（`docker-compose.split-runtime.yml`。Worker / Scheduler は別プロセス。Worker `MaxConcurrency` は v0.5 計測時 compose **4**。現行既定は **16**） |
| p95 ベースライン倍率 N | **3**（L1 ベースラインはウォームアップ後 count=8 の Start 受理 p95） |

`ref-cloud-small` は未計測です。

## 参照構成（split-runtime）

| コンポーネント | 台数 | 備考 |
| --- | --- | --- |
| PostgreSQL 16 | 1 | 既定 compose |
| Service API | 1 | プロセス内 HostedService は Off。UI は計測対象外 |
| Worker | 1 | `execution_work_items`。compose の `MaxConcurrency` は 16（v0.5 表は当時 4） |
| Scheduler | 1 | DelayWait 掃引と Ownership Recovery |
| Action Host | 0 または 1 | L1 / L2 の builtin `noop` では不要 |

起動手順は [operations-docker.md](../guides/operations-docker.md) のランタイム分離。本表は **単一 API** のみです。マルチ API は未検証です（DelayWait の重複 enqueue リスク）。

Compose に `cpus` / `mem_limit` が無いため、実効 vCPU / メモリは計測メタデータに残します。

## 暫定上限

ランプ中に閾値を超えた直前ステップを上限とします。超えなければ、失敗ゼロの最高ステップを載せ、破断点ではない旨を注記します。

| シナリオ | メトリクス | 暫定上限 | 注記 |
| --- | --- | --- | --- |
| L1 短寿命 | Start 受理 rate | 約 12 req/s | 128 件ウェーブ（concurrency 16）。HTTP 201。5xx なし |
| L1 短寿命 | 実行完了 rate | 約 12 exec/s | 同上。Worker `MaxConcurrency` 4 |
| L1 短寿命 | Start 受理 p50 / p95 | 90 / 125 ms | 128 件。ウォームアップ後 8 件の p95 は 43 ms（×2.9、N=3 未満） |
| L2 Wait 滞留 | 同時滞留 waits | 16 | Resume 失敗ゼロの最高ステップ（16 件、concurrency 8）。32 件は WAITING 待ちタイムアウト（3 回） |
| L2 Wait 滞留 | Resume 受理 rate | 約 7.7 req/s | 1×16・16 件（v0.15）。HTTP 5xx なし。32 件ウェーブは未再測 |
| L2 Wait 滞留 | Resume 受理 p50 / p95 | 26 / 303 ms | 同上。v0.5 の 58 / 473 ms（slots 4）より短い |
| L3 DelayWait | 期限〜Resume 遅延 | 未計測 | HTTP YAML では DelayWait を定義できない（`wait.timeout` 未使用、投影の wait_kind は常に EventWait）。掃引 poll **5s** は設定下限であり TimerFire の実測ではない。v0.15 プローブも autoCompleted 0 |
| L1O スロット占有 | 実行完了 rate | 約 7.5 exec/s | 1 Worker × スロット 16、512 件。HTTP 5xx なし。理論上限（16 / 0.5s ≒ 32）は下回る。破断点ではない |
| D1 再起動耐久 | 合否 | 不合格（v0.15） | 8 件を 2 回。API restart と health 復帰は成功。グラフ snapshot は数秒で Completed。`executions.status` が追いつかず 300s タイムアウト。履歴の合格は v0.5 |

## Worker プロセス × スロット格子（v0.11）

v0.5 表は 1 Worker・`MaxConcurrency` 4・L1 128 件です。利用者がプロセス数とスロットを決める材料として、書き込み削減後の同じ `ref-dev` / `split-runtime` で縮小格子を再測しました。L2 は入れていません（32 件で GET `/waits` 待ちがタイムアウトするため）。

| 項目 | 値 |
| --- | --- |
| 計測日（UTC） | 2026-09-17〜18 |
| git | `8252a5d2` |
| L1 | count 512、HTTP concurrency 16、制限 300 秒 |
| C1 | count 128、settle 3 秒、HTTP concurrency 16。WAITING snapshot は待たない |
| Worker | `--scale worker=N` と `STATEVIA_WORKER_MAX_CONCURRENCY` / `STATEVIA_WORKER_CANCEL_CONCURRENCY` |

L1 本体は `CancelConcurrency` 1。値は完了レート（exec/s）と Start 受理 p95（ms）。512 件すべて `Completed` にならなかったセルはタイムアウトです。完了してもレートが大きく落ちたセルは数値のまま載せます。

| プロセス \ スロット | 8 | 16 | 32 | 64 |
| --- | --- | --- | --- | --- |
| 1 | 約 32 / 286 | 約 42 / 141 | 約 41 / 150 | 約 41 / 150 |
| 2 | 約 44 / 172 | 約 46 / 173 | 約 43 / 210 | 約 7 / 301 |
| 3 | 約 51 / 238 | 約 47 / 240 | 約 7 / 279 | タイムアウト |
| 4 | 約 42 / 322 | 約 40 / 383 | 約 4 / 396 | 約 4 / 502 |

読み方:

- 最高完了レートは **約 51 exec/s**（3 プロセス × 8 スロット）。v0.6 の天井約 30〜32 を上回る
- 1 プロセス × 16 は約 42 / 141。単セル再測（v0.10 約 41 / 182）と整合する
- 1 プロセスではスロット 16 以上で完了レートは約 41 で頭打ち。1 × 64 は完了する（v0.6 はタイムアウト）
- 2〜3 プロセスは 8〜16 スロットで約 44〜51 まで伸びる。Start p95 はプロセス増で悪化する
- 同時スロットが多いセル（2 × 64、3 × 32、4 × 32、4 × 64）は 512 `Completed` でも完了レートが約 4〜7 に落ちる
- 3 × 64 のみ 300 秒タイムアウト（残留 Running 1）
- 4 プロセスは 3 × 8 を超えず、p95 だけ悪化する

C1 角はすべて Cancel 受理 128 / `Cancelled` 128、HTTP 5xx なし。`cancelledRate` は Start + settle 3 秒 + Cancel 待ちを含む実経過時間なので、純 Cancel TPS ではありません。下表は Cancel 受理 p95（ms）と、その実経過時間レート（1/s）。

| プロセス × スロット | ループ 1 | ループ 2 | ループ 4 | ループ 8 |
| --- | --- | --- | --- | --- |
| 1 × 8 | 32 / 13 | 32 / 16 | 35 / 16 | 84 / 8.1 |
| 1 × 64 | 37 / 14 | 32 / 16 | 32 / 16 | 35 / 11 |
| 4 × 8 | 127 / 14 | 110 / 15 | 36 / 14 | 35 / 9.7 |
| 4 × 64 | 137 / 2.7 | 178 / 2.0 | 39 / 3.0 | 109 / 1.8 |

読み方:

- 1 プロセスではループ 2〜4 が波全体レート最大（約 16 /s）。Cancel HTTP p95 は約 32〜37 ms
- ループ 8 は 1 × 8 で波全体のレートが約 8 /s に落ちる（Start スロットと Cancel ループの取り合い）
- 4 プロセスにしても Cancel レートは伸びない。4 × 64 は約 2〜3 /s まで落ち、L1 と同じ大域ボトルネックが見える

### 履歴: v0.6 格子

書き込み削減前（2026-09-09〜10、git `fdb57c2`）。L1 は完了レート / Start p95。C1 は Cancel p95 / 実経過時間レート。

| プロセス \ スロット | 8 | 16 | 32 | 64 |
| --- | --- | --- | --- | --- |
| 1 | 約 22 / 169 | 約 30 / 150 | 約 29 / 150 | タイムアウト |
| 2 | 約 32 / 185 | 約 31 / 198 | タイムアウト | タイムアウト |
| 3 | 約 30 / 270 | 約 31 / 323 | タイムアウト | タイムアウト |
| 4 | 約 30 / 415 | 不安定 | タイムアウト | タイムアウト |

| プロセス × スロット | ループ 1 | ループ 2 | ループ 4 | ループ 8 |
| --- | --- | --- | --- | --- |
| 1 × 8 | 145 / 11 | 54 / 13 | 57 / 15 | 47 / 7.5 |
| 1 × 64 | 103 / 11 | 133 / 12 | 70 / 14 | 48 / 15 |
| 4 × 8 | 161 / 12 | 78 / 12 | 121 / 12 | 167 / 9 |
| 4 × 64 | 137 / 3.0 | 138 / 2.5 | 71 / 2.0 | 104 / 2.1 |

## L1 1×16 書き込み削減後（v0.7）

v0.6 格子の 1 Worker × スロット 16 と同じセル（count 512、HTTP concurrency 16、`pg_stat_reset()` 直後）を、短寿命のプライマリ書き込み削減後に 1 回測りました。格子の他セルは再測していません。

| 項目 | 改修前（v0.6 格子） | 改修後 |
| --- | --- | --- |
| 計測日（UTC） | 2026-09-09〜10 | 2026-09-16 |
| `execution_runtime_checkpoints` UPDATE / DELETE | 約 3076 / 0 | 0 / 512 |
| `executions` UPDATE | 約 1540 | 約 1033 |
| `execution_graph_snapshots` UPDATE | 約 1540 | 1024 |
| `execution_cursors` | INSERT+DELETE 512 | 0 |
| 完了 | 512 `Completed` | 512 `Completed`、HTTP 5xx なし |
| 完了レート | 約 30 exec/s | 約 40 exec/s |
| Start 受理 p95 | 約 150 ms | 約 371 ms |

短寿命では checkpoint は lease 用 INSERT のあと終端で DELETE し、所有中の JSON refresh は走りません。snapshot は JSON 変化時だけ書くため、実行あたり約 3 UPDATE から 2 になりました。計測直前の Running drain が `pg_stat_reset()` 後に数件分の work item / status 更新を足しているため、`executions` の 1033 と `execution_work_items` INSERT 520 は L1 本体 512 件よりわずかに多いです。

## L1 1×16 空 Claim 削減後（v0.9）

同じセル（count 512、HTTP concurrency 16、1 Worker × スロット 16、`pg_stat_reset()` 直後）を、空 Claim の EXISTS skip 後に再測しました。Worker イメージは第二段の `ClaimAsync` を含みます。

| 項目 | 第一段後（v0.7） | 第二段後 |
| --- | --- | --- |
| 計測日（UTC） | 2026-09-16 | 2026-09-17 |
| `execution_runtime_checkpoints` UPDATE / DELETE | 0 / 512 | 0 / 512 |
| `executions` UPDATE | 約 1033 | 1024 |
| `execution_graph_snapshots` UPDATE | 1024 | 1022 |
| `execution_work_items` INSERT / UPDATE / DELETE | 520 / 512 / 512 | 512 / 512 / 512 |
| `execution_cursors` | 0 | 0 |
| 完了 | 512 `Completed` | 512 `Completed`、HTTP 5xx なし |
| 完了レート | 約 40 exec/s | 約 47 exec/s |
| Start 受理 p95 | 約 371 ms | 約 202 ms |
| アイドル 30 秒の work item UPDATE | （未測） | 0（タプル更新 0、WAL レコード +3） |

アイドルでは Worker が 1 秒ごとに EXISTS を読むだけです。空の Claim UPDATE は開きません。L1 本体の work item UPDATE 512 は実 claim 分です。v0.9 の完了レート約 47 は、511 件タイムアウト後の再走です。

## L1 1×16 Unload 修正後（v0.10）

同じセルを、Running 投影のあとに live 終端で Engine を落とさない修正込みで再測しました（git `8252a5d2`）。1 回目のウェーブで 512 `Completed` です。

| 項目 | 第二段後（v0.9 再走） | Unload 修正後 |
| --- | --- | --- |
| 計測日（UTC） | 2026-09-17 | 2026-09-17 |
| `execution_runtime_checkpoints` UPDATE / DELETE | 0 / 512 | 0 / 512 |
| `executions` UPDATE | 1024 | 1026 |
| `execution_graph_snapshots` UPDATE | 1022 | 1024 |
| `execution_work_items` INSERT / UPDATE / DELETE | 512 / 512 / 512 | 512 / 512 / 512 |
| `execution_cursors` | 0 | 0 |
| 完了 | 512 `Completed` | 512 `Completed`、HTTP 5xx なし |
| 完了レート | 約 47 exec/s | 約 41 exec/s |
| Start 受理 p95 | 約 202 ms | 約 182 ms |
| アイドル 30 秒の work item UPDATE | 0（WAL +3） | 0（タプル更新 0、WAL レコード +1） |

完了レートは第一段後の約 40 exec/s を下回っていません。v0.9 の 47 との差はウェーブ間のばらつきとして読みます。

## L1O 1×16 スロット占有（v0.14）

noop の L1 とは別に、builtin `sleep` 500ms でスロットを握るセルを 1 回測りました（`ref-dev` / split-runtime、1 Worker × スロット 16、count 512、HTTP concurrency 16、git `314a519b`）。

| 項目 | 値 |
| --- | --- |
| 完了 | 512 `Completed`、HTTP 5xx なし |
| 完了レート | 約 7.5 exec/s |
| Start 受理 p50 / p95 | 約 109 / 227 ms |
| 実経過時間 | 約 68 s |
| 理論上限（スロット / 0.5s） | 約 32 exec/s |

sleep 以外に work item poll（1s）と終端検知（AwaitLoad 250ms）が乗るため、理論上限には達しません。CPU は焼いていません。

## 1×16 再計測（v0.15）

L1O と同じセル（`ref-dev` / split-runtime、1 Worker × スロット 16、git `314a519b`）で、残りのハーネスを流しました。格子は再走していません。

| シナリオ | 件数 | 結果 |
| --- | --- | --- |
| L1 | 512、HTTP concurrency 16 | 512 `Completed`、HTTP 5xx なし。完了レート約 58 exec/s。Start p50 / p95 約 99 / 191 ms。実経過時間約 9 s |
| L2 | 16、HTTP concurrency 8 | waits 16、Resume 16/16、Completed 16。Resume p50 / p95 約 26 / 303 ms。実経過時間約 2 s |
| L3 | 8 | `delayWaitHttpAuthorable` 0。WAITING 8 のあと leftover を Cancel。TimerFire は起きない |
| C1 | 128、settle 3 s | Cancel 受理 128、`Cancelled` 128、HTTP 5xx なし。Cancel p50 / p95 約 38 / 67 ms。実経過時間レート約 16 /s |
| D1 | 8 | 2 回とも不合格。restart / health は成功。300 s で `TaskCanceledException` |

L1 の 1 回目は 300 s タイムアウト（JSON なし）。再走で 512/512 です。v0.11 格子の 1×16 約 42 exec/s との差は、単セルのばらつきとして読みます。

D1 は API 再起動後、グラフ snapshot が数秒で Wait を含む全ノード `Completed` になる一方、`executions.status` はハーネスが Cancel するまで Running のままです。合否は GET execution の status を見るため、投影のずれでタイムアウトします。L2（再起動なし）は 16/16 完了するので、Wait / Resume そのものは生きています。

## 履歴: Phase 0（プロセス同居）

Version 0.4 までの参照構成は `phase0-single-api`（既定 compose、Worker / Scheduler は API プロセス内、`MaxConcurrency` 既定 1）でした。2026-09-08〜09 の `ref-dev` 要約:

| シナリオ | メトリクス | 当時の値 |
| --- | --- | --- |
| L1 | Start / 完了 rate、p50 / p95 | 約 13 req/s、66 / 103 ms（128 件） |
| L2 | 同時滞留、Resume rate、p50 / p95 | 32、約 10 req/s、66 / 289 ms |
| D1 | 合否 | 合格（8 件。API 再起動は in-process Worker も含む） |

## スケールの読み方

- **受理 ≠ 完了。** `POST /v1/executions` の 201 はキュー投入です。完了は Worker の claim と投影更新の後です。
- **L1O 占有。** builtin `sleep` 500ms でスロットを握ります（CPU は焼きません）。1×16・512 件の完了レートは約 7.5 exec/s で、理論上限（約 32 exec/s）を下回ります。work item poll 1s と終端待ちが乗ります。noop の L1 書き込み天井とは別物です。
- **Wait / Resume。** 滞留中の実行は Running のまま waits を持ちます。再開の正本は `POST /v1/executions/{id}/nodes/{nodeId}/resume` です。GET `/waits` はグラフ snapshot です。WAITING が揃わないと L2 ウェーブはタイムアウトします。
- **DelayWait。** 期限検知の poll（既定 5s）が下限になります。現行の HTTP Definition では DelayWait を定義できないため、期限〜Resume の実測セルは未計測です。
- **投影。** キューはグローバル直列です。Worker を並列にしても投影が遅延し得ます。契約は [data-integration.md](../specifications/data-integration.md)、設定キーは [environment-variables.md](environment-variables.md) の `ExecutionProjectionQueue:*` です。
- **耐久 D1。** 再起動後に Resume でき、投影が壊れないことが合否です。件数の宣伝には使いません。v0.15 ではグラフ snapshot が Completed でも `executions.status` が遅れると不合格になります。
- **Worker 格子。** v0.11 では 3×8 で約 51 exec/s です。1 プロセスは 16 スロット付近で約 41〜42（v0.15 単セルは約 58）。同時スロットが多いセルは完了してもレートが約 4〜7 に落ち、3×64 はタイムアウトします。Start p95 はプロセスを増やすと悪化します。投影キュー（グローバル直列）または PostgreSQL が先に飽和し得ます。

上限の解釈に使う実装ボトルネック（v0.11 格子。短寿命の checkpoint / snapshot 回数は v0.7 以降で削減済み）:

| 要因 | 現行の目安 | 設定 / 場所 |
| --- | --- | --- |
| Start / Resume のプロセス内同時件数 | 専用 Worker の compose 未設定時は 16（API 内既定は 1。範囲 1〜64）。1 プロセスは 16 付近で完了レートが頭打ち。格子の最高は 3×8 | `Statevia:Runtime:Worker:MaxConcurrency` |
| Cancel 独立ループ | 既定 1（1〜8） | `Statevia:Runtime:Worker:CancelConcurrency` |
| work item poll / lease | poll 1s、lease 1 分。claim 件数は空きスロット数。未完了 item が無いときは EXISTS のみ（空 UPDATE なし） | ランタイム Worker |
| 無進捗 watchdog | 既定 10 分（長い Action は対象外） | `Statevia:Runtime:Worker:NoProgressTimeout` |
| DelayWait 掃引 | poll 5s、batch 64 | ランタイム Scheduler |
| Engine ステート並列 | `MaxParallelism` 既定 4 | 同時 execution 数の上限ではない |
| 投影キュー | 有界・1 コンシューマ・debounce 既定 50ms | `ExecutionProjectionQueue:*` |

## 関連設定

| キー | 容量との関係 |
| --- | --- |
| `Statevia:Runtime:EnableInProcess*` | 現行参照は Off（split-runtime）。既定 compose は On |
| `Statevia:Runtime:Worker:MaxConcurrency` | 完了レートの主因になりやすい |
| `Statevia:Runtime:Worker:CancelConcurrency` | Cancel 負荷。L1 / L2 の主経路ではない |
| `Statevia:Runtime:Worker:NoProgressTimeout` | 長時間 Running の Unload |
| `Statevia:Runtime:Worker:MaxAttempts` | 恒久失敗の打ち切り |
| `ExecutionProjectionQueue:*` | 投影遅延・ブロックの兆候 |
| `DATABASE_URL` | PostgreSQL がボトルネックになり得る |

詳細は [environment-variables.md](environment-variables.md) と [operations-docker.md](../guides/operations-docker.md) です。

## 改訂ルール

- **メジャー機能のあと**（Worker 分離、新しい Wait 種別、イベント配送の変更など）は Version を上げ、参照構成を書いて再計測します。
- **マイナー変更のみ**は、表の据え置きと計測日の更新を許容します。据え置く理由を注記します。
- **プロセス分離後**は、Phase 0（プロセス同居・単一 API）の表を履歴として残し、新しい参照構成の表に差し替えます。本 Version 0.5 で実施済みです。
- 未計測のまま公開してかまいません。推測でセルを埋めません。
- **Version 0.6** で Worker 格子を追加しました。1 Worker 固定の v0.5 表は、ウェーブサイズが違うため差し替えず併記します。
- **Version 0.11** で書き込み削減後の格子を差し替えました。v0.6 表は履歴に残します。
