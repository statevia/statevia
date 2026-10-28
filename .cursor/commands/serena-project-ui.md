# serena-project-ui

Serena MCP で **statevia の UI（studio）** を使う（MCP サーバー `serena-ui`）。

## 実行してほしいこと

1. 主戦場を **ui**（`ui/studio/`）とみなす。
2. MCP サーバー **`user-serena-ui`** が ready か確認する。
3. ready でない場合は **作業を停止**し、ユーザーへ次を促す（自動トグルは不可）。
   - Cursor Customize / MCP で、他の `serena-*` をすべて **Off**
   - `serena-ui` を **On**
4. ユーザーが完了を報告したら、再度 ready を確認する。
5. ready なら、以降のシンボル探索・編集は **`user-serena-ui`** のツールを使う。
6. `activate_project` は呼び出さない。`mcp.json` も編集しない。
7. 結果（促した内容 / ready 確認）をユーザーに短く報告する。

詳細は `.cursor/skills/serena-mcp-project-switch/SKILL.md` を参照できる。
