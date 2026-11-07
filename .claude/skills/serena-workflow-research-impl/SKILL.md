---
name: serena-workflow-research-impl
description: Serena MCP でコード調査・設計前調査・実装を進める。ユーザーが調査、設計前の確認、実装、改修、原因究明、影響範囲確認を依頼したときに適用する。
disable-model-invocation: false
---

# serena-workflow-research-impl（Claude Code 用ラッパー）

正本は `.cursor/skills/serena-workflow-research-impl/SKILL.md`。このファイルは参照用の薄いラッパーで、手順を重複記載しない。

## 手順

1. `.cursor/skills/serena-workflow-research-impl/SKILL.md` を Read で読み込む。
2. 読み込んだ手順に従って作業する。

## Claude Code での読み替え

正本は Cursor 前提のため、次のとおり読み替える。

- `serena-csharp` は `mcp__serena-csharp__*`、`serena-ui` は `mcp__serena-ui__*` を指す。
- Claude Code では両サーバーが同時に有効なため、UI トグルによる切替（`serena-mcp-project-switch`、`/serena-project-csharp`、`/serena-project-ui`）は不要。主戦場に応じて呼び先サーバーを選ぶ。
- **Claude Code の worktree では Serena で編集しない。** 常駐サーバーは本体のチェックアウトに固定されているので、編集が worktree ではなく本体に入る。読み取りの結果も本体の内容で、worktree の変更は含まない。worktree の編集は Edit、確認は Read / Grep を使う。
- 未接続・エラー時は作業を停止し、ユーザーに `claude mcp list` での接続確認を促す。`activate_project` は引き続き使わない。
