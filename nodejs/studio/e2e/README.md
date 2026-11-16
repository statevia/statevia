# E2E テスト (Playwright)

Studio の E2E です。**fixture で組み立て、Page Object は薄く**保ちます。分岐の網羅は Vitest（`tests/`）に任せ、E2E は画面をまたぐ導線と、画面と API の契約のつなぎ目だけを検証します。

## 構成

```text
e2e/
├─ specs/
│  ├─ mock/          # mock project。ブラウザで API をモックする（Service API は不要）
│  └─ real-api/      # real-api project。Service API の実体を使う
├─ setup/
│  └─ auth.setup.ts  # setup-real project。1 回だけログインして Cookie を e2e/.auth/ に保存する
├─ fixtures/
│  ├─ test.ts        # spec の入口。mock 用の test と実 API 用の realTest、expect を出す
│  ├─ mockStudioApi.ts / mockExecution.ts / mockTimeline.ts   # モックの応答（契約の型）
│  ├─ serviceApi.ts  # 実 API の補助（定義の作成・実行の開始・状態の待ち合わせ）
│  └─ e2eWaitWorkflowYaml.ts
├─ pages/            # 画面 1 つに 1 クラスの Page Object
├─ components/       # 複数の画面に出る部品（タイムライン・イベント送信ダイアログ・トースト）
└─ support/
   ├─ env.ts         # 実 API の環境変数と Cookie の保存先
   └─ step.ts        # 操作を利用者の語のステップ名でレポートに出す
```

## 書き方の規約

`npm run lint` が `e2e/**` に `eslint-plugin-playwright` の規則を当てます（警告も失敗）。

- **spec**
  - `test` と `expect` は `fixtures/test.ts` からだけ読む。mock は `test`、real-api は `realTest`。
  - AAA（`// Arrange` / `// Act` / `// Assert`）で、1 ケース 1 シナリオ。シナリオ名は日本語で、きっかけと期待を 1 文で書く。
  - 検証は web-first assertion（`await expect(locator).toBeVisible()` など）。`isVisible()` の結果を `expect` しない。
  - `if` / `for`、固定の待ち時間（`waitForTimeout`）、`force: true`、`test.skip` を書かない。
  - ロケーターを spec に直接書かない。Page Object かコンポーネントオブジェクトに足す。
- **Page Object / コンポーネントオブジェクト**
  - ロケーターはコンストラクタで `readonly` のフィールドに作る。引数で絞るものだけメソッドにする（`heading(executionId)` など）。
  - `getByRole` と `uiText`（日本語の辞書）の語で特定する。role とアクセシブルネームで特定できないときだけ ID などを使い、`// Reason:` を書く。
  - 操作は利用者の意図の単位のメソッドにし、`step("実行をキャンセルする", ...)` で包む。
  - `expect`・`waitFor` を持たない。読み込みを待つときは、spec で見出しなどを `expect` する。
  - 複数の画面に出る部品は `components/` に置き、Page Object が合成で持つ。継承は使わない。
- **モック**
  - `mockApi`（auto fixture）は、どの mock の spec でも有効です。`await mockApi.setup(options)` で実行一覧・実行詳細の API と `/api/auth/me` を返します。
  - キャンセルなどを受け付けた後に画面が実行を取り直した結果は、`executionAfterCommand` で返します。
  - 実行一覧は所属 Project の先頭を選んでから一覧を取得します。既定の所属 Project は空なので、一覧を出すシナリオでは `projects` も渡してください。
  - **モックしていない `/api/core/**` が呼ばれるとシナリオが失敗します**（呼ばれたメソッドとパスがエラーに出る）。画面が新しい API を呼ぶようになったら、`mockStudioApi.ts` に足してください。
  - 応答は HTTP の契約の型（`ExecutionDTO` など）で書き、契約が変わったら `tsc --noEmit` で検出します。

## 初回セットアップ

```bash
npm install
npx playwright install chromium
```

## 実行

`nodejs/studio` から実行してください。

- ヘッドレスで実行: `npm run test:e2e`
- UI モードで実行: `npm run test:e2e:ui`
- mock だけ: `npx playwright test --project=mock`

テスト実行時に `next dev` がポート **3200** で自動起動します（`E2E_PORT` で変えられる）。ローカルの Docker Compose が使う 3000（Studio）・3001（Grafana）・3100（Loki）と重ならないようにしています。3000 にすると、Compose の Studio（イメージのビルド時点のコード）を再利用してしまいます。

Playwright が起動する Next には、開発時の認証バイパス（`SERVICE_API_AUTH_TOKEN` にダミー値）を渡します。mock の spec はログインせずに画面へ入れます。管理画面と定期実行の画面はサーバー側で Service API を呼ぶのでモックできず、real-api でだけ扱います。

## スペック

| ファイル | project | 内容 |
| --- | --- | --- |
| `specs/mock/smoke.spec.ts` | mock | ルートからダッシュボードへの移動、実行詳細の見出し（実行 ID の入力欄が無い） |
| `specs/mock/executions-list.spec.ts` | mock | 一覧の表示と件数、所属 Project が無いときの空の表示、実行詳細への移動、権限による「イベントを送信」の出し分け |
| `specs/mock/execution-detail.spec.ts` | mock | パンくず、ノードの一覧とグラフ、権限による見出しの操作の出し分け、キャンセルの送信と成功のトースト、終端の実行でキャンセルが押せないこと、「このノードへ送る」からの再開 |
| `specs/mock/execution-timeline.spec.ts` | mock | イベントタイムラインの開閉・続きの読み込み・リプレイ |
| `specs/real-api/execution-commands.spec.ts` | real-api | 一覧から詳細への移動、キャンセル、待機 1 つと並列分岐の待機 2 つへのイベント送信 |
| `specs/real-api/admin-screens.spec.ts` | real-api | テナント管理者の Cookie で、管理画面（ユーザー管理）と定期実行の一覧が開き、一覧の API が返ること。データは作らない |

### Service API 実体を使う real-api

事前に **PostgreSQL と Service API**（例: `http://localhost:8080`）を起動し、マイグレーション済みであることを前提とします。ローカルでは Docker Compose の構成で足ります。

次の環境変数がすべてそろうと、`setup-real` と `real-api` の project が定義されます。どれかが無いときは project ごと無く、スキップとしても数えません。

| 環境変数 | 内容 |
| --- | --- |
| `SERVICE_API_E2E_URL` | Service API のオリジン |
| `E2E_TENANT_KEY` | ログインするテナントキー |
| `E2E_USERNAME` | ログインするユーザー名 |
| `E2E_PASSWORD` | ログインするパスワード |

ローカルの開発環境では、資格情報に Service API の `appsettings.Development.json` の `Statevia:Bootstrap:DevAdmin`（`TenantKey` / `Username` / `Password`）を使えます。ユーザーは `executions.write` を持つテナント管理者で（管理画面と定期実行の画面はテナント管理者だけが開ける）、テナントに slug が `default` の Project が要ります（定義をそこに作る）。

**PowerShell の例**

```powershell
cd nodejs/studio
$env:SERVICE_API_E2E_URL = "http://localhost:8080"
$env:E2E_TENANT_KEY = "<テナントキー>"
$env:E2E_USERNAME = "<ユーザー名>"
$env:E2E_PASSWORD = "<パスワード>"
npx playwright test --project=real-api
```

**bash の例**

```bash
cd nodejs/studio
export SERVICE_API_E2E_URL=http://localhost:8080 E2E_TENANT_KEY='<テナントキー>' E2E_USERNAME='<ユーザー名>' E2E_PASSWORD='<パスワード>'
npx playwright test --project=real-api
```

`--project=real-api` は依存する `setup-real` を先に走らせます。setup-real は Next の `/api/auth/login` で 1 回だけログインし、Cookie を `e2e/.auth/studio.json` に保存します（`.gitignore` の対象。コミットしない）。各シナリオはこの Cookie を読み込みます。setup-real はトレースを取りません（ログインに失敗したときのトレースにパスワードが残るため）。ユーザー名などの資格情報の値は、ロケーターやステップ名に使わないでください。定義の作成と実行の開始は Service API を直接呼びます（fixture `serviceApi`）。E2E で作った定義と実行は消さずに残ります（名前は `e2e_` で始まる）。

`playwright.config.ts` は `SERVICE_API_E2E_URL` があるとき、Next のプロキシ用に **`SERVICE_API_INTERNAL_BASE`** を同じ値で渡します。別 URL にしたい場合は **`SERVICE_API_INTERNAL_BASE`** を明示してください。

環境変数をそろえたまま `npm run test:e2e` を叩くと、mock と real-api の両方が走ります。片方だけにするときは `--project` で選んでください。
