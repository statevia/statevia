# ディレクトリ構成

| 項目 | 値 |
| --- | --- |
| 種別 | Architecture |
| Version | 2.8 |
| 更新日 | 2026-11-01 |
| 関連 | [ui-studio-structure.md](ui-studio-structure.md), [overview.md](overview.md) |

**Version 2.8（2026-11-01）**: ツリーを `git ls-files` の実際の構成に合わせた（存在しない docs のファイルを消し、エージェント設定・`dotnet/service/runtime/`・シナリオテスト・`dotnet/tools/capacity/` 等を足した）。ルート直下に置いてよいファイルとフォルダの役割、ツールチェーンの境界、C# の依存方向と既知の負債、Studio のテスト配置の参照先を書いた。

**Version 2.7（2026-10-31）**: Compose の重ね合わせファイル 3 つを `compose/` へ移した（`compose/split-runtime.yml` / `compose/logging.yml` / `compose/logging-split.yml`）。`sonar/` を解体し、スキャナと README を `scripts/sonar/`、Sonar サーバの compose を `compose/sonar.yml`、`dotnet-format-all.ps1` と `measure-loc.ps1` を `scripts/`、カバレッジ XML を `artifacts/sonar/` へ移した。

**Version 2.6（2026-10-31）**: テスト用 Module のフィクスチャ（Api.Tests と action-host の下にあった 3 つ）を `dotnet/tests/Fixtures/ActionModules/` に集めた。

**Version 2.5（2026-10-31）**: C# を `dotnet/`、Studio を `nodejs/studio/` に集約。C# の sln・`Directory.Build.*`・`Directory.Packages.props`・`global.json`・`.editorconfig` も `dotnet/` へ移した。リファレンス Module のソース（`dotnet/modules/reference/`）と、実行時の modules ルート（リポジトリルートの `modules/`）を分けた。

**Version 2.4（2026-10-31）**: ルートに C# の SDK を 10 系に限る `global.json` を追加。

**Version 2.3（2026-10-30）**: ルートに C# の `Directory.Build.props`（共通のビルド設定）と `Directory.Packages.props`（パッケージ版）を追加。既存の `Directory.Build.targets` もツリーに載せた。

**Version 2.2（2026-08-27）**: ルートに IDE 用包括 `statevia.sln` を追加。

**Version 2.1（2026-07-26）**: `ui/studio` を feature-first（`app` / `features` / `shared`）に合わせてツリーを更新。

---

C# を `dotnet/`、Node（Studio）を `nodejs/` に集めた現行レイアウトです。本ファイルは配置の正本で、新しいファイルやフォルダを足すときは §2 の規則に従います。

## 1. ツリー

git で追跡しているものを載せます。`（gitignore）` と書いたものは追跡せず、手元で作られます。

```txt
statevia/
├─ README.md
├─ LICENSE
├─ AGENTS.md                     # エージェント向けの起動・テスト・構成の要約
├─ CLAUDE.md                     # Claude Code 向けの規約（.cursor/rules の変換）
├─ .cursorignore
├─ .gitignore
├─ .gitleaks.toml                # 秘密情報の検出設定
├─ .lychee.toml                  # Markdown のリンク検査の設定
├─ .markdownlint.json
├─ docker-compose.yml            # ローカル起動の入口（postgres + service-api (C#) + action-host + ui）
├─ .env.example                  # POSTGRES_*・COMPOSE_FILE の例（.env はコミットしない）
│
├─ .claude/                      # Claude Code の設定・コマンド・スキル・フック（多くは .cursor/ の正本を参照）
├─ .cursor/                      # Cursor のルール・コマンド・スキル・フック（エージェント設定の正本）
├─ .github/workflows/            # CI（ci.yml）と Sonar（sonar.yml）
├─ .spec/                        # Spec の Steering とテンプレート（specs/ は gitignore）
│
├─ compose/                      # Compose の重ね合わせ定義と、その設定ファイル
│  ├─ split-runtime.yml          # Scheduler / Worker を別プロセスにする
│  ├─ logging.yml                # Fluent Bit / Loki / Grafana
│  ├─ logging-split.yml          # logging.yml の Worker / Scheduler 向け追記（split-runtime と併用）
│  ├─ logging/                   # logging.yml がマウントする設定
│  └─ sonar.yml                  # ローカル SonarQube（name: sonar）
│
├─ docs/                         # 利用者・開発者向け文書（索引は docs/README.md）
│  ├─ README.md
│  ├─ DOCUMENTATION-STANDARD.md  # 文書の書き方
│  ├─ development-guidelines.md  # 開発ガイドライン
│  ├─ architecture/              # 構成・境界（本ファイル、overview.md 等）
│  ├─ concepts/                  # 概念の説明
│  ├─ decisions/                 # 設計判断の記録
│  ├─ future/                    # 将来構想
│  ├─ guides/                    # 手順（getting-started.md、operations-docker.md 等）
│  ├─ images/
│  ├─ reference/                 # 一覧・早見表（環境変数、エラーコード等）
│  ├─ samples/                   # 定義の例
│  └─ specifications/            # 契約（api-http.md、definition.md、actions/ 等）
│
├─ dotnet/                       # C# のすべて（compose の C# イメージのビルドコンテキスト）
│  ├─ statevia.sln               # IDE 用包括（CI / Warning 0 / Sonar の正本ではない）
│  ├─ Directory.Build.props      # C# の共通ビルド設定（対象 FW・Nullable・ImplicitUsings・Guid.NewGuid の禁止・実行時 modules ルート）
│  ├─ Directory.Build.targets    # C# の Sonar 用設定（テストの除外・SonarAnalyzer）
│  ├─ Directory.Packages.props   # C# のパッケージ版（Central Package Management）
│  ├─ global.json                # C# の SDK を 10 系に限る（rollForward: latestFeature）
│  ├─ .editorconfig              # C# の書式とアナライザーの設定
│  ├─ .dockerignore              # C# イメージのビルドコンテキストから bin/obj 等を除く
│  ├─ .serena/                   # Serena（serena-csharp）のプロジェクト設定
│  │
│  ├─ build/                     # MSBuild の共有設定（BannedGuidNewGuid.props・BannedSymbols.txt・SonarQube.ComponentScope.props）
│  │
│  ├─ core/                      # ドメイン・契約（非デプロイ）
│  │  ├─ engine/
│  │  │  ├─ statevia-engine.sln
│  │  │  ├─ Statevia.Core.Engine/          # ワークフローエンジン
│  │  │  │  ├─ Engine/                     # ExecutionEngine, ExecutionInstance
│  │  │  │  ├─ FSM/                        # 遷移評価
│  │  │  │  ├─ Scheduler/                  # 並列制御
│  │  │  │  ├─ Execution/                  # DefaultStateExecutor
│  │  │  │  ├─ Join/                       # JoinTracker
│  │  │  │  ├─ Definition/                 # 定義ロード・コンパイル・検証
│  │  │  │  ├─ ExecutionGraph/             # 実行グラフ
│  │  │  │  └─ Abstractions/
│  │  │  ├─ Statevia.Core.Engine.Tests/
│  │  │  └─ samples/hello-statevia/        # Engine 単体の利用例
│  │  ├─ application/
│  │  │  ├─ Statevia.Core.Application/           # ユースケース実装
│  │  │  └─ Statevia.Core.Application.Contracts/ # DDD ポート・DTO
│  │  └─ actions/
│  │     └─ Statevia.Core.Actions.Abstractions/  # Action / Module SPI
│  │
│  ├─ infrastructure/            # 技術実装（差し替え可能・最外殻）
│  │  ├─ statevia-infrastructure.sln
│  │  ├─ Statevia.Infrastructure.Persistence/    # EF Core + Migrations
│  │  ├─ Statevia.Infrastructure.Security/       # JWT, Tenant, 認可
│  │  ├─ Statevia.Infrastructure.Notification/   # SMTP 通知
│  │  ├─ Statevia.Infrastructure.Modules/        # Module ホスト + Source
│  │  ├─ Statevia.Infrastructure.Actions.Grpc/   # gRPC Action Backend
│  │  ├─ Statevia.Infrastructure.Common/         # IdGenerator 等
│  │  ├─ Statevia.Infrastructure.Modules.Tests/
│  │  └─ Statevia.Infrastructure.Actions.Grpc.Tests/
│  │
│  ├─ modules/
│  │  ├─ reference/              # first-party リファレンス Module のソース
│  │  │  ├─ statevia-reference.sln
│  │  │  ├─ Statevia.Reference.Http/ と .Tests/
│  │  │  └─ Statevia.Reference.Notification/ と .Tests/
│  │  └─ local/                  # 手元実験用 Module のソース（gitignore）
│  │
│  ├─ service/                   # デプロイ可能なインターフェイス
│  │  ├─ api/
│  │  │  ├─ statevia-api.sln
│  │  │  ├─ Dockerfile
│  │  │  ├─ Statevia.Service.Api/              # HTTP アダプタ（Controllers + DI）
│  │  │  ├─ Statevia.Service.Api.Bootstrap/    # エントリポイント（Program.cs）
│  │  │  ├─ Statevia.Service.Api.Tests/
│  │  │  └─ Statevia.Service.Api.Scenario.Tests/ # Testcontainers の PostgreSQL を使うシナリオテスト
│  │  ├─ runtime/                # Worker / Scheduler（statevia-api.sln に含まれる）
│  │  │  ├─ Statevia.Runtime/                  # Worker / DelayWait Scheduler / OwnershipRecovery の HostedService
│  │  │  ├─ Statevia.Service.Runtime.Scheduler/ # 分離した Scheduler の Generic Host（任意）
│  │  │  └─ Statevia.Service.Runtime.Worker/    # 分離した Worker の Generic Host（任意）
│  │  ├─ action-host/
│  │  │  ├─ statevia-action-host.sln
│  │  │  ├─ Dockerfile
│  │  │  ├─ Statevia.Service.ActionHost/       # gRPC sandbox
│  │  │  └─ Statevia.Service.ActionHost.Tests/
│  │  └─ cli/
│  │     ├─ statevia-cli.sln
│  │     ├─ Statevia.Service.Cli/              # statevia コマンド
│  │     └─ Statevia.Service.Cli.Tests/
│  │
│  ├─ tests/                     # 横断テスト・横断フィクスチャ
│  │  ├─ Statevia.Architecture.Tests/  # 依存方向テスト・ログの EventId のテスト
│  │  └─ Fixtures/ActionModules/       # テスト用 Module（Api.Tests / ActionHost.Tests が読み込む）
│  │     ├─ TestActionModule/
│  │     ├─ MalformedActionModule/
│  │     └─ PrivateCtorActionModule/
│  │
│  └─ tools/
│     └─ capacity/               # 容量計測ハーネス（CI の常時実行外）
│        ├─ Statevia.Tools.Capacity.sln
│        ├─ Statevia.Tools.Capacity/ と .Tests/
│        ├─ run-worker-grid.ps1
│        └─ results/             # 計測の生結果（gitignore）
│
├─ nodejs/
│  └─ studio/                    # UI（Next.js / React / ReactFlow — @statevia/studio）
│     ├─ package.json
│     ├─ app/                    # 薄い App Router（page / layout / BFF）
│     │  └─ api/core/[...path]/  # Service API プロキシ（/v1 へ転送）
│     ├─ features/               # 画面・ドメイン別（definitions, executions, projects 等）
│     ├─ shared/                 # 横断 api / ui / i18n / auth / project / lib
│     ├─ tests/                  # 単体テスト（ソースの位置を映して置く）
│     ├─ e2e/                    # Playwright
│     └─ Dockerfile
│
├─ scripts/                      # 言語をまたぐ、またはリポジトリ全体の運用スクリプト
│  ├─ dotnet-format-all.ps1      # C# の整形（CI の format-check と編集後フックが呼ぶ）
│  ├─ test-api.ps1               # Service API のテスト（シナリオの有無を選ぶ）
│  ├─ export-core-api-openapi.ps1
│  ├─ bootstrap-tenant*.ps1      # テナントと管理者の初期作成
│  ├─ measure-*.ps1・api-measure-common.ps1 # 行数・API の TAT / RPS・sandbox の資源の計測
│  └─ sonar/                     # Sonar のスキャナと README（プロジェクトキー一覧の正本）
│
├─ modules/                      # 実行時の modules ルート（gitignore。compose が /app/modules にマウント）
│  └─ default/                   # テナント default の Module（リファレンス Module のビルドで copy）
├─ artifacts/                    # ビルド・スキャンの出力（gitignore。Sonar のカバレッジ XML は artifacts/sonar/）
└─ .local/                       # 手元の秘密情報・実験データ（gitignore）
```

## 2. 配置の規則

### 2.1 ツールチェーンの境界

- ルート直下のコードのフォルダは、それぞれ 1 つのツールチェーンの根にする。C# は `dotnet/`、Node（TS）は `nodejs/` に置く。
- ルートには、言語ごとのビルド設定を置かない。C# の sln・`Directory.Build.*`・`Directory.Packages.props`・`global.json`・C# 用の `.editorconfig`・`.serena/` は `dotnet/` に、Node の `package.json`・ESLint・tsconfig 等は `nodejs/studio/` に置く。
- SDK はカレントディレクトリから上をたどって `global.json` を探すため、`dotnet/global.json` は `dotnet/` の中で実行したコマンドにだけ効く。`dotnet` コマンドは `dotnet/` の下で実行する。
- C# イメージのビルドコンテキストは `dotnet/` で、`dotnet/.dockerignore` がホストの `bin/` / `obj/` を除く。

### 2.2 ルート直下に置いてよいファイル

| 種類 | ファイル |
| --- | --- |
| リポジトリ説明 | `README.md`, `LICENSE` |
| エージェント設定 | `AGENTS.md`, `CLAUDE.md`, `.cursorignore` |
| リポジトリ全体のツール設定 | `.gitignore`, `.gitleaks.toml`, `.lychee.toml`, `.markdownlint.json` |
| ローカル起動の入口 | `docker-compose.yml`, `.env.example` |

これ以外のファイルをルートに足すときは、この表を更新する。

### 2.3 ルート直下のフォルダの役割

新しいフォルダを足すときは、まず次のどれかに入らないかを確かめる。

| 区分 | フォルダ | 置くもの |
| --- | --- | --- |
| コード | `dotnet/` | C# のすべて（§2.4） |
| コード | `nodejs/` | Node（TS）のすべて。現在は `studio/` だけ。npm workspaces を使う場合は、その `package.json` もここに置く |
| 支援 | `compose/` | Compose の重ね合わせ定義と、その設定ファイル（logging、Sonar サーバ）。入口の `docker-compose.yml` だけはルートに置く |
| 支援 | `scripts/` | 言語をまたぐ、またはリポジトリ全体の運用スクリプト（`scripts/sonar/` を含む） |
| 文書 | `docs/` | 利用者・開発者向け文書 |
| エージェント・CI | `.claude/`, `.cursor/`, `.github/`, `.spec/` | 各ツールの設定と Spec |
| 生成物（gitignore） | `artifacts/` | ビルド・スキャンの出力 |
| 実行時データ（gitignore） | `modules/` | 実行時の modules ルート（§2.5） |
| ローカルデータ（gitignore） | `.local/` | 手元の秘密情報・実験データ |

`scripts/` のうち C# だけを扱うもの（`dotnet-format-all.ps1`、`test-api.ps1`、`export-core-api-openapi.ps1` 等）も、CI と文書がリポジトリルートからの実行を前提にしているため `scripts/` に置く。`scripts/` の分類は本数が増えてから見直す。

### 2.4 `dotnet/` の直下

| フォルダ | 置くもの |
| --- | --- |
| `core/` / `infrastructure/` / `service/` | 各層のソース。依存方向は §3 |
| `modules/` | Action Module のソースだけ（`reference/` と、手元実験用の `local/`） |
| `tests/` | 横断テストと、複数のテストプロジェクトが使うフィクスチャ。テストプロジェクトは、他のテストプロジェクトのフォルダを参照しない |
| `tools/` | 補助の C# プロジェクトと、その付属スクリプト |
| `build/` | MSBuild の共有設定だけ |

各コンポーネントのテストプロジェクトは、対象プロジェクトの隣に `<対象>.Tests/` として置く。CI・テスト・Warning 0・Sonar の単位はコンポーネント別の sln（engine / infrastructure / reference / api / action-host / cli）で、`dotnet/statevia.sln` は IDE 用である。

### 2.5 実行時の modules ルート

- 実行時の modules ルートは、リポジトリルートの `modules/` である（git 管理外）。compose がコンテナの `/app/modules` にマウントし、Service API・Worker・Action Host が `modules/<テナントキー>/<Module>/` を読む。
- 書き込むのは、ホストでのリファレンス Module のビルド（`modules/default/<ModuleId>/` へ DLL を copy。基点は `dotnet/Directory.Build.props` の `StateviaModulesOutputRoot`）と、CLI の `statevia module install`（既定の書き込み先は `./modules/<テナントキー>/`）である。
- Module のソースは `dotnet/modules/` に置き、`modules/` には置かない（`modules/` 直下のディレクトリはテナントとして読まれる）。
- 手順は [operations-docker.md](../guides/operations-docker.md) の「Action Module（plugins）」、展開後の形は [module-zip-layout.md](../specifications/actions/module-zip-layout.md)。

### 2.6 Studio の内部構成とテスト配置

Studio（`nodejs/studio/`）の内部構成（`app` / `features` / `shared`）、feature 間の依存の規則、単体テストの配置（`tests/` の下に対象ソースの位置を映す）の正本は [ui-studio-structure.md](ui-studio-structure.md) である。

## 3. C# の依存方向

依存方向の正本は [overview.md](overview.md) §3 である。要点は次のとおり。

- `service` → `infrastructure` → `core`（Contracts / Abstractions）の向きだけを許す。`core` の中では、`application` が `engine`・`application.contracts`・`actions.abstractions` を参照する。
- `core/*` → `infrastructure/*` / `service/*`、`infrastructure/*` → `service/*`、リファレンス Module → Module が使ってよい範囲の外の Engine の型は、`dotnet/tests/Statevia.Architecture.Tests` で機械的に検出する（CI の Architecture tests）。
- 既知の負債（Worker / Scheduler → Service.Api、Application → EF Core）は overview.md §3.1 に理由とともに書いている。

## 4. フォルダごとの補足

- **dotnet/core/**: ドメインと契約。Engine は HTTP/DB に非依存。Application がユースケースを実装し、Contracts が DDD ポートを定義。
- **dotnet/modules/reference/**: ユーザー Module と同じ SPI の first-party 実装のソース。ビルドが DLL をリポジトリルートの `modules/default/` へ copy する。
- **dotnet/infrastructure/**: core の契約の技術実装。DB、認証、通知、Module ホスト等。
- **dotnet/service/**: HTTP / gRPC / CLI のアダプタ。Composition Root として DI を組み立てる。
- **dotnet/tests/**: ソリューション横断のアーキテクチャテストと、複数のテストプロジェクトが使うフィクスチャ。テスト用 Module は `Fixtures/ActionModules/` に置く。
- **docker-compose.yml / compose/**: `-f` で重ねたファイルの中の相対パスは、最初に指定した `docker-compose.yml` の場所（リポジトリルート）を基準に解決される。手元の `.env` に `COMPOSE_FILE` を書けば、`-f` を並べずに重ねて起動できる（[operations-docker.md](../guides/operations-docker.md)）。
- **scripts/sonar/**: Sonar のスキャナ。生成物は `artifacts/sonar/` に出す。プロジェクトキーの一覧は [scripts/sonar/README.md](../../scripts/sonar/README.md)。
- **nodejs/studio/**: Next.js（`@statevia/studio`）。`/api/core/*` で Service API にプロキシ。
