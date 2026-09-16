# 運用: ログ収集例（Fluent Bit / Loki / Grafana）

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0.2 |
| 更新日 | 2026-09-16 |
| 関連 | [logging-operations.md](../specifications/platform/logging-operations.md), [operations-docker.md](operations-docker.md), [logging-property-keys.md](../reference/logging-property-keys.md) |

---

任意の compose overlay で、コンテナ stdout の JSON を Fluent Bit → Loki → Grafana まで辿る例。製品は特定ベンダーを必須にしない。契約の正本は [logging-operations.md](../specifications/platform/logging-operations.md)。

**Version 1.0.2（2026-09-16）**: Grafana Logs は Microsoft `LogLevel` を `info` / `warn` 等へ写して色分けする。

## 起動

既定の `docker compose up -d` では Loki / Grafana / Fluent Bit は起動しない。Quick Start の必須ステップでもない。

```bash
docker compose -f docker-compose.yml -f docker-compose.logging.yml up -d
```

ランタイム分離と同時に使うときは `docker-compose.logging-split.yml` も並べる（Compose は未定義サービスの部分マージができないため）。

```bash
docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml \
  -f docker-compose.logging.yml -f docker-compose.logging-split.yml up -d
```

Grafana は `http://localhost:3001`（Studio の 3000 と分ける）。UI の既定言語は日本語。匿名 Admin は **ローカル例に限る**。テナント利用者に Grafana を渡さない。

| データソース | 用途 |
| --- | --- |
| `statevia-ops` | オペレータ Org。`TenantId` が無い行 |
| `statevia-tenant-default` | Development シード `tenant_key=default` の Loki Org（固定 UUID） |
| `statevia-postgres` | 製品 DB。パスワードハッシュ等の機微列はパネルに出さない |

ダッシュボードは [statevia ops logs](http://localhost:3001/d/statevia-ops-logs)、[statevia default tenant logs](http://localhost:3001/d/statevia-tenant-default-logs)、[statevia postgres](http://localhost:3001/d/statevia-postgres-data)。Explore は `{job="statevia", service="service-api"}`。既定レンジは 6 時間。

戻すとき:

```bash
docker compose -f docker-compose.yml -f docker-compose.logging.yml down
docker compose up -d
```

fluentd ドライバが繋がらないときは `STATEVIA_FLUENTD_ADDRESS=host.docker.internal:24224` を付けて対象コンテナを作り直す。

## ingest 時の Org 写像

Fluent Bit は 1 行 JSON をパースし、次の順で UUID を探す。

1. `State.tenantId` または `State.TenantId`
2. トップレベルの `tenantId` / `TenantId`
3. `Scopes` 配列内の同じキー（`IncludeScopes` 時。Engine 行など）

UUID なら Loki の `X-Scope-OrgID` にその文字列を付ける。欠落・非 JSON は `statevia-ops`。`TenantKey` は OrgID に使わない。ラベルは `service`（compose サービス名）のみ。

## JSON 1 行の例

Microsoft JsonConsole（overlay の `Logging__Console__FormatterName=json`）。`GET /v1/executions` 開始ログの形:

```json
{
  "Timestamp": "2026-09-16T00:00:00.0000000Z",
  "EventId": 4010,
  "LogLevel": "Information",
  "Category": "Statevia.Service.Api.Hosting.RequestLoggingMiddleware",
  "Message": "HTTP request start TraceId=... Method=GET Path=/v1/executions ... TenantId=00000000-0000-4000-8000-000000000001 ...",
  "State": {
    "Message": "HTTP request start ...",
    "traceId": "...",
    "tenantId": "00000000-0000-4000-8000-000000000001",
    "{OriginalFormat}": "HTTP request start TraceId={traceId} ..."
  }
}
```

Engine のプレースホルダは PascalCase（`ExecutionId`）。スコープで足したテナントは例えば `"Scopes":[{"TenantId":"..."}]` になる。

JsonConsole は HTML 安全のため、ネストした JSON の引用符を `\u0022` にすることがある（JSON としては正しい）。overlay の Fluent Bit はパース後のフィールドだけを Loki へ送り、Docker が付けた生の `log` 文字列と `State.{OriginalFormat}` は残さない。Grafana の Logs パネルは `LogLevel` / `Category` / `Message` だけを取り出す。行頭の UNK を避けるため、Microsoft の `Information` などを Grafana が色分けする `info` / `warn` / `error` / `debug` / `trace` / `critical` へ写す（フィルタの値は `Information` のまま）。新規 ingest の JSON には同じ写像の `level` も載る。Loki のインデックスラベルにはしない。

`GET /v1/health` の開始・完了ログは出ない。

## 保持

製品は保持日数を強制しない。本 Guide の推奨は **14 日**。compose 例の Loki は `retention_period: 168h`（7 日）で、開発向けに短い。監査の正本は `event_store`。本番相当で延ばすときは `compose/logging/loki-config.yml` の `limits_config.retention_period` を変える。

## コンテナの役割

| コンテナ | 役割 |
| --- | --- |
| 対象サービス | JSON を stdout へ。Loki URL は知らない |
| Fluent Bit | fluentd driver（24224）から受け、Org を付けて Loki へ push |
| Loki | 保管例。`auth_enabled` で Org ヘッダを見る。本体認証はしない |
| Grafana | オペレータの Explore / ダッシュボード例 |

Studio と PostgreSQL コンテナは収集対象外。Fluent Bit / Loki が止まっても API は 5xx にしない（ドライバのバッファ。溢れは破棄してよい）。
