# sonar-scan-results

SonarQube のスキャン結果（Quality Gate / issue / カバレッジ）を確認し、不足を是正する。

## 前提

- 安全手順の原則は `.cursor/skills/sonarqube-mcp-ops/SKILL.md` に従う（開始時に読む）。
- MCP **`user-sonarqube`** が利用可能なこと。未接続・認可エラー時は `mcp_auth` で **USER token**（project token ではない）を確認する。
- プロジェクトキーは推測しない。既知（組織 `statevia`）: engine = **`statevia_statevia_engine`** / API = **`statevia_statevia_api`** / runtime = **`statevia_statevia_runtime`** / CLI = **`statevia_statevia_cli`** / Action Host = **`statevia_statevia_action_host`** / reference = **`statevia_statevia_reference`** / UI = **`statevia_statevia_ui`**。不明なら `search_my_sonarqube_projects` で確認する。`statevia_statevia` は取り込み時の自動解析プロジェクトであり、解析対象にしない。
- スキャン正本スクリプトと詳細手順は `docs/development-guidelines.md` §5.1（Service API）/ §5.2（UI）、`scripts/sonar/README.md`。

## 実行してほしいこと

1. SKILL を読み、`toggle_automatic_analysis` が使えれば**最初に自動解析を無効化**する。
2. 対象プロジェクトキーを確定する（不明なら `search_my_sonarqube_projects`）。ブランチ文脈があれば対象ブランチを明示する。
3. 現状把握（読み取りのみ）:
   - `get_project_quality_gate_status` で Quality Gate を取得。組み込み **Sonar way**（新規コードの信頼性・セキュリティ・保守性が A、Security Hotspot のレビュー 100%、カバレッジ 80% 以上、重複行 3% 以下）。
   - `search_sonar_issues_in_projects` で issue を取得（type = Bug / Vulnerability / Code Smell、Security Hotspot、severity、**new code を優先**）。
   - `get_component_measures` で `coverage` / `new_coverage` / `duplicated_lines_density` 等の不足を確認する。
   - 必要に応じ `show_rule` でルールの趣旨・修正方針を確認する。
4. トリアージ: **新規コード違反と Quality Gate を最優先**。重大度と誤検知の可能性で分類し、対応方針（修正 / テスト追加 / 抑制の妥当性）を簡潔に提示する。
5. 是正:
   - コードは規約（`.cursor/rules/csharp-standards.mdc` / `.cursor/rules/typescript-standards.mdc`）に従って修正する。
   - カバレッジ不足は対象範囲にテストを追加する（AAA・1ケース1シナリオ）。
   - issue ステータス変更（False Positive / Won't Fix 等）は**ユーザー合意がある場合のみ** `change_sonar_issue_status` を使い、理由を残す。
6. ローカル検証（再スキャン前に green を確認）:
   - Service API: `cd dotnet/service/api && dotnet build statevia-api.sln`（Analyzer 警告方針は §4.3）+ `dotnet test`。カバレッジは `XPlat Code Coverage` / `dotnet-coverage`。
   - UI: `cd nodejs/studio && npm run lint && npm run typecheck && npm run test:run`（Sonar 前は `npm run test:coverage`）。
7. 再スキャン（**正本スクリプト**、リポジトリルートから。`SONAR_TOKEN` 設定後）:
   - Service API: `./scripts/sonar/sonar-scanner-api.ps1`
   - UI: `./scripts/sonar/sonar-scanner-ui.ps1`
   - **修正直後の即時再検証はしない**（反映遅延）。Quality Gate 判定は Sonar のプロジェクト画面を正とする。スニペット解析（`analyze_code_snippet`）はフルスキャンの代替にしない。
8. 終了時も **Automatic Analysis は無効のまま**にする。有効に戻すと CI 解析と二重になる。
9. 対応内容（是正した issue / 残課題 / Quality Gate 結果）を簡潔に報告する。

## ユーザー向け短文（チャットへの貼り付け用）

SonarQube のスキャン結果を確認し、新規コードの Quality Gate と issue・カバレッジ不足を是正してください。手順は `.cursor/skills/sonarqube-mcp-ops/SKILL.md` と `docs/development-guidelines.md` §5.1 / §5.2 に従ってください。
