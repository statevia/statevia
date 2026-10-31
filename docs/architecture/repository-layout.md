# ディレクトリ構成

| 項目 | 値 |
| --- | --- |
| 種別 | Architecture |
| Version | 2.5 |
| 更新日 | 2026-10-31 |
| 関連 | [ui-studio-structure.md](ui-studio-structure.md), [overview.md](overview.md) |

**Version 2.5（2026-10-31）**: C# を `dotnet/`、Studio を `nodejs/studio/` に集約。C# の sln・`Directory.Build.*`・`Directory.Packages.props`・`global.json`・`.editorconfig` も `dotnet/` へ移した。リファレンス Module のソース（`dotnet/modules/reference/`）と、実行時の modules ルート（リポジトリルートの `modules/`）を分けた。

**Version 2.4（2026-10-31）**: ルートに C# の SDK を 10 系に限る `global.json` を追加。

**Version 2.3（2026-10-30）**: ルートに C# の `Directory.Build.props`（共通のビルド設定）と `Directory.Packages.props`（パッケージ版）を追加。既存の `Directory.Build.targets` もツリーに載せた。

**Version 2.2（2026-08-27）**: ルートに IDE 用包括 `statevia.sln` を追加。

**Version 2.1（2026-07-26）**: `ui/studio` を feature-first（`app` / `features` / `shared`）に合わせてツリーを更新。

---

C# を `dotnet/`、Node（Studio）を `nodejs/` に集めた現行レイアウトです。`dotnet/` の中は `core/` / `infrastructure/` / `service/` の層に分けます。

```txt
statevia/
├─ README.md
├─ LICENSE
├─ .gitignore
├─ docker-compose.yml            # postgres + service-api (C#) + ui
├─ .env.example                  # POSTGRES_*（.env はコミットしない）
│
├─ docs/
│  ├─ statevia-architecture.md   # システム構成 + レイヤー
│  ├─ statevia-design-philosophy.md
│  ├─ statevia-directory.md      # 本ファイル
│  ├─ development-guidelines.md  # 開発ガイドライン
│  ├─ specifications/api-http.md      # Service API HTTP 契約（v1）
│  ├─ guides/operations-docker.md       # Docker Compose 運用
│  ├─ specifications/execution/  # Engine 各仕様
│  └─ specifications/data-integration.md
│
├─ dotnet/                       # C# のすべて（compose の C# イメージのビルドコンテキスト）
│  ├─ statevia.sln               # IDE 用包括（CI / Warning 0 / Sonar の正本ではない）
│  ├─ Directory.Build.props      # C# の共通ビルド設定（対象 FW・Nullable・ImplicitUsings・Guid.NewGuid の禁止・実行時 modules ルート）
│  ├─ Directory.Build.targets    # C# の Sonar 用設定（テストの除外・SonarAnalyzer）
│  ├─ Directory.Packages.props   # C# のパッケージ版（Central Package Management）
│  ├─ global.json                # C# の SDK を 10 系に限る（rollForward: latestFeature）
│  ├─ .editorconfig              # C# の書式とアナライザーの設定
│  ├─ .dockerignore              # C# イメージのビルドコンテキストから bin/obj 等を除く
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
│  │  │  └─ samples/hello-statevia/
│  │  ├─ application/
│  │  │  ├─ Statevia.Core.Application/           # ユースケース実装
│  │  │  └─ Statevia.Core.Application.Contracts/ # DDD ポート・DTO
│  │  └─ actions/
│  │     └─ Statevia.Core.Actions.Abstractions/  # Action / Module SPI
│  │
│  ├─ modules/
│  │  └─ reference/              # first-party リファレンス（Http / Notification）のソース
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
│  ├─ service/                   # デプロイ可能なインターフェイス
│  │  ├─ api/
│  │  │  ├─ statevia-api.sln
│  │  │  ├─ Dockerfile
│  │  │  ├─ Statevia.Service.Api/              # HTTP アダプタ（Controllers + DI）
│  │  │  ├─ Statevia.Service.Api.Bootstrap/    # エントリポイント（Program.cs）
│  │  │  └─ Statevia.Service.Api.Tests/
│  │  ├─ action-host/
│  │  │  ├─ statevia-action-host.sln
│  │  │  ├─ Statevia.Service.ActionHost/       # gRPC sandbox
│  │  │  └─ Statevia.Service.ActionHost.Tests/
│  │  └─ cli/
│  │     ├─ statevia-cli.sln
│  │     ├─ Statevia.Service.Cli/              # statevia コマンド
│  │     └─ Statevia.Service.Cli.Tests/
│  │
│  ├─ tests/                     # 横断テスト
│  │  └─ Statevia.Architecture.Tests/  # 依存方向テスト
│  ├─ tools/                     # 補助の C# プロジェクト（容量計測等）
│  └─ build/                     # MSBuild の共有設定
│
├─ modules/                      # 実行時の modules ルート（gitignore。compose が /app/modules にマウント）
│  └─ default/                   # テナント実行時 DLL（リファレンス Module のビルドで copy）
│
├─ nodejs/
│  └─ studio/                    # UI（Next.js / React / ReactFlow — @statevia/studio）
│     ├─ package.json
│     ├─ app/                    # 薄い App Router（page / layout / BFF）
│     │  └─ api/core/[...path]/ # Service API プロキシ（/v1 へ転送）
│     ├─ features/               # 画面・ドメイン別（definitions, executions 等）
│     ├─ shared/                 # 横断 api / ui / i18n / auth / lib
│     └─ Dockerfile
│
└─ scripts/                      # ビルド・運用スクリプト
```

- **dotnet/**: C# のすべて。sln・共通のビルド設定・C# 用の `.editorconfig` はここに置き、リポジトリルートには置かない。SDK はカレントディレクトリから上をたどって `global.json` を探すため、`dotnet/global.json` は `dotnet/` の中で実行したコマンドにだけ効く。
- **dotnet/core/**: ドメインと契約。Engine は HTTP/DB に非依存。Application がユースケースを実装し、Contracts が DDD ポートを定義。
- **dotnet/modules/reference/**: ユーザー Module と同じ SPI の first-party 実装のソース。ビルドが DLL をリポジトリルートの `modules/default/` へ copy する。
- **dotnet/infrastructure/**: core の契約の技術実装。DB、認証、通知、Module ホスト等。
- **dotnet/service/**: HTTP / gRPC / CLI のアダプタ。Composition Root として DI を組み立てる。
- **dotnet/tests/**: ソリューション横断のアーキテクチャテスト。
- **modules/**: 実行時の modules ルート。ビルドがリファレンス Module を、CLI が Module を書き込む（git 管理外）。
- **nodejs/studio/**: Next.js（`@statevia/studio`）。`/api/core/*` で Service API にプロキシ。内部構成（`app` / `features` / `shared`）の正本は [ui-studio-structure.md](ui-studio-structure.md)。
