---
name: markdown-spec-authoring
description: 仕様系 Markdown の作成・更新手順を適用する。markdownlint 実行、Spec Workflow テンプレート準拠、Mermaid ラベル統一が必要なときに使う。
disable-model-invocation: true
---

# Markdown Spec Authoring

## 対象

- `**/*.md`
- 特に `.spec/specs/` と `.spec/steering/`

## 手順

1. 本文は日本語で記述する（テンプレート由来の英語見出しは維持可）。
2. `.markdownlint.json` 準拠で整形する。
   - 見出し前後に空行を入れる
   - リスト/コードブロック前後に空行を入れる
   - 行末不要スペースを除去する
3. `.spec/specs/` および `.spec/steering/` の更新時は、対応テンプレートの見出し構成・必須項目を維持する。
   - `requirements-template.md`
   - `design-template.md`
   - `tasks-template.md`
   - `product-template.md`
   - `tech-template.md`
   - `structure-template.md`
4. Mermaid `flowchart` のエッジラベルで可読性調整が必要な場合は、`&nbsp;&nbsp;` を左右に付けて同一図内で統一する。
5. 編集後は必ず対象ファイルに対して次を実行し、エラーがないことを確認する。
   - `npx markdownlint-cli2 "<変更したファイルパス>"`
