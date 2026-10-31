# AGENTS.md

Cursor Cloud / エージェント向けの**薄い索引**。詳細は [`docs/README.md`](docs/README.md) を正本とする。

## ドキュメント入口

| 目的 | 参照 |
| --- | --- |
| 利用者向け索引 | [`docs/README.md`](docs/README.md) |
| Quick Start | [`docs/guides/getting-started.md`](docs/guides/getting-started.md) |
| HTTP / データ契約 | [`docs/specifications/api-http.md`](docs/specifications/api-http.md), [`docs/specifications/data-integration.md`](docs/specifications/data-integration.md) |
| コーディング規約 | [`docs/development-guidelines.md`](docs/development-guidelines.md) |
| レイヤー・境界 | [`docs/architecture/overview.md`](docs/architecture/overview.md), [`docs/architecture/domain-model-boundaries.md`](docs/architecture/domain-model-boundaries.md) |

## リポジトリ構成（概要）

| コンポーネント | 場所 |
| --- | --- |
| engine（C# ライブラリ） | `dotnet/core/engine/` |
| application | `dotnet/core/application/` |
| infrastructure | `dotnet/infrastructure/` |
| Service API（ASP.NET Core） | `dotnet/service/api/` |
| runtime（共有 HostedService） | `dotnet/service/runtime/Statevia.Runtime/` |
| scheduler（Generic Host・任意） | `dotnet/service/runtime/Statevia.Service.Runtime.Scheduler/` |
| worker（Generic Host・任意） | `dotnet/service/runtime/Statevia.Service.Runtime.Worker/` |
| action-host（gRPC） | `dotnet/service/action-host/` |
| cli | `dotnet/service/cli/` |
| リファレンス Module（ソース） | `dotnet/modules/reference/`（実行時の modules ルートはリポジトリルートの `modules/`） |
| 横断テスト（依存方向） | `dotnet/tests/Statevia.Architecture.Tests/` |
| ui（Next.js） | `nodejs/studio/` |

C# は `dotnet/`、Node は `nodejs/` に置き、ルートには言語ごとのビルド設定を置かない。配置の規則と全体のツリーは [`docs/architecture/repository-layout.md`](docs/architecture/repository-layout.md)。

PostgreSQL 16 + EF Core。UI は `/api/core/*` で Service API にプロキシ。

## 起動

1. **PostgreSQL** — Docker 例: `docker compose up -d postgres`、または [`docs/guides/getting-started.md`](docs/guides/getting-started.md)
2. **マイグレーション** — `cd dotnet/service/api && dotnet ef database update --project Statevia.Service.Api`
3. **Service API** — `cd dotnet/service/api && dotnet run --project Statevia.Service.Api --no-launch-profile`（`ASPNETCORE_URLS=http://0.0.0.0:8080` 推奨。`launchSettings.json` は git 管理外で、VS がランダムポートを付ける）
4. **（任意）Scheduler / Worker 分離** — `docker compose -f docker-compose.yml -f compose/split-runtime.yml up -d`（API 内 HostedService Off と scheduler/worker 起動を同時に適用。手順: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)）
5. **UI** — `cd nodejs/studio && SERVICE_API_INTERNAL_BASE=http://localhost:8080 npm run dev`

Docker Compose 一式: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)

OpenAPI / Scalar: [`docs/reference/api-openapi.md`](docs/reference/api-openapi.md)

### Docker gotcha (Cloud VM)

`fuse-overlayfs` + `iptables-legacy`。`sudo dockerd` で手動起動が必要な場合あり。

## テスト

| 対象 | コマンド |
| --- | --- |
| engine | `cd dotnet/core/engine && dotnet test statevia-engine.sln` |
| Service API | `cd dotnet/service/api && dotnet test statevia-api.sln` |
| 依存方向（Architecture tests） | `cd dotnet && dotnet test tests/Statevia.Architecture.Tests/Statevia.Architecture.Tests.csproj` |
| Service API（シナリオのみ） | `./scripts/test-api.ps1 -ScenarioOnly`（Docker 必要） |
| Service API（シナリオ除外） | `./scripts/test-api.ps1 -SkipScenario` または `STATEVIA_SKIP_SCENARIO_TESTS=true` |
| ui | `cd nodejs/studio && npm run lint && npm run typecheck && npm run test:run` |

> **シナリオテスト（`Statevia.Service.Api.Scenario.Tests`）**は Testcontainers で PostgreSQL 16 を起動する。
> Docker が利用できない環境では `Skipped`（Failed にならない）。フィルタ: `Category=Scenario`。

Sonar / Analyzer: [`docs/development-guidelines.md`](docs/development-guidelines.md) §4.3 / §5

## 主要 env / 設定（抜粋）

| 変数 / 設定 | 用途 |
| --- | --- |
| `DATABASE_URL` | PostgreSQL 接続 |
| `SERVICE_API_INTERNAL_BASE` | UI → Service API |
| `STATEVIA_ENABLE_API_DOCS` | 本番で OpenAPI / Scalar を有効化 |
| `STATEVIA_MODULES_PATH` | 実行時の Action Module ルート（compose ではリポジトリルートの `modules/` を `/app/modules` にマウント。ソースは `dotnet/modules/`） |
| `Statevia:ActionHost:BaseUrl` | OutOfProcess 実行（未設定時は `ActionHostNotConfigured`） |
| `Statevia:Modules:Signing:*` | Module 署名・TrustLevel |
| `Statevia:Modules:Oci:*` | OCI Module Source |
| `Statevia:Modules:S3:*` | S3 Module Source |
| `Statevia:Modules:Git:*` | Git Module Source（HTTP archive） |
| `Statevia:ExecutionPolicy:*` | 実行モード下限・テナント Policy / `Sandbox:Docker:*`（Container） |
| `Statevia:Runtime:*` | API 内 Worker / DelayWait Scheduler / OwnershipRecovery の On/Off（既定 On） |

完全な一覧と説明: [`docs/guides/operations-docker.md`](docs/guides/operations-docker.md)、[`docs/specifications/actions/platform.md`](docs/specifications/actions/platform.md)

## 実装メモ（エージェント向け）

- **IDE sln**: `dotnet/statevia.sln` は Cursor / VS Code の IntelliSense 用（`.vscode/settings.json` の `dotnet.defaultSolution`）。CI / テスト / Warning 0 / Sonar はコンポーネント別 6 sln。
- **Read-model**: `GET /v1/executions` の一覧は `execution_branches.execution_id` に無いルートのみ（件数も同じ）。graph は DB projection 正本（[`data-integration.md`](docs/specifications/data-integration.md)）。Hosted 物理 Fork の親 graph / events は **GET 時合成**（一覧を含め UI は分割非認知。[`fork-join.md`](docs/specifications/execution/fork-join.md)）
- **機微 IO 方針**: 既定で `input` / `output` を一覧 GET に含めない。ログは `LogRedaction`（[`io-log-masking.md`](docs/specifications/platform/io-log-masking.md)）
- **Engine 境界**: `ExecutionEngine` は `IStateExecutor` のみ。Catalog / Policy / ModuleHost は Service API 側。Hosted Fork の親子協調は Application（`execution_branches`・予約 Resume）
- **Execution Facade**: HTTP / Worker は `IExecutionService` のみ。実処理は `dotnet/core/application` のドメインサービス（Query / Lifecycle / WaitEvent / Checkpoint / Ownership / Recovery / Projection）。境界は [`docs/architecture/domain-model-boundaries.md`](docs/architecture/domain-model-boundaries.md)
- **Serena MCP**: C# は `serena-csharp`（`dotnet/` / `statevia.sln`）、UI は `serena-ui`。切替は UI トグル（同時 On は原則 1）。未起動時は停止して起動を促す。手順は [`.cursor/skills/serena-mcp-project-switch/SKILL.md`](.cursor/skills/serena-mcp-project-switch/SKILL.md)。

## .NET SDK

.NET 10 SDK が必要（`dotnet/global.json` で 10 系に限っている。SDK はカレントディレクトリから `global.json` を探すため、`dotnet/` の外で実行したコマンドには効かない）。Cloud VM での置き場所は `/usr/local/share/dotnet`。PATH は `~/.bashrc` 参照。
