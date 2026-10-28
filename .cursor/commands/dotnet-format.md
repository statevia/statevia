# dotnet-format

全 .NET ソリューションに `dotnet format` を適用し、EditorConfig に基づくコードフォーマットを統一する。

## 前提

- .NET SDK（`dotnet format` サブコマンド利用可能）がインストール済みであること。
- リポジトリルートの `.editorconfig` が書式ルールの正本。

## 実行してほしいこと

1. **フォーマット適用**（指定がなければ全ソリューション）:

   ```powershell
   .\sonar\dotnet-format-all.ps1
   ```

2. **特定ソリューションのみ**（ユーザーから指示があれば）:

   ```powershell
   .\sonar\dotnet-format-all.ps1 -Solution <name>
   ```

   `<name>` は `engine` / `api` / `action-host` / `cli` / `infrastructure`、
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
| `-Solution` | 対象 sln（`engine` / `api` / `action-host` / `cli` / `infrastructure`） |
| `-VerifyNoChanges` | 書式変更が発生しないことを検証する |
| `-Verbosity` | `quiet` / `minimal` / `normal` / `detailed` / `diagnostic` |

## 対象ソリューション

- `core/engine/statevia-engine.sln`
- `service/api/statevia-api.sln`
- `service/action-host/statevia-action-host.sln`
- `service/cli/statevia-cli.sln`
- `infrastructure/statevia-infrastructure.sln`

## ユーザー向け短文（チャットへの貼り付け用）

全ソリューションに `dotnet format` を適用し、EditorConfig に基づくフォーマットを統一してください。
