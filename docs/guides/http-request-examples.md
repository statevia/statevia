# HTTP リクエスト例

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.7 |
| 更新日 | 2026-11-18 |
| 関連 | [getting-started.md](getting-started.md), [../specifications/api-http.md](../specifications/api-http.md) |

---

Service API（`http://localhost:8080`）への代表的な HTTP 呼び出し例。契約の正本は [api-http 仕様](../specifications/api-http.md) です。

**Version 1.7（2026-11-18）**: ログインで `TOKEN` に入れ、以降の例をそのまま続けて実行できる形にした。定義の構造・スキーマ、Action のスキーマ、実行の一覧と件数、待機中の実行の再開とイベント発行、集合配送とイベント購読、定期実行の例を追加。

**Version 1.6（2026-10-30）**: 定義の初回登録に必須の `projectId` と、所属 Project の一覧の例を追加。

**Version 1.5（2026-09-03）**: `POST /v1/definitions/validate` の dry-run 例を追加。

**Version 1.4（2026-09-01）**: `X-Api-Key` を現行として書く。JWT 例は残す。

## 前提

- `X-Tenant-Id: default`（または運用中の `tenant_key`）
- Runtime API には `Authorization: Bearer $TOKEN` または `X-Api-Key` と `X-Tenant-Id`
- ミューテーションには `X-Idempotency-Key` を付与（推奨）

例はリポジトリルートで上から順に実行できます。JSON の組み立てと取り出しに `jq` を使います。定義・実行の運用コアは CLI の `statevia def` / `statevia exec` からも呼べます（手順は [cli-reference.md](cli-reference.md)）。

## ログイン（開発）

Development では API 起動時に開発用の管理者が自動作成される（`tenant_key = default`。値は [getting-started.md](getting-started.md) §3）。本番の管理者は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md) の手順で作る。

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","username":"admin","password":"admin123"}' | jq -r .accessToken)
```

以降の例は `Authorization: Bearer $TOKEN` を付ける。

## ヘルス

```bash
curl -s http://localhost:8080/v1/health
```

## 定義の検証（永続化しない）

`POST /v1/definitions/validate` — `yaml` 必須、`name` 任意。catalog には保存しない。成功は `{ "valid": true, "name": "..." }`。不正は 422。`X-Idempotency-Key` は不要。

```bash
curl -s -X POST "http://localhost:8080/v1/definitions/validate" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    '{yaml:$yaml}')"
```

## 所属 Project の一覧

`GET /v1/projects` — 呼び出し元が所属する Project の配列を返す。各項目の `projectId` を定義の初回登録に使う。所属が無ければ `POST /v1/projects`（`slug` と `displayName`）で作る。作成者が admin になる。

```bash
PROJECT_ID=$(curl -s "http://localhost:8080/v1/projects" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.[] | select(.slug=="default") | .projectId')
```

## 定義の初回登録

`POST /v1/definitions` — `Content-Type: application/json` で `name`、`yaml`、`projectId` を送る。`projectId` は必須で、省略は 422（`field=projectId`）。応答の `displayId` を実行開始時の `definitionId` に使う。

```bash
DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    --arg projectId "$PROJECT_ID" \
    '{name:"my-workflow",yaml:$yaml,projectId:$projectId}')" | jq -r .displayId)
```

## 定義の版追加（既存のみ）

`PUT /v1/definitions/{displayId}` — 既存定義に新版を append する。未定義 ID は **404**。

```bash
curl -s -X PUT "http://localhost:8080/v1/definitions/$DEF_ID" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    '{name:"my-workflow",yaml:$yaml}')"
```

## 定義の取得

```bash
curl -s "http://localhost:8080/v1/definitions/$DEF_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## 定義の構造（グラフ）

`GET /v1/graphs/{graphId}` — 定義の `displayId`（または UUID）で、ノードと遷移の構造（`graphId`、`nodes`、`edges`）を返す。

```bash
curl -s "http://localhost:8080/v1/graphs/$DEF_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## nodes 形式の入力スキーマ

`GET /v1/definitions/schema/nodes` — エディタの補完や検査に使う、nodes 形式の定義の JSON Schema（`schemaVersion`、`nodesVersion`、`schema`）。

```bash
curl -s "http://localhost:8080/v1/definitions/schema/nodes" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## Action のスキーマ

`definitions.read` が要る。一覧（`/v1/actions/schema`）、軽量な索引（`/v1/actions/schema/index`）、1 つの Action の入出力スキーマと UI 用の情報（`/v1/actions/schema/{actionId}`）を返す。

```bash
curl -s "http://localhost:8080/v1/actions/schema" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

curl -s "http://localhost:8080/v1/actions/schema/index" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

curl -s "http://localhost:8080/v1/actions/schema/statevia.action.builtin.execution.noop" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## 実行の開始

応答の `displayId` を以降の例の実行 ID に使う。

```bash
EXEC_ID=$(curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d "{\"definitionId\":\"$DEF_ID\",\"input\":{}}" | jq -r .displayId)
```

## 実行の一覧と件数

`GET /v1/executions` — `status`（`ACTIVE` / `COMPLETED` / `FAILED` / `CANCELED`）、`waiting`、`needsAttention`、`definitionId`、`projectId` などで絞り込み、`limit` / `offset` でページを切る。`GET /v1/executions/counts` は状態ごとの件数を返す。

```bash
curl -s "http://localhost:8080/v1/executions?projectId=$PROJECT_ID&status=ACTIVE&limit=20" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

curl -s "http://localhost:8080/v1/executions/counts?projectId=$PROJECT_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## 実行状態・グラフ

```bash
curl -s "http://localhost:8080/v1/executions/${EXEC_ID}" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

curl -s "http://localhost:8080/v1/executions/${EXEC_ID}/graph" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

curl -s "http://localhost:8080/v1/executions/${EXEC_ID}/waits" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## 待機中の実行を進める

承認か却下を待つサンプル（[ui-wait-multi-event.yaml](../samples/ui-wait-multi-event.yaml)）を登録して開始すると、実行は Wait で止まる。`GET .../waits` で待っているノードと受け付けるイベントを確かめ、正本の `POST .../nodes/{nodeId}/resume` で進める。受け付けると 204 を返す。

実行は非同期に進むので、開始の直後はまだ Wait に届いておらず、`waits` が空のことがある。例では空のあいだ 1 秒ごとに取り直す（最大 30 回）。

```bash
WAIT_DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-wait-multi-event.yaml \
    --arg projectId "$PROJECT_ID" \
    '{name:"wait-workflow",yaml:$yaml,projectId:$projectId}')" | jq -r .displayId)

WAIT_EXEC_ID=$(curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d "{\"definitionId\":\"$WAIT_DEF_ID\",\"input\":{}}" | jq -r .displayId)

# 待っているノード（nodeId）を取る。受け付けるイベントは allowedEvents（ここでは approve と reject）
for attempt in $(seq 1 30); do
  NODE_ID=$(curl -s "http://localhost:8080/v1/executions/$WAIT_EXEC_ID/waits" \
    -H "X-Tenant-Id: default" \
    -H "Authorization: Bearer $TOKEN" | jq -r '.waits[0].nodeId // empty')
  [ -n "$NODE_ID" ] && break
  sleep 1
done

curl -s -X POST "http://localhost:8080/v1/executions/$WAIT_EXEC_ID/nodes/$NODE_ID/resume" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d '{"resumeKey":"approve"}'
```

イベント名だけで送る `POST /v1/executions/{id}/events` は互換の入口で、受け付けるイベントが 1 つだけの待機が 1 つだけあるときに受け付ける。上の例のように 1 つの待機が複数のイベント（`approve` と `reject`）を受け付けるとき、または当てはまる待機が無いときは 422。次の例は、入力の `eligible` を true にして、在庫の移動（`StockTransferred`）だけを待つ分岐を持つ実行を進める。受け付けると 204 を返す。

```bash
STOCK_EXEC_ID=$(curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d "{\"definitionId\":\"$DEF_ID\",\"input\":{\"eligible\":true,\"shared\":{\"orderId\":\"ORD-1001\"}}}" | jq -r .displayId)

# 待機が現れるまで待つ（最大 30 秒）
for attempt in $(seq 1 30); do
  curl -s "http://localhost:8080/v1/executions/$STOCK_EXEC_ID/waits" \
    -H "X-Tenant-Id: default" \
    -H "Authorization: Bearer $TOKEN" | grep -q '"nodeId"' && break
  sleep 1
done

curl -s -X POST "http://localhost:8080/v1/executions/$STOCK_EXEC_ID/events" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d '{"name":"StockTransferred"}'
```

## 集合配送とイベント購読

`POST /v1/events` — `topic`（必須）と `key` に一致する購読（定義の `wait.subscribe`）を持つ実行へまとめて配送する。`executions.write` が要る。一致が 0 件でも 204。`GET /v1/event-subscriptions` は、今のテナントで購読されている `topic` と `key` の組を返す（`executions.read`）。

```bash
curl -s -X POST "http://localhost:8080/v1/events" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"topic":"order.shipped","key":"ORD-1001","payload":{}}'

curl -s "http://localhost:8080/v1/event-subscriptions" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

## キャンセル

```bash
curl -s -X POST "http://localhost:8080/v1/executions/${EXEC_ID}/cancel" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d '{}'
```

## SSE（グラフ更新ストリーム）

```bash
curl -N "http://localhost:8080/v1/executions/${EXEC_ID}/stream" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

UI からは同一オリジンの `/api/core/executions/{id}/stream` 経由でも可。

## 定期実行

定期実行は、指定したサービスアカウント（run-as）が実行を開始する。run-as は、有効で、`executions.write` を持ち、定義の Project に実行者（`executor`）以上で所属している必要がある。満たさないと作成は 422 になる。次の例はテナント管理者で、run-as 用のグループとサービスアカウントを用意してから定期実行を作る。

```bash
# executions.write を持つグループ
GROUP_ID=$(curl -s -X POST "http://localhost:8080/v1/admin/groups" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"schedule-runners"}' | jq -r .groupId)

curl -s -X PUT "http://localhost:8080/v1/admin/groups/$GROUP_ID/permissions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"permissionKeys":["definitions.read","executions.read","executions.write"]}'

# そのグループに属するサービスアカウント（API キーは発行しない）
RUN_AS_ID=$(curl -s -X POST "http://localhost:8080/v1/admin/service-accounts" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"name\":\"nightly-runner\",\"groupIds\":[\"$GROUP_ID\"]}" | jq -r .principalId)

# 定義の Project に実行者として所属させる
curl -s -X PUT "http://localhost:8080/v1/projects/$PROJECT_ID/members/$RUN_AS_ID" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"role":"executor"}'
```

`POST /v1/schedules` — `name`、`definitionId`、`cronExpression`（5 フィールド）、`timeZone`（IANA。省略して UTC にはしない）、`runAsPrincipalId` が必須。`overlapPolicy` は `skip`（既定。前の実行が残っていれば飛ばす）か `allow`。

```bash
SCHEDULE_ID=$(curl -s -X POST "http://localhost:8080/v1/schedules" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"name\":\"nightly-order\",\"definitionId\":\"$DEF_ID\",\"cronExpression\":\"0 9 * * 1-5\",\"timeZone\":\"Asia/Tokyo\",\"runAsPrincipalId\":\"$RUN_AS_ID\",\"input\":{}}" \
  | jq -r .scheduleId)
```

一覧・取得・手動実行・更新・削除:

```bash
# 一覧（input は含まない）と取得（input を含む）
curl -s "http://localhost:8080/v1/schedules" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

curl -s "http://localhost:8080/v1/schedules/$SCHEDULE_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"

# 今すぐ 1 回実行する（201 で実行を返す。次の予定は進まない）
curl -s -X POST "http://localhost:8080/v1/schedules/$SCHEDULE_ID/run" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")"

# 指定した項目だけ更新する（ここでは無効にする）
curl -s -X PATCH "http://localhost:8080/v1/schedules/$SCHEDULE_ID" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"enabled":false}'

# 削除（204。以降は発火しない）
curl -s -X DELETE "http://localhost:8080/v1/schedules/$SCHEDULE_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

発火の規則と権限の詳細は [api-http.md](../specifications/api-http.md) §3.12。

## API キー

スコープ付き API キーはテナント管理者が発行する。Runtime API では JWT の代わりに `X-Api-Key` と `X-Tenant-Id` を付ける。

```bash
curl -s "http://localhost:8080/v1/definitions/$DEF_ID" \
  -H "X-Tenant-Id: default" \
  -H "X-Api-Key: <plain-key>"
```

発行手順は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md) と [api-http.md](../specifications/api-http.md) §4.1.3。

## 次に読むもの

- 初回セットアップ: [getting-started.md](getting-started.md)
- OpenAPI / Scalar: [../reference/api-openapi.md](../reference/api-openapi.md)
