# 負荷・耐久計測

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 0.6 |
| 更新日 | 2026-09-10 |
| 関連 | [service-capacity.md](../reference/service-capacity.md), [operations-docker.md](operations-docker.md), [http-request-examples.md](http-request-examples.md) |

---

**Version 0.6（2026-09-10）**: C1（一斉 Cancel）と Worker 格子手順を追加。数表は Reference。

**Version 0.5（2026-09-10）**: 参照構成を split-runtime に差し替え。Phase 0 は履歴。D1 は API のみ再起動。

**Version 0.4（2026-09-09）**: D1 再起動手順と L3 プローブ（DelayWait は HTTP 未定義）を追加。

**Version 0.3（2026-09-09）**: 終端済みの遅い Resume は 204。L2 の失敗は非 2xx のみ。

**Version 0.2（2026-09-09）**: p95 倍率 N=3 を初回計測で確定。L3 / D1 は定義のみ。

同じ手順で限界点を測り、[サービス容量（暫定指針）](../reference/service-capacity.md) を更新するためのランブックです。数値の正本は Reference です。本 Guide に件数を複製しません。

既存の単体・統合テストは公開件数の根拠にしません。容量ハーネスは **CI の既定パイプラインに載せません**（手動、または任意の夜間実行）。

## 前提

- `docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml` で PostgreSQL・Service API・Worker・Scheduler が動いていること（[operations-docker.md](operations-docker.md)）
- Runtime API を叩ける Principal（Development なら `POST /v1/auth/login`。例は [http-request-examples.md](http-request-examples.md)）
- 検証用テナントだけを使うこと。本番秘密をリポジトリやハーネス引数の履歴に残さないこと

UI は計測対象外です。

## 参照構成（split-runtime）

| 項目 | 内容 |
| --- | --- |
| トポロジ ID | `split-runtime` |
| PostgreSQL | 1 |
| Service API | 1（プロセス内 HostedService は Off） |
| Worker | 1（compose の `MaxConcurrency` は 4） |
| Scheduler | 1（DelayWait / Ownership Recovery） |
| Action Host | L1 / L2 の builtin `noop` では不要 |
| 対象外 | Studio UI、複数 API レプリカ |

起動:

```bash
docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml up -d postgres service-api action-host scheduler worker
```

ヘルス確認:

```bash
curl -s http://localhost:8080/v1/health
```

実効 CPU / メモリ、日時、git SHA、compose の前提を計測ごとに記録します。Compose に `cpus` / `mem_limit` が無いため、ホスト側の観測値を `hwNotes` に書きます。

Version 0.4 までは `phase0-single-api`（既定 compose・プロセス同居）が参照構成でした。数値の履歴は [service-capacity.md](../reference/service-capacity.md) にあります。

## シナリオ

| ID | 目的 | ワークロード | 主要メトリクス | ハーネス |
| --- | --- | --- | --- | --- |
| L1 | 短寿命スループット | 即完了 Definition を並列 Start | Start 受理 / 完了 rate、p50/p95、HTTP エラー | 実装済み |
| L2 | Wait 滞留スケール | Start → EventWait → ノード Resume | 滞留 waits、Resume rate、p50/p95 | 実装済み |
| L3 | Timer | `wait.timeout` 付き Wait を Start し、HTTP Resume 無しで完了するか見る | 自動完了の有無、滞留、Cancel した leftover | 実装済み（プローブ） |
| D1 | プロセス耐久 | L2 の途中で API 再起動 | Resume 可否、投影の非破壊（合否） | 実装済み |
| C1 | Cancel スループット | Wait 定義を Start し settle 後に一斉 Cancel | Cancel 受理 / Cancelled rate、p50/p95 | 実装済み |

L1 / L2 / D1 の Definition はハーネスに同梱します（`statevia.action.builtin.execution.noop` と単一イベント Wait）。L3 は同じ Wait に `timeout: PT2S` を付けたプローブです。HTTP 契約は [api-http.md](../specifications/api-http.md) です。Wait 再開の正本はノード Resume です。

## ハーネス（L1 / L2 / L3 / D1 / C1）

リポジトリの `tools/capacity/` です。詳細な引数は同ディレクトリの README を見てください。

```bash
dotnet test tools/capacity/Statevia.Tools.Capacity.sln

# Development の例。パスワードは環境変数へ
export STATEVIA_CAPACITY_PASSWORD='admin123'
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- \
  --scenario L1 \
  --base-url http://localhost:8080 \
  --tenant default \
  --username admin \
  --count 8 \
  --concurrency 2 \
  --hw-label ref-dev \
  --hw-notes "laptop, 実効 vCPU/メモリを記入" \
  --topology split-runtime \
  --output tools/capacity/results/l1.json
```

PowerShell の例:

```powershell
dotnet test tools/capacity/Statevia.Tools.Capacity.sln

# Development の例。パスワードは環境変数へ
$env:STATEVIA_CAPACITY_PASSWORD = 'admin123'
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- `
  --scenario L1 `
  --base-url http://localhost:8080 `
  --tenant default `
  --username admin `
  --count 8 `
  --concurrency 2 `
  --hw-label ref-dev `
  --hw-notes "laptop, 実効 vCPU/メモリを記入" `
  --topology split-runtime `
  --output tools/capacity/results/l1.json
```

L2 は `--scenario L2`、L3 は `--scenario L3`、D1 は `--scenario D1`、C1 は `--scenario C1` です。出力は JSON と、同名の CSV です。トークン・パスワードはファイルに書きません。

C1 は L2 と同じ Wait 定義を Start し、`--settle-ms`（既定 3000）のあと受理済み実行を Cancel します。GET `/waits` の WAITING が揃うのは待ちません（32 件ウェーブで snapshot 待ちがタイムアウトするため）。すでに `Completed` の行は Cancel しません。

L3 は DelayWait を HTTP から定義できるかを確認します。現行の Definition では `wait.timeout` は未使用で、投影の wait_kind は EventWait です。期限到達による TimerFire は起きません。観測窓のあと、残った実行は Cancel します。クライアント側タイマーで EventWait を擬似完了させないでください（L3 のラベルを誤ります）。

D1 は git ルートで `docker compose restart service-api` します（`--restart-service` でサービス名を変えられます。既定タイムアウト 300 秒）。split-runtime では Worker / Scheduler は再起動しません。再起動後に `/v1/health` と再ログインし、GET execution / graph / waits が壊れず Resume で `Completed` になれば合格です。compose を起こすときは `-f docker-compose.yml -f docker-compose.split-runtime.yml` を使ってください。`restart` 自体は既存コンテナの環境を保ちます。

JWT または `X-Api-Key` を使う場合は `--token` / `--api-key` を渡します。パスワードをコマンド履歴に残さないでください。

## 負荷ランプ

1. 参照構成を起動し、メタデータ（HW、git、日時、トポロジ）を記録する。
2. 低い `--count` / `--concurrency` で 1 回走らせ、ベースラインの p50/p95 を取る。
3. 件数または同時 Start を段階的に上げる。
4. 次のいずれかで止める。直前ステップを暫定上限とする。
   - HTTP 5xx が 0.1% 以上
   - Resume 失敗（L2）
   - 実行が `Failed` / タイムアウト（L2 の WAITING 待ちタイムアウトを含む）
   - 投影のブロック警告（運用ログ）
5. p95 がベースラインの **N=3** 倍を超えたら上限候補とする。
6. 要約を [service-capacity.md](../reference/service-capacity.md) に反映する。生データは公開しない。

L2 は Resume の非 2xx を失敗と数えます。失敗が出た直前ステップ（失敗ゼロの最高ウェーブ）を上限にします。投影がすでに終端の遅い Resume は API が **204** を返すため、失敗に含めません。ハーネスは GET 時点で `Completed` の実行へは Resume を送らず、`resumeSkippedCompletedCount` に分けます。GET `/waits` はグラフ snapshot 由来です。RUNNING のまま WAITING が現れない実行が残るとウェーブ全体がタイムアウトします。

閾値超過は暫定上限の確定であり、製品障害チケットの必須条件ではありません。

## Worker プロセス × スロット格子

単一 Worker・固定 `MaxConcurrency` だけでは、利用者が台数やスロットを決める材料になりません。縮小格子は次です。

- **L1 本体（16 セル）**: プロセス 1/2/3/4 × スロット 8/16/32/64。`CancelConcurrency` は 1。同じ `--count`（例: 512）で完了レートと Start p95 を比較する
- **C1 角（16 セル）**: `(プロセス, スロット)` が `(1,8)` / `(1,64)` / `(4,8)` / `(4,64)` × Cancel ループ 1/2/4/8。WAITING snapshot は待たない
- L2 は格子に入れない（32 件で GET `/waits` 待ちがタイムアウトする）
- 投影キューや PostgreSQL の頭打ちは、スロットを増やしても完了レートが伸びない地点として読む

`worker` は `container_name` が無いので `--scale worker=N` できます。スロットと Cancel ループは `STATEVIA_WORKER_MAX_CONCURRENCY` / `STATEVIA_WORKER_CANCEL_CONCURRENCY` です（[operations-docker.md](operations-docker.md)）。一括実行は `tools/capacity/run-worker-grid.ps1` です。`ui-studio` は起動しません。セル間で Running の leftover を Cancel します。

数表の正本は [service-capacity.md](../reference/service-capacity.md) です。

## 必須メタデータ

ハーネス出力に次を含めます。

| 項目 | 意味 |
| --- | --- |
| `scenarioId` | `L1` / `L2` / `L3` / `D1` / `C1` |
| `measuredAtUtc` | 計測開始（UTC） |
| `gitSha` | 対象コードの SHA |
| `hwLabel` | `ref-dev` または `ref-cloud-small` |
| `hwNotes` | 実効 vCPU / メモリなど |
| `topology` | 現行は `split-runtime`。履歴は `phase0-single-api` |
| `metrics` | rate、p50/p95、エラー件数など |
| `provisionalCap` | ランプで確定した上限（任意） |
| `notes` | 特記 |

## 合格閾値の見方

初版の例です。製品保証ではありません。

- HTTP 5xx &lt; 0.1%
- Resume 失敗なし（L2。非 2xx を失敗と数える）
- 投影ブロック警告なし
- p95 ≤ ベースライン × **3**

D1 はスループットではなく合否です。再起動後に Resume できない、または投影が壊れる場合は件数を宣伝せず、製品不具合として扱います。
