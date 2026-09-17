# サービス容量（暫定指針）

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 0.9 |
| 更新日 | 2026-09-18 |
| 関連 | [capacity-load-testing.md](../guides/capacity-load-testing.md), [environment-variables.md](environment-variables.md), [data-integration.md](../specifications/data-integration.md), [wait-cancel.md](../specifications/execution/wait-cancel.md) |

---

**Version 0.9（2026-09-18）**: 空 Claim EXISTS skip 後に L1 1×16・512 件を再計測。完了レートは約 47 exec/s。アイドル 30 秒で `execution_work_items` の UPDATE は 0。

**Version 0.8（2026-09-18）**: 未完了 work item が無い Claim は EXISTS のみ（空 UPDATE なし）。poll は 1 秒のまま。

**Version 0.7（2026-09-17）**: L1 1 Worker × スロット 16・512 件を書き込み削減後に再計測。checkpoint UPDATE は 0、完了レートは約 40 exec/s。v0.6 格子の他セルは未再測。

**Version 0.6（2026-09-10）**: Worker プロセス × スロットの縮小格子と C1（一斉 Cancel）を追加。v0.5 の 1 Worker・`MaxConcurrency` 4 表は残す。

**Version 0.5（2026-09-10）**: 参照構成を `split-runtime` に差し替え再計測。Phase 0 数値は履歴。

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
| トポロジ | `split-runtime`（`docker-compose.split-runtime.yml`。Worker / Scheduler は別プロセス。Worker `MaxConcurrency` は compose で **4**） |
| p95 ベースライン倍率 N | **3**（L1 ベースラインはウォームアップ後 count=8 の Start 受理 p95） |

`ref-cloud-small` は未計測です。

## 参照構成（split-runtime）

| コンポーネント | 台数 | 備考 |
| --- | --- | --- |
| PostgreSQL 16 | 1 | 既定 compose |
| Service API | 1 | プロセス内 HostedService は Off。UI は計測対象外 |
| Worker | 1 | `execution_work_items`。compose の `MaxConcurrency` は 4 |
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
| L2 Wait 滞留 | Resume 受理 rate | 約 6 req/s | 同上 |
| L2 Wait 滞留 | Resume 受理 p50 / p95 | 58 / 473 ms | 16 件ステップ。8 件ベースラインの Resume p95 は 528 ms（N=3 未満） |
| L3 DelayWait | 期限〜Resume 遅延 | 未計測 | HTTP YAML では DelayWait を定義できない（`wait.timeout` 未使用、投影の wait_kind は常に EventWait）。掃引 poll **5s** は設定下限であり TimerFire の実測ではない |
| D1 再起動耐久 | 合否 | 合格 | 8 件。`docker compose restart service-api` のみ（Worker / Scheduler は存続）。waits 8・Resume 8/8・投影エラー 0。再起動直後 Resume p95 は約 872 ms |

## Worker プロセス × スロット格子（v0.6）

v0.5 表は 1 Worker・`MaxConcurrency` 4・L1 128 件です。利用者がプロセス数とスロットを決める材料として、同じ `ref-dev` / `split-runtime` で縮小格子を測りました。L2 は入れていません（32 件で GET `/waits` 待ちがタイムアウトするため）。

| 項目 | 値 |
| --- | --- |
| 計測日（UTC） | 2026-09-09〜10 |
| git | `fdb57c2` |
| L1 | count 512、HTTP concurrency 16、制限 300 秒 |
| C1 | count 128、settle 3 秒、HTTP concurrency 16。WAITING snapshot は待たない |
| Worker | `--scale worker=N` と `STATEVIA_WORKER_MAX_CONCURRENCY` / `STATEVIA_WORKER_CANCEL_CONCURRENCY` |

L1 本体は `CancelConcurrency` 1。値は完了レート（exec/s）と Start 受理 p95（ms）。512 件すべて `Completed` にならなかったセルはタイムアウトです。

| プロセス \ スロット | 8 | 16 | 32 | 64 |
| --- | --- | --- | --- | --- |
| 1 | 約 22 / 169 | 約 30 / 150 | 約 29 / 150 | タイムアウト |
| 2 | 約 32 / 185 | 約 31 / 198 | タイムアウト | タイムアウト |
| 3 | 約 30 / 270 | 約 31 / 323 | タイムアウト | タイムアウト |
| 4 | 約 30 / 415 | 不安定 | タイムアウト | タイムアウト |

読み方:

- 天井は **約 30〜32 exec/s**（1 プロセス × 16 スロット付近で到達。2 プロセス × 8 が最高）
- それ以上のプロセスやスロットは完了レートを伸ばさず、Start p95 だけ悪化する
- スロット 64 は 1 プロセスでも 300 秒以内に 512 件が揃わない（再測でも同じ）。残留 Running は数件
- 3 プロセス × 16 は初回タイムアウト、drain 後の再測で約 31 / 323
- 4 プロセス × 16 は初回約 8 exec/s、再測はタイムアウト。頭打ち境界として不安定

C1 角はすべて Cancel 受理 128 / `Cancelled` 128、HTTP 5xx なし。`cancelledRate` は Start + settle 3 秒 + Cancel 待ちを含む壁時計なので、純 Cancel TPS ではありません。下表は Cancel 受理 p95（ms）と、その壁時計レート（1/s）。

| プロセス × スロット | ループ 1 | ループ 2 | ループ 4 | ループ 8 |
| --- | --- | --- | --- | --- |
| 1 × 8 | 145 / 11 | 54 / 13 | 57 / 15 | 47 / 7.5 |
| 1 × 64 | 103 / 11 | 133 / 12 | 70 / 14 | 48 / 15 |
| 4 × 8 | 161 / 12 | 78 / 12 | 121 / 12 | 167 / 9 |
| 4 × 64 | 137 / 3.0 | 138 / 2.5 | 71 / 2.0 | 104 / 2.1 |

読み方:

- 1 プロセスでは Cancel ループ 2〜4 で Cancel HTTP p95 が下がる
- ループ 8 は 1 × 8 で波全体のレートが落ちる（Start スロットと Cancel ループの取り合い）
- 4 プロセスにしても Cancel レートは伸びない。4 × 64 は波全体が約 2〜3 /s まで落ち、L1 と同じ大域ボトルネックが見える

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

アイドルでは Worker が 1 秒ごとに EXISTS を読むだけです。空の Claim UPDATE は開きません。L1 本体の work item UPDATE 512 は実 claim 分です。

## 履歴: Phase 0（プロセス同居）

Version 0.4 までの参照構成は `phase0-single-api`（既定 compose、Worker / Scheduler は API プロセス内、`MaxConcurrency` 既定 1）でした。2026-09-08〜09 の `ref-dev` 要約:

| シナリオ | メトリクス | 当時の値 |
| --- | --- | --- |
| L1 | Start / 完了 rate、p50 / p95 | 約 13 req/s、66 / 103 ms（128 件） |
| L2 | 同時滞留、Resume rate、p50 / p95 | 32、約 10 req/s、66 / 289 ms |
| D1 | 合否 | 合格（8 件。API 再起動は in-process Worker も含む） |

## スケールの読み方

- **受理 ≠ 完了。** `POST /v1/executions` の 201 はキュー投入です。完了は Worker の claim と投影更新の後です。
- **Wait / Resume。** 滞留中の実行は Running のまま waits を持ちます。再開の正本は `POST /v1/executions/{id}/nodes/{nodeId}/resume` です。GET `/waits` はグラフ snapshot です。WAITING が揃わないと L2 ウェーブはタイムアウトします。
- **DelayWait。** 期限検知の poll（既定 5s）が下限になります。現行の HTTP Definition では DelayWait を定義できないため、期限〜Resume の実測セルは未計測です。
- **投影。** キューはグローバル直列です。Worker を並列にしても投影が遅延し得ます。契約は [data-integration.md](../specifications/data-integration.md)、設定キーは [environment-variables.md](environment-variables.md) の `ExecutionProjectionQueue:*` です。
- **耐久 D1。** 再起動後に Resume でき、投影が壊れないことが合否です。件数の宣伝には使いません。
- **Worker 格子。** v0.6 ではプロセスやスロットを増やしても完了レートは約 30 exec/s で頭打ちでした。書き込み削減後の 1×16 単セルは約 40 exec/s です（格子の全面再測は未）。Start p95 はプロセスを増やすと悪化します。スロット 64 は 1 プロセスでも 512 件が 300 秒以内に揃いません。投影キュー（グローバル直列）または PostgreSQL が先に飽和し得ます。

上限の解釈に使う実装ボトルネック（v0.6 格子時点。短寿命の checkpoint / snapshot 回数は v0.7 で削減済み）:

| 要因 | 現行の目安 | 設定 / 場所 |
| --- | --- | --- |
| Start / Resume のプロセス内同時件数 | 専用 Worker の compose 未設定時は 4（API 内既定は 1。範囲 1〜64）。格子では 16 付近で完了レートが頭打ち | `Statevia:Runtime:Worker:MaxConcurrency` |
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
