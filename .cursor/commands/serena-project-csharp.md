# serena-project-csharp

Serena MCP で **statevia の C# 横断**（`dotnet/statevia.sln`）を使う（MCP サーバー `serena-csharp`）。UI は `serena-ui`。

## 実行してほしいこと

1. 主戦場を **csharp**（`dotnet/`、プロダクト C#）とみなす。
2. MCP サーバー **`user-serena-csharp`** が ready か確認する。
3. ready でない場合は **作業を停止**し、ユーザーへ次を促す（自動トグルは不可）。
   - Cursor Customize / MCP で、他の `serena-*` をすべて **Off**
   - `serena-csharp` を **On**
   - 新規追加直後は Cursor が既定 On にすることがある。他 `serena-*` が On なら Off にする
4. ユーザーが完了を報告したら、再度 ready を確認する。
5. ready なら、以降のシンボル探索・編集は **`user-serena-csharp`** のツールを使う。
6. `activate_project` は呼び出さない。`mcp.json` も編集しない。
7. 結果（促した内容 / ready 確認）をユーザーに短く報告する。

詳細は `.cursor/skills/serena-mcp-project-switch/SKILL.md` を参照できる。
