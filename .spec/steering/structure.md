# Project Structure Steering — Statevia

## Top-Level Layout

```text
dotnet/                # C# のすべて（sln・共通のビルド設定を含む）
├─ core/               # ドメイン・契約（非デプロイ）
├─ infrastructure/     # 技術実装（差し替え可能・最外殻）
├─ service/            # デプロイ可能なインターフェイス（API / CLI / action-host / runtime）
├─ tests/              # 横断テスト（Architecture.Tests）と横断フィクスチャ（Fixtures/）
└─ tools/capacity/     # 容量計測ハーネス（CI 常時実行外）
nodejs/studio/         # Web ダッシュボード（Next.js — @statevia/studio）
docs/                  # 契約・運用・開発ガイド
compose/               # Compose の重ね合わせ定義（split-runtime / logging / sonar）。入口はルートの docker-compose.yml
scripts/               # 運用スクリプト（dotnet-format-all.ps1・measure-loc.ps1、Sonar は scripts/sonar/）
```

## Core（`dotnet/core/`）

ドメインと契約。HTTP/DB に直接依存しない。

| プロジェクト | 役割 |
| --- | --- |
| `dotnet/core/engine/Statevia.Core.Engine` | ワークフローエンジン（FSM, Graph, Scheduler） |
| `dotnet/core/application/Statevia.Core.Application` | ユースケース実装 |
| `dotnet/core/application/Statevia.Core.Application.Contracts` | DDD ポート / DTO（リポジトリ、UoW、サービスインターフェース） |
| `dotnet/core/actions/Statevia.Core.Actions.Abstractions` | Action / Module SPI |

## Infrastructure（`dotnet/infrastructure/`）

core の契約（`Application.Contracts` / `Actions.Abstractions`）の技術実装。

| プロジェクト | 役割 |
| --- | --- |
| `Statevia.Infrastructure.Persistence` | EF Core + Migrations |
| `Statevia.Infrastructure.Security` | JWT, Tenant context, 認可 |
| `Statevia.Infrastructure.Notification` | SMTP 通知 |
| `Statevia.Infrastructure.Modules` | Module ホスト + OCI/filesystem Source |
| `Statevia.Infrastructure.Actions.Grpc` | gRPC Action Backend |
| `Statevia.Infrastructure.Common` | IdGenerator (UUID v7) 等 |

## Service（`dotnet/service/`）

HTTP / gRPC / CLI のアダプタ。Composition Root として全層を DI で結合。

| プロジェクト | 役割 |
| --- | --- |
| `dotnet/service/api/Statevia.Service.Api` | HTTP アダプタ（Controllers + Hosting） |
| `dotnet/service/api/Statevia.Service.Api.Bootstrap` | エントリポイント（Program.cs） |
| `dotnet/service/runtime/Statevia.Runtime` | Worker / DelayWait Scheduler / OwnershipRecovery の HostedService 正本 |
| `dotnet/service/runtime/Statevia.Service.Runtime.Scheduler` | DelayWait / ownership recovery Generic Host（任意分離） |
| `dotnet/service/runtime/Statevia.Service.Runtime.Worker` | execution_work_items Worker Generic Host（任意分離） |
| `dotnet/service/action-host/Statevia.Service.ActionHost` | OutOfProcess Action 実行（gRPC sandbox） |
| `dotnet/service/cli/Statevia.Service.Cli` | 統合 `statevia` コマンド |

## UI（`nodejs/studio/`）

- Next.js の route handler で Service API にプロキシし、ブラウザからは同一オリジンの `/api/core/...` 等を利用。
- 環境変数 `SERVICE_API_INTERNAL_BASE` でバックエンド基底 URL を指定（`AGENTS.md` の表）。

## Tests（`dotnet/tests/`）

- `dotnet/tests/Statevia.Architecture.Tests`: `NetArchTest.eNhancedEdition` で依存方向禁止ルールを機械的に検証。
- `dotnet/tests/Fixtures/ActionModules/`: 複数のテストプロジェクト（Api.Tests / ActionHost.Tests）が読み込むテスト用 Module。テストプロジェクトは、他のテストプロジェクトのフォルダを参照しない。

## Module Boundaries

- **Engine ↔ Application**: `IExecutionEngine` 越しのみ。Application はユースケースを実装し Engine を利用。
- **Application ↔ Infrastructure**: `Application.Contracts` のポート越しのみ。Infrastructure が実装を提供。
- **Service ↔ Core/Infrastructure**: DI で結合。Service は HTTP/gRPC 境界のみ担当。
- **UI ↔ Service**: HTTP（プロキシ）のみ。DB に直接接続しない。
- **Module ↔ Engine**: Module が使ってよい Engine は `Statevia.Core.Engine.Abstractions` と `DefaultStateExecutor` だけ（`docs/specifications/actions/module-zip-layout.md` §3）。リファレンス Module については Architecture.Tests で固定。

## Tools（`dotnet/tools/`）

| 配置 | 役割 |
| --- | --- |
| `dotnet/tools/capacity/` | Service API 向け容量計測ハーネス。公開件数の正本は `docs/reference/service-capacity.md`。CI 常時実行には載せない |

## Documentation & Specs

| 種類 | 場所 |
| --- | --- |
| エージェント／起動・テスト | ルート `AGENTS.md` |
| 開発の共通ルール | `docs/development-guidelines.md` |
| Spec（本 Steering を含む） | `.spec/` |
| 旧作業用メモ（参照のみ） | `.spec/archive/workspace-docs/` |

## References

- `AGENTS.md` の Architecture overview と Service API layers 表
