---
name: sonarqube-mcp-ops
description: SonarQube MCP での分析作業手順を実行する。SonarQube の issue 調査、プロジェクトキー確認、自動解析の on/off 管理が必要なときに使う。
disable-model-invocation: true
---

# SonarQube MCP Operations

## 目的

SonarQube MCP を使う作業で、毎回同じ安全手順を実行する。

## 手順

1. 作業開始時に `toggle_automatic_analysis` ツールが利用可能なら、自動解析が有効になっていないことを確認し、有効なら無効化する。解析の正本は CI（`.github/workflows/sonar.yml`）である。
2. プロジェクトキー指定が必要な場合は、推測せず `search_my_sonarqube_projects` で正しいキーを確認する。組織は `statevia`。コンポーネントのキーは `scripts/sonar/README.md` の一覧（`statevia_statevia_engine` など）。`statevia_statevia` は解析対象にしない。
3. ブランチ作業の文脈がある場合は、対象ブランチを明示して実行する。
4. コード修正後、`search_sonar_issues_in_projects` で即時再検証はしない（反映遅延のため）。
5. 作業終了時も自動解析は無効のままにする。有効に戻すと CI 解析と二重になる。

## Troubleshooting

- `Not authorized` の場合は USER token を確認する（project token ではない）。
- `Project not found` の場合はキーを再検索し、スペルを確認する。
- スニペット解析結果はフルスキャンの代替ではないことを明示する。
