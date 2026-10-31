# SonarQube（ローカル分析）

このディレクトリ（`scripts/sonar/`）には、**コンポーネント別スキャナ（PowerShell）** と共通処理がまとまっています。ローカル SonarQube 用の Docker Compose は `compose/sonar.yml`、行数集計スクリプトは `scripts/measure-loc.ps1` です。

C# 分析時のカバレッジ XML（`service-*-coverage.xml` / `core-engine-coverage.xml` 等）は **生成物**として `artifacts/sonar/` に出力します（`artifacts/` は `.gitignore` で除外済み）。

## 前提条件

- **Docker**（`docker compose` が使えること）
- **.NET 10 SDK** と次のツール
  - `dotnet sonarscanner`（例: `dotnet tool install --global dotnet-sonarscanner`）
  - `dotnet-coverage`（PATH で実行できること）
- **UI 分析**: Node.js / npm、`nodejs/studio` で `npm install` 済みであること
- 分析実行前に **`SONAR_TOKEN`** を環境変数で設定すること（SonarQube のユーザートークンまたはプロジェクトトークン）

## SonarQube サーバの起動（ローカル）

`compose/sonar.yml` は **Community Build**（`sonarqube:community`）と **PostgreSQL 17** をポート **9000** で起動します。SonarQube Community Build 26.8 以降は PostgreSQL 15〜18 が必須です。

```bash
docker compose -f compose/sonar.yml up -d
```

`compose/sonar.yml` の先頭の `name: sonar` で Compose のプロジェクト名を固定しているため、どのディレクトリから起動しても手元の volume（`sonar_postgresql` / `sonar_sonarqube_data` など）を使います。

ブラウザで `http://localhost:9000` にアクセスする。CI と通常の解析の送信先は SonarQube Cloud であり、このコンテナはローカル確認用である。ローカルへ送るときは `SONAR_HOST_URL=http://localhost:9000` を指定する（組織キーは付けない）。

## 分析の実行（推奨: PowerShell スクリプト）

`SONAR_TOKEN` を設定したうえで、**リポジトリのどのカレントディレクトリからでも**実行できます（各スクリプトが `scripts/sonar/` の位置から `dotnet/core/engine` / `dotnet/service/api` / `nodejs/studio` 等を解決します）。

**一括実行**（未指定時は全プロジェクト。複数指定可）:

```powershell
$env:SONAR_TOKEN = "（SonarQube のトークン）"
& .\scripts\sonar\sonar-scanner-all.ps1
& .\scripts\sonar\sonar-scanner-all.ps1 -Projects engine,api,ui
& .\scripts\sonar\sonar-scanner-all.ps1 engine api
& .\scripts\sonar\sonar-scanner-all.ps1 -List
```

`-Projects` には短い名前（`engine` / `api` / `runtime` / `cli` / `action-host` / `reference` / `ui`）または SonarQube の projectKey を指定できます。先頭で `dotnet build-server shutdown` を実行します（省略は `-SkipBuildServerShutdown`）。失敗したプロジェクトは最後にまとめて報告します。

**個別実行**（1 コンポーネントだけ送るとき）:

```powershell
$env:SONAR_TOKEN = "（SonarQube のトークン）"
dotnet build-server shutdown
& .\scripts\sonar\sonar-scanner-engine.ps1
& .\scripts\sonar\sonar-scanner-api.ps1
```

- **engine / api / runtime / cli / action-host / reference**: `dotnet sonarscanner begin` → `build` → `dotnet-coverage` → `end` の順で、`artifacts/sonar/*-coverage.xml` にカバレッジを出力します（XML は git 管理外）。
- **ui**: `npm run test:coverage` で `nodejs/studio/coverage/lcov.info` を生成したあと、`npx sonar-scanner` で `nodejs/studio/sonar-project.properties` を読み込んで送信します。

C# スキャナは `sonar.projectBaseDir` を `dotnet/` に固定し、ファイルのキー（`core/engine/...` 等）を集約前と同じに保っています。**テストプロジェクト**（`*.Tests`）は `sonar.dotnet.excludeTestProjects=true` で各コンポーネントの projectKey から除外します（品質ゲートの対象はプロダクションコード）。IDE も同じ範囲に揃えるため、`IsTestProject=true` の csproj は `dotnet/Directory.Build.targets` で `SonarQubeExclude=true` です。UI Studio は `tests/` を `sonar.exclusions` で解析対象外にし、カバレッジは lcov のプロダクションパスだけを使います（C# の `excludeTestProjects` と同趣旨）。Connected Mode の除外は各プロジェクトの **Project Settings → Analysis Scope → Source File Exclusions**（Scanner の `/d:sonar.exclusions` は IDE に残らない）。スタンドアロン時は `.vscode/settings.json` の `sonarlint.analysisExcludesStandalone` を使います。`Statevia.Runtime` は **`statevia_statevia_runtime`** で解析し、API スキャナからは `dotnet/service/runtime` を除外して二重計上を避けます。first-party リファレンス Module は **`statevia_statevia_reference`** で解析し、API スキャナからは `dotnet/modules/reference` を除外します。Studio（`nodejs/studio`）は **`statevia_statevia_ui`** だけで解析します。C# 用スクリプトはすべて `sonar.scanner.scanAll=false` にし、`sonar.exclusions` だけでは止まらない JS センサの拾い上げを防ぎます。

### 送信先

既定は **SonarQube Cloud**（`https://sonarcloud.io`、組織 **`statevia`**）です。接続パラメータは `SonarScanner.Common.ps1` にまとめてあります。`SONAR_TOKEN` は Cloud の個人アクセストークンです。GitHub Actions ではリポジトリシークレット `SONAR_TOKEN` を使います（`.github/workflows/sonar.yml`）。

`main` への push は 7 プロジェクトをすべて解析し、Quality Gate の完了は待ちません（初回解析をベースラインにするため）。`main` 向け pull request は変更のあったコンポーネントだけ解析し、`SONAR_QUALITYGATE_WAIT=true` でゲート失敗をワークフロー失敗にします。ゲートは組み込みの **Sonar way**（新規コードの信頼性・セキュリティ・保守性が A、Security Hotspot をすべてレビュー、カバレッジ 80% 以上、重複行 3% 以下）です。

ローカル Community Build へ送る場合:

```powershell
$env:SONAR_HOST_URL = "http://localhost:9000"
$env:SONAR_TOKEN = "（ローカル SonarQube のトークン）"
```

### プロジェクトキー一覧

| コンポーネント | projectKey |
| -------------- | ---------- |
| Core Engine | `statevia_statevia_engine` |
| Service API | `statevia_statevia_api` |
| Runtime | `statevia_statevia_runtime` |
| CLI | `statevia_statevia_cli` |
| Action Host | `statevia_statevia_action_host` |
| Reference Modules | `statevia_statevia_reference` |
| UI Studio | `statevia_statevia_ui` |

## 手動実行（リポジトリルートをカレントに）

スクリプトを使わず同じ処理を手で行う場合の例です。

### Core Engine

```powershell
$env:SONAR_TOKEN = "（トークン）"
$repoRoot = (Get-Location).Path
Set-Location dotnet\core\engine
dotnet sonarscanner begin /k:"statevia_statevia_engine" /o:"statevia" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.token="$($env:SONAR_TOKEN)" /d:sonar.projectBaseDir="$repoRoot\dotnet" /d:sonar.scanner.scanAll=false /d:sonar.cs.vscoveragexml.reportsPaths="$repoRoot\artifacts\sonar\core-engine-coverage.xml"
dotnet build "statevia-engine.sln"
dotnet-coverage collect "dotnet test" -f xml -o "$repoRoot\artifacts\sonar\core-engine-coverage.xml"
dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
Set-Location $repoRoot
```

### Service API

```powershell
$env:SONAR_TOKEN = "（トークン）"
$repoRoot = (Get-Location).Path
Set-Location dotnet\service\api
dotnet sonarscanner begin /k:"statevia_statevia_api" /o:"statevia" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.token="$($env:SONAR_TOKEN)" /d:sonar.projectBaseDir="$repoRoot\dotnet" /d:sonar.scanner.scanAll=false /d:sonar.cs.vscoveragexml.reportsPaths="$repoRoot\artifacts\sonar\service-api-coverage.xml"
dotnet build "statevia-api.sln"
dotnet-coverage collect "dotnet test" -f xml -o "$repoRoot\artifacts\sonar\service-api-coverage.xml"
dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
Set-Location $repoRoot
```

Scanner for .NET の `sonar.scanner.scanAll`（既定 true）は、`sonar.projectBaseDir`（`dotnet/`）の中の MSBuild のプロジェクト外のファイル（Dockerfile や設定）も解析に混ぜる。C# 用スクリプトと手動手順はすべて `sonar.scanner.scanAll=false` にする。UI は `statevia_statevia_ui` で別スキャンする。

### Runtime

```powershell
$env:SONAR_TOKEN = "（トークン）"
& .\scripts\sonar\sonar-scanner-runtime.ps1
```

### CLI

```powershell
$env:SONAR_TOKEN = "（トークン）"
$repoRoot = (Get-Location).Path
Set-Location dotnet\service\cli
dotnet sonarscanner begin /k:"statevia_statevia_cli" /o:"statevia" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.token="$($env:SONAR_TOKEN)" /d:sonar.projectBaseDir="$repoRoot\dotnet" /d:sonar.scanner.scanAll=false /d:sonar.cs.vscoveragexml.reportsPaths="$repoRoot\artifacts\sonar\service-cli-coverage.xml"
dotnet build "statevia-cli.sln"
dotnet-coverage collect "dotnet test" -f xml -o "$repoRoot\artifacts\sonar\service-cli-coverage.xml"
dotnet sonarscanner end /d:sonar.token="$($env:SONAR_TOKEN)"
Set-Location $repoRoot
```

### Reference Modules

```powershell
$env:SONAR_TOKEN = "（トークン）"
& .\scripts\sonar\sonar-scanner-reference.ps1
```

Scanner for .NET の `sonar.scanner.scanAll`（既定 true）は、MSBuild のプロジェクト外のファイルも拾ってしまう。リファレンス用スクリプトも他の C# スキャナと同様に `sonar.scanner.scanAll=false` にする。

`HttpRequestPublication` / `NotificationSendPublication` の JSON スキーマは Module ごとに独立して持つ定型のため、`sonar.cpd.exclusions`（`*Publication.cs`）で重複検出だけ除外する。カバレッジと issue は対象のまま。

### Service UI（手動）

```powershell
$env:SONAR_TOKEN = "（トークン）"
Set-Location nodejs\studio
npm run test:coverage
npx --yes sonar-scanner "-Dsonar.token=$($env:SONAR_TOKEN)" "-Dsonar.projectKey=statevia_statevia_ui" "-Dsonar.organization=statevia" "-Dsonar.host.url=https://sonarcloud.io"
Set-Location ..\..
```

## 行数集計（`scripts/measure-loc.ps1`）

現行リポジトリ構成（`dotnet/core/` / `dotnet/infrastructure/` / `dotnet/service/` / `dotnet/modules/reference/` / `nodejs/studio` / `dotnet/tests/`）のソース行数を、**プロダクト**と**テスト**に分けて集計します。SonarQube の分析とは独立しており、**`SONAR_TOKEN` は不要**です。

```powershell
.\scripts\measure-loc.ps1
.\scripts\measure-loc.ps1 -Detailed
.\scripts\measure-loc.ps1 -Json | Set-Content loc-report.json
```

### 集計対象

| コンポーネント | プロダクト | テスト |
| -------------- | ---------- | ------ |
| engine | `Statevia.Core.Engine` | `Statevia.Core.Engine.Tests` |
| application | `Statevia.Core.Application`, `Statevia.Core.Application.Contracts`, `Statevia.Core.Actions.Abstractions` | （なし） |
| infrastructure | `Statevia.Infrastructure.*`（Common / Persistence / Security / Modules / Notification / Actions.Grpc） | `*.Modules.Tests`, `*.Actions.Grpc.Tests` |
| api | `Statevia.Service.Api`, `Statevia.Service.Api.Bootstrap` | `Statevia.Service.Api.Tests` |
| runtime | `Statevia.Runtime`, `Statevia.Service.Runtime.Scheduler`, `Statevia.Service.Runtime.Worker` | （なし。HostedService テストは Api.Tests） |
| cli | `Statevia.Service.Cli` | `Statevia.Service.Cli.Tests` |
| action-host | `Statevia.Service.ActionHost` | `Statevia.Service.ActionHost.Tests` |
| reference | `Statevia.Reference.Http`, `Statevia.Reference.Notification` | `Statevia.Reference.Http.Tests`, `Statevia.Reference.Notification.Tests` |
| ui | `nodejs/studio`（`tests/`・`e2e/`・`*.test.*` を除く `*.ts` / `*.tsx`） | `tests/`、`*.test.*`、`*.spec.*`、`e2e/` |
| architecture | （なし） | `Statevia.Architecture.Tests` |

除外: `bin/`, `obj/`, `node_modules/`, `.next/`, `coverage/`。`dotnet/core/engine/samples/` と `dotnet/tests/Fixtures/`（テスト用 Module フィクスチャ）は対象プロジェクト定義に含めません。Migrations は `dotnet/infrastructure/Statevia.Infrastructure.Persistence/Migrations` を内訳表示します。

## 主なファイル

| ファイル | 説明 |
| -------- | ---- |
| `compose/sonar.yml` | ローカル SonarQube + PostgreSQL（通常の送信先は SonarQube Cloud） |
| `SonarScanner.Common.ps1` | 送信先・組織・Quality Gate 待ちの共通引数、リポジトリルートと出力先（`artifacts/sonar/`）の解決 |
| `select-sonar-projects.ps1` | CI で解析するプロジェクトを変更パスから選ぶ |
| `sonar-scanner-all.ps1` | 全プロジェクト、または `-Projects` で指定した複数プロジェクトを順に分析 |
| `sonar-scanner-engine.ps1` | Engine 向け一括分析 |
| `sonar-scanner-api.ps1` | API 向け一括分析 |
| `sonar-scanner-runtime.ps1` | Runtime（HostedService / Scheduler / Worker）向け一括分析 |
| `sonar-scanner-cli.ps1` | CLI 向け一括分析 |
| `sonar-scanner-action-host.ps1` | Action Host 向け一括分析 |
| `sonar-scanner-reference.ps1` | リファレンス Module（Http / Notification）向け一括分析 |
| `sonar-scanner-ui.ps1` | UI 向け一括分析 |
| `scripts/measure-loc.ps1` | 行数集計（プロダクト・テスト別） |
| `artifacts/sonar/*-coverage.xml` | C# カバレッジ（**生成物・git 管理外**） |

## 関連ドキュメント

- 開発ガイド Sonar 節: `docs/development-guidelines.md` §5
- SonarQube MCP: `.cursor/skills/sonarqube-mcp-ops/SKILL.md`
