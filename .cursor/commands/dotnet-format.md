# dotnet-format

リポジトリ内の .NET コードに `dotnet format` を適用し、EditorConfig に基づくコードフォーマットを統一する。

## 前提

- .NET SDK（`dotnet format` サブコマンド利用可能）がインストール済みであること。
- リポジトリルートの `.editorconfig` が書式ルールの正本。

## 実行してほしいこと

1. **フォーマット適用**（指定がなければリポジトリ全体）:

   ```powershell
   .\sonar\dotnet-format-all.ps1
   ```

2. **特定ソリューションのみ**（ユーザーから指示があれば）:

   ```powershell
   .\sonar\dotnet-format-all.ps1 -Solution <name>
   ```

   `<name>` は `engine` / `api` / `action-host` / `cli` / `infrastructure` / `reference`、
   または `statevia-api.sln` 等のファイル名。

3. **CI チェックモード**（変更がないことを検証、差分があれば exit 1）:

   ```powershell
   .\sonar\dotnet-format-all.ps1 -VerifyNoChanges
   ```

4. フォーマット後、変更があればユーザーへ要約を報告する。
5. 明示的に依頼された場合を除き、フォーマット結果のコミットは行わない。

## パラメータ

| パラメータ | 説明 |
|-----------|------|
| `-Solution` | 対象 sln（`engine` / `api` / `action-host` / `cli` / `infrastructure` / `reference`） |
| `-VerifyNoChanges` | 書式変更が発生しないことを検証する |
| `-Verbosity` | `quiet` / `minimal` / `normal` / `detailed` / `diagnostic` |

## 対象

`-Solution` 未指定時（CI もこのモード）は、リポジトリ全体を一括で処理する。

- whitespace: `dotnet format whitespace --folder` でリポジトリルートの `.cs` をまとめて処理する。
  gitignore 対象の `.claude/`、`modules/local/`、`bin` / `obj` は除外する。
- using の整理: IDE 用 `statevia.sln` と、`statevia.sln` に含まれない
  `Statevia.Service.Api.Scenario.Tests.csproj` を処理する。

コンポーネント別 sln を順に処理すると共通プロジェクトの restore と読み込みが繰り返されて遅いため、一括で処理する。

`-Solution` 指定時は、次のいずれか 1 つの sln だけを処理する。

- `core/engine/statevia-engine.sln`
- `service/api/statevia-api.sln`
- `service/action-host/statevia-action-host.sln`
- `service/cli/statevia-cli.sln`
- `infrastructure/statevia-infrastructure.sln`
- `modules/reference/statevia-reference.sln`

## ユーザー向け短文（チャットへの貼り付け用）

リポジトリ内の .NET コードに `dotnet format` を適用し、EditorConfig に基づくフォーマットを統一してください。
