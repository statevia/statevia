# 最初のワークフローを書く（チュートリアル）

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-11-18 |
| 関連 | [../concepts/definition.md](../concepts/definition.md), [../specifications/definition.md](../specifications/definition.md), [http-request-examples.md](http-request-examples.md), [../samples/](../samples/) |

---

[docs/samples/](../samples/) のサンプル定義を登録して動かしながら、定義の書き方を 3 段階で学ぶ。

1. **Wait**: 人の判断（イベント）を待って、受け取ったイベントで行き先を変える
2. **Fork / Join**: 処理を並列に進め、すべての枝が終わってから合流する
3. **Action**: Action に入力を渡し、出力の値で道を分ける

各段で、定義のどこが効いているかを抜粋で示し、HTTP で登録・開始・確認する。同じ操作は Studio の画面（[ui-user-guide.md](ui-user-guide.md)）からもできる。

## 前提

- [getting-started.md](getting-started.md) の手順で、Service API と UI が起動していること
- コマンドはリポジトリルートで上から順に実行する。JSON の組み立てと取り出しに `jq` を使う

## 準備: ログインと Project

開発用の管理者でログインし、定義を置く Project を用意する（Project が無ければ作る）。

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","username":"admin","password":"admin123"}' | jq -r .accessToken)

PROJECT_ID=$(curl -s "http://localhost:8080/v1/projects" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.[] | select(.slug=="default") | .projectId')

[ -n "$PROJECT_ID" ] || PROJECT_ID=$(curl -s -X POST "http://localhost:8080/v1/projects" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"slug":"default","displayName":"Default"}' | jq -r .projectId)
```

## 定義の骨組み

サンプルはどれも nodes 形式で書いてある。ルートに `version`・`workflow`・`nodes` を置き、`nodes` に状態を並べる。各ノードは `name`（ASCII 識別子）と `type` を持ち、`next` などで次のノードの名前を指す。

| type | 役割 | 主なプロパティ |
| --- | --- | --- |
| `start` | 開始。1 つだけ | `next` |
| `end` | 終端 | — |
| `action` | Action を実行する | `action`、`input`、`next` または `edges`、`error` |
| `wait` | イベントを待つ | `events`（イベント名 → 次のノード） |
| `fork` | 並列に分ける | `branches`（枝の先頭のノード名） |
| `join` | 枝の合流を待つ | `mode: all`、`next` |

すべてのプロパティと検証の決まりは [definition.md](../specifications/definition.md) §2 を参照する。

## 1. Wait — 人の判断を待つ

[ui-wait-multi-event.yaml](../samples/ui-wait-multi-event.yaml) は、承認か却下を待って行き先を変える。効いているのは `wait` ノードの `events` である。キーが受け付けるイベント名、値がそのイベントを受けたときの次のノードになる。

```yaml
  - name: flow.approve.wait
    type: wait
    label: 承認待ち
    events:
      approve: flow.approved
      reject: flow.rejected
```

定義を登録して実行を開始する。応答の `displayId` を、定義は `definitionId`、実行は実行 ID として使う。

```bash
WAIT_DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-wait-multi-event.yaml \
    --arg projectId "$PROJECT_ID" \
    '{name:"tutorial-wait",yaml:$yaml,projectId:$projectId}')" | jq -r .displayId)

WAIT_EXEC_ID=$(curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d "{\"definitionId\":\"$WAIT_DEF_ID\",\"input\":{}}" | jq -r .displayId)
```

実行は非同期に進み、すぐに `flow.approve.wait` で止まる。待っているノードを取ると、`allowedEvents` に `approve` と `reject` が出る（開始の直後はまだ空のことがあるので、空のあいだは取り直す）。

```bash
for attempt in $(seq 1 30); do
  NODE_ID=$(curl -s "http://localhost:8080/v1/executions/$WAIT_EXEC_ID/waits" \
    -H "X-Tenant-Id: default" \
    -H "Authorization: Bearer $TOKEN" | jq -r '.waits[0].nodeId // empty')
  [ -n "$NODE_ID" ] && break
  sleep 1
done

curl -s "http://localhost:8080/v1/executions/$WAIT_EXEC_ID/waits" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN"
```

`approve` を送って進める。`resumeKey` が `events` のキーに当たる。

```bash
curl -s -X POST "http://localhost:8080/v1/executions/$WAIT_EXEC_ID/nodes/$NODE_ID/resume" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d '{"resumeKey":"approve"}'
```

実行は `COMPLETED` になる。実行グラフの `nodes` を見ると、`flow.approved` を通り、`flow.rejected` は通っていない。

```bash
curl -s "http://localhost:8080/v1/executions/$WAIT_EXEC_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r .status

curl -s "http://localhost:8080/v1/executions/$WAIT_EXEC_ID/graph" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.nodes[].nodeName'
```

新しく開始した実行に `reject` を送ると、今度は `flow.rejected` を通る。`events` に無いイベント名は 422 で受け付けられない。

## 2. Fork / Join — 並列に進めて合流する

[ui-customer-order-parallel.yaml](../samples/ui-customer-order-parallel.yaml) は、受注を検証したあと、出荷を 2 つの枝で並列に進め、両方が終わってから顧客へ通知する。

```yaml
  - name: order.split
    type: fork
    branches:
      - order.ship.east
      - order.await.transfer

  - name: order.ship.east
    type: action
    action: statevia.action.builtin.execution.sleep
    input:
      duration: 5s
    next: order.merge

  - name: order.await.transfer
    type: wait
    event: StockTransferred
    next: order.merge

  - name: order.merge
    type: join
    mode: all
    next: order.notify.customer
```

- `fork` の `branches` に並べた 2 つのノードが、それぞれ枝の先頭になる。
- 片方の枝は `sleep` の Action で 5 秒かかり、もう片方は `StockTransferred` を待つ。`event` と `next` の組は、イベントが 1 つだけの `events` と同じ意味である。
- `join` の `mode: all` は、すべての枝が `order.merge` に着くまで待つ。片方だけが終わっても先へは進まない。

この定義は入力の `eligible` が true のときだけ並列の出荷へ進む（理由は次の段で見る）。登録して、`eligible` を true にして開始する。

```bash
ORDER_DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    --arg projectId "$PROJECT_ID" \
    '{name:"tutorial-order",yaml:$yaml,projectId:$projectId}')" | jq -r .displayId)

ORDER_EXEC_ID=$(curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d "{\"definitionId\":\"$ORDER_DEF_ID\",\"input\":{\"eligible\":true,\"shared\":{\"orderId\":\"ORD-1001\"}}}" | jq -r .displayId)
```

`order.await.transfer` の待機が現れたら、`StockTransferred` を送る。`sleep` の枝がまだ終わっていなくても、`join` が両方の到着を待つ。

```bash
for attempt in $(seq 1 30); do
  TRANSFER_NODE_ID=$(curl -s "http://localhost:8080/v1/executions/$ORDER_EXEC_ID/waits" \
    -H "X-Tenant-Id: default" \
    -H "Authorization: Bearer $TOKEN" | jq -r '.waits[0].nodeId // empty')
  [ -n "$TRANSFER_NODE_ID" ] && break
  sleep 1
done

curl -s -X POST "http://localhost:8080/v1/executions/$ORDER_EXEC_ID/nodes/$TRANSFER_NODE_ID/resume" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d '{"resumeKey":"StockTransferred"}'
```

`sleep` の 5 秒が過ぎて両方の枝が合流すると、`order.notify.customer` を通って `COMPLETED` になる。実行グラフには、2 つの枝のノードと `order.merge` が並ぶ。

```bash
for attempt in $(seq 1 30); do
  STATUS=$(curl -s "http://localhost:8080/v1/executions/$ORDER_EXEC_ID" \
    -H "X-Tenant-Id: default" \
    -H "Authorization: Bearer $TOKEN" | jq -r .status)
  [ "$STATUS" != "ACTIVE" ] && break
  sleep 1
done
echo "$STATUS"

curl -s "http://localhost:8080/v1/executions/$ORDER_EXEC_ID/graph" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.nodes[].nodeName'
```

並列の枝は内部では子の実行として動くが、HTTP と画面では 1 つの実行として見える。枝どうしの値の受け渡しと合流後の扱いは [fork-join.md](../specifications/execution/fork-join.md) を参照する。

## 3. Action — 入力と条件で道を分ける

同じ定義の前半は、Action の入力と出力で道を分ける。

```yaml
  - name: order.preflight
    type: action
    action: statevia.action.builtin.execution.noop
    edges:
      - to: order.validate
        when:
          path: $.states['order.preflight'].output.eligible
          op: eq
          value: true
        order: 10
      - to: order.reject.notify

  - name: order.reject.notify
    type: action
    action: statevia.action.builtin.execution.noop
    input:
      reason: validation_failed
      stage: pre_fulfillment
    next: order.end
```

- `action` には Action の ID を、`statevia.action.builtin.execution.noop` のように省略しない形で書く。`noop` は受け取った入力をそのまま出力し、`sleep` は `input.duration` のあいだ待つ（Builtin の一覧は [definition.md](../specifications/definition.md) §1.1）。
- `order.preflight` は `input` を書いていないので、開始時の入力がそのまま渡り、`noop` がそれを出力する。
- `edges` は条件つきの遷移で、`when` のある行を `order` の小さい順（`order` の無い行はその後に書いた順）に確かめ、最初に合ったものへ進む。どれも合わなければ、`when` の無い行（ちょうど 1 つ置く）へ進む。ここでは、受付ゲートの出力の `eligible` が true なら `order.validate` へ、そうでなければ `order.reject.notify` へ進む。`$.states['名前'].output` は、完了したノードの出力を指す。
- `order.reject.notify` の `input` は、キーと値のリテラルの組を Action に渡す。値に `$.input.…` のようなパスを書くと、実行中の値を渡せる（[definition.md](../specifications/definition.md) §1.6）。
- `order.validate` の `error` は、Action が失敗したときの行き先である。

`eligible` を付けずに開始すると、受付ゲートの既定の行き先へ進み、並列の出荷も待機も通らずにすぐ `COMPLETED` になる。

```bash
REJECT_EXEC_ID=$(curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo "demo-$RANDOM$RANDOM")" \
  -d "{\"definitionId\":\"$ORDER_DEF_ID\",\"input\":{}}" | jq -r .displayId)

for attempt in $(seq 1 30); do
  STATUS=$(curl -s "http://localhost:8080/v1/executions/$REJECT_EXEC_ID" \
    -H "X-Tenant-Id: default" \
    -H "Authorization: Bearer $TOKEN" | jq -r .status)
  [ "$STATUS" != "ACTIVE" ] && break
  sleep 1
done
echo "$STATUS"

curl -s "http://localhost:8080/v1/executions/$REJECT_EXEC_ID/graph" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.nodes[].nodeName'
```

実行グラフには `order.start`・`order.preflight`・`order.reject.notify`・`order.end` だけが並ぶ。ノードの `input` と `output` を見ると、`order.reject.notify` に `reason` と `stage` が渡っている。

## Studio で見る

ここまでの実行は Studio でも確かめられる。

- 定義一覧（`/definitions`）に `tutorial-wait` と `tutorial-order` が出る。定義の詳細から編集画面を開くと、YAML とグラフを切り替えて構造を見られる。
- 実行一覧（`/executions`）から実行詳細（`/executions/{executionId}`）を開き、リストとグラフを切り替えて、通ったノードと待機を見る。待機中の実行は、実行詳細の「イベントを送信」から進められる。

## 次に読むもの

- 定義の考え方: [concepts/definition.md](../concepts/definition.md)
- 定義の全項目と検証の決まり: [specifications/definition.md](../specifications/definition.md)
- 待機とキャンセルの契約: [specifications/execution/wait-cancel.md](../specifications/execution/wait-cancel.md)
- そのほかの HTTP の例（定期実行・集合配送など）: [http-request-examples.md](http-request-examples.md)
