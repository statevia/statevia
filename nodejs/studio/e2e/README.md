# E2E テスト (Playwright)

**Fixture + Page Object** で構成しています。

- **Page Object**: 実行詳細（`/executions/[executionId]`）のロケーターと操作は `pages/ExecutionDetailPage.ts` に集める。文言は i18n 辞書（日本語）から引く
- **Fixture**: `fixtures/test.ts` で `executionDetailPage` を注入する。各テストは `async ({ executionDetailPage }) => { ... }` で使う
- **モック**: `fixtures/mockStudioApi.ts` の `mockExecutionApi` が、ブラウザ側で `/api/core/**` と `/api/auth/me` をモックする。個別に返さないパスは 404（401 だとログイン画面へ飛ぶため）
- **実 API**: `fixtures/serviceApi.ts` がログイン・定義の作成・実行の開始・状態の待ち合わせを持つ

## 初回セットアップ

```bash
npm install
npx playwright install
```

## 実行

`nodejs/studio` から実行してください。

- ヘッドレスで実行: `npm run test:e2e`
- UI モードで実行: `npm run test:e2e:ui`

テスト実行時に `next dev` がポート **3200** で自動起動します（`E2E_PORT` で変えられる）。ローカルの Docker Compose が使う 3000（Studio）・3001（Grafana）・3100（Loki）と重ならないようにしています。3000 にすると、Compose の Studio（イメージのビルド時点のコード）を再利用してしまいます。

Playwright が起動する Next には、開発時の認証バイパス（`SERVICE_API_AUTH_TOKEN` にダミー値）を渡します。モックのスペックはログインせずに画面へ入れます。

## スペック

| ファイル | API | 内容 |
| --- | --- | --- |
| `smoke.spec.ts` | モック | ルートからダッシュボードへの移動、実行詳細の見出し（実行 ID の入力欄が無い） |
| `execution.spec.ts` | モック | パンくず、ノードの一覧とグラフ、権限による見出しの操作の出し分け、「このノードへ送る」からの再開 |
| `timelineReplay.spec.ts` | モック | イベントタイムラインの開閉・続きの読み込み・リプレイ |
| `core-api-real.spec.ts` | 実 API | Service API を直接呼ぶ（ヘルス、実行の開始とキャンセル、冪等キー） |
| `core-api-ui-execution.spec.ts` | 実 API | 一覧から詳細への移動、キャンセル、待機 1 つと並列分岐の待機 2 つへのイベント送信 |

モックのスペックは **Service API の起動は不要**です。

### Service API 実体を使うオプション E2E

事前に **PostgreSQL と Service API**（例: `http://localhost:8080`）を起動し、マイグレーション済みであることを前提とします。ローカルでは Docker Compose の構成で足ります。

次の環境変数がそろうと、`core-api-real.spec.ts` と `core-api-ui-execution.spec.ts` が実行されます（どれかが未設定のときはスキップ）。

| 環境変数 | 内容 |
| --- | --- |
| `SERVICE_API_E2E_URL` | Service API のオリジン |
| `E2E_TENANT_KEY` | ログインするテナントキー |
| `E2E_USERNAME` | ログインするユーザー名 |
| `E2E_PASSWORD` | ログインするパスワード |

ローカルの開発環境では、資格情報に Service API の `appsettings.Development.json` の `Statevia:Bootstrap:DevAdmin`（`TenantKey` / `Username` / `Password`）を使えます。ユーザーは `executions.write` を持ち、テナントに slug が `default` の Project が要ります（定義をそこに作る）。

**PowerShell の例**

```powershell
cd nodejs/studio
$env:SERVICE_API_E2E_URL = "http://localhost:8080"
$env:E2E_TENANT_KEY = "<テナントキー>"
$env:E2E_USERNAME = "<ユーザー名>"
$env:E2E_PASSWORD = "<パスワード>"
npx playwright test e2e/core-api-real.spec.ts e2e/core-api-ui-execution.spec.ts
```

**bash の例**

```bash
cd nodejs/studio
export SERVICE_API_E2E_URL=http://localhost:8080 E2E_TENANT_KEY='<テナントキー>' E2E_USERNAME='<ユーザー名>' E2E_PASSWORD='<パスワード>'
npx playwright test e2e/core-api-real.spec.ts e2e/core-api-ui-execution.spec.ts
```

`playwright.config.ts` は `SERVICE_API_E2E_URL` があるとき、Next のプロキシ用に **`SERVICE_API_INTERNAL_BASE`** を同じ値で渡します。別 URL にしたい場合は **`SERVICE_API_INTERNAL_BASE`** を明示してください。

UI のスペックは Next の `/api/auth/login` でログインし、Cookie をブラウザのコンテキストに置きます。定義の作成と実行の開始は Service API を直接呼びます。E2E で作った定義と実行は消さずに残ります（名前は `e2e_` で始まる）。

`SERVICE_API_E2E_URL` を張った状態で `npm run test:e2e` を叩くと、モック E2E と実 API E2E が混在するため負荷が上がります。トラブル時は上記のようにスペックを限定するか、一時的に `SERVICE_API_E2E_URL` を外してください。
