# claude-agent

Claude MCP（**Cursor の MCP 一覧に表示される `claude-code` 系サーバー名**）の **`Agent`** を使い、**サブエージェントに複数段の調査・作業を委任する**。

サーバー識別子は環境で変わる。例: プロジェクトの `.cursor/mcp.json` のみ定義している場合は `project-0-statevia-claude-code`、ユーザースコープで有効化している場合は `user-claude-code` など。**都度、MCP 一覧の表記に合わせること**。

## 前提

- Cursor で **Claude MCP**（上記のとおり一覧で確認したサーバー名）が有効で、ユーザーが実行した **`/claude-agent`** と同一チャット内に、エージェントへ渡すべきタスクがあること。
- **`description`** と **`prompt`** は MCP 側で **必須**。タスクが曖昧なら、実行前にユーザーへ短く確認する。

## 実行してほしいこと

1. ユーザーの意図を **`description`**（短い要約、目安 3〜5 語）と **`prompt`**（**会話を知らない第三者向け**に、目的・背景・成果物の形を自己完結で書く）に整理する。コード変更を期待するか調査のみか、明示する。
2. **`call_mcp_tool`** で次を呼び出す。
   - **server**: MCP 一覧の `claude-code` 系識別子（例: `project-0-statevia-claude-code`）
   - **toolName**: `Agent`
   - **arguments**: 少なくとも `description` と `prompt`。必要に応じて下記の任意項目を付ける。

### 任意の引数（必要なときだけ）

| 引数 | 用途の目安 |
|------|------------|
| `subagent_type` | 専用エージェント種別がある場合（例: `code-reviewer`）。利用可能な型は MCP 側定義に従う。 |
| `model` | `sonnet` / `opus` / `haiku` のいずれか。 |
| `run_in_background` | 並列で独立作業するとき `true`。完了通知を待てばよい。 |
| `name` | 後から `SendMessage` で指名する場合の名前。 |
| `mode` | 権限モード（例: `plan`）。 |
| `isolation` | 分離が必要なら `worktree`（一時 git worktree）。 |

3. ツールの戻り値を踏まえ、**ユーザー向けに要約**する。Claude の Agent は **結果がチャットにそのまま見えない場合**があるため、要点・結論・次のアクションをこちらで伝える。
4. エージェントがファイル変更を行った可能性がある場合は、**差分や該当ファイルを確認してから**「完了」と報告する（盲信しない）。

## ユーザー向け短文（チャットへの貼り付け用）

Claude MCP の `Agent` で、このタスクをサブエージェントに任せ、結果を要約して返してください。
