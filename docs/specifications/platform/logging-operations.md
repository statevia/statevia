# 運用ログ（JSON stdout）

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.0.1 |
| 更新日 | 2026-09-17 |
| 関連 | [io-log-masking.md](io-log-masking.md), [logging-property-keys.md](../../reference/logging-property-keys.md), [operations-logging.md](../../guides/operations-logging.md) |

---

## Normative 要約

- **必須**: logging overlay 配下の Service API / Worker / Scheduler / Action Host は、コンソールを **1 イベント 1 JSON** で出す。
- **必須**: 実行系では可能な範囲で `TraceId`。テナント解決後は `TenantId`（`tenants.tenant_id` の UUID）。実行が分かる行は `ExecutionId`。欠けているキーを埋めてはならない。
- **必須**: ログ出力前に機微 IO のマスキングを通す（[io-log-masking.md](io-log-masking.md)）。
- **必須**: 既定の `docker compose up -d` ではログ基盤を起動しない。アプリログを PostgreSQL に入れない。Studio にアプリログ探索 UI を置かない。
- **必須**: Error は運用者が対応すべき想定外。協調 Cancel による実行終端は Information（エンジンは Cancel 理由を区別しない）。`Fact=Failed` の実行終端は Error。
- **禁止**: 特定ベンダー（Loki / Grafana / CloudWatch / Datadog 等）を製品必須としない。製品は Loki 上の ACL を強制しない。
- **禁止**: ホスト / プロセスの資源指標と Action 単位のテナント向け指標を本契約の対象にしない。
- **推奨**: 保管キーへの写像（例: JSON の `TenantId` UUID → ingest 時のテナント識別子）は運用者が行う。
- **任意**: 収集・保管・可視化の実装。リポジトリの compose overlay は一例である。

**Version 1.0.1（2026-09-17）**: 重大度の原則（協調 Cancel は Information、Failed 終端は Error）。

**Version 1.0（2026-09-16）**: JSON stdout 契約と ingest 時写像。ベンダー非必須。

## 出力契約

ホストの `dotnet run`（Development）は Simple コンソールのままでよい。logging overlay を付けたコンテナだけ JSON にする。

1 行の JSON から、少なくとも次を読めること。

| フィールド | 条件 |
| --- | --- |
| 時刻 | ランタイムが出す Timestamp |
| 重大度 | `LogLevel` または同等 |
| Category / EventId / Message | 常に |
| `TraceId` | HTTP および実行系で可能な範囲 |
| `TenantId` | テナント解決後。未解決パスでは欠落 |
| `ExecutionId` | 実行が分かるログのみ |

キー命名は [logging-property-keys.md](../../reference/logging-property-keys.md)。実装は `[LoggerMessage]` 経由とし、`ILogger.Log*` を直接増やさない。

重大度は次とする。Error は運用者が対応すべき想定外。協調 Cancel による実行終端は Information とする（エンジンはユーザー操作とサービス起因を区別しない）。`Fact=Failed` の実行終端は Error のままとする。

Microsoft JsonConsole ではプレースホルダが `State.tenantId`（API / Worker / Application）または `State.TenantId` / `State.ExecutionId`（Engine）に載る。スコープで足した `TenantId` は `Scopes` 配列に載ることがある。収集側は両方を見てよい。

## テナントと未解決パス

テナントが手元にある実行行・work item 行には内部 UUID を載せる。Engine ライブラリにテナント概念を足さない。ホストが既にテナントを持つときだけログスコープで足してよい。無いなら欠ける。

次は `TenantId` を無理に載せない。収集例ではオペレータ用の固定保管キーへ送る。

- `/v1/auth/login`
- `/v1/health`
- `/swagger` / `/scalar`
- 起動・モジュール読み込みなどプロセスログ

ログイン失敗の `TenantKey` / `Username` はフィールドとして維持する。保管テナント識別子には使わない。

## 収集と保管

製品はログ基盤クライアントをアプリケーションに埋め込まない。stdout を運用者の基盤へ載せる。

ingest 時に JSON の `TenantId` が UUID なら、その文字列を保管側のテナント識別子にしてよい。欠落・非 JSON はオペレータ用の単一キーへ送るか、パース失敗を捨てる（捨てる場合は運用手順に書く）。行をテナント保管へ誤って混ぜない。

テナント保管を分けない運用（オペレータ専用の単一基盤）も許可する。製品は保管基盤上の ACL を強制しない。監査の正本は `event_store` のまま。保持日数は製品 SLA にしない。

OTLP・メトリクス・トレースは本仕様の対象外。

## 健康プローブ

`GET /v1/health` では RequestLogging の開始・完了を出さない。エンドポイント自体の死活は現行のまま。health 以外の HTTP Information は間引かない。

## 対象外（資源・Action 指標）

ホスト CPU・メモリ、プロセス資源、HPA 用キュー長はテナント契約として返さない。1 プロセスが複数テナントを扱うため、資源量を `TenantId` で割ると不正になる。オンプレ / SaaS 運用者向けの資源確認は後続の可観測性契約とする。Action の成功率・経過時間などテナント向け指標も本仕様の対象外。

## 関連

- [io-log-masking.md](io-log-masking.md)
- [operations-logging.md](../../guides/operations-logging.md)
- [operations-docker.md](../../guides/operations-docker.md)
