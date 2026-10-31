---
name: serena-mcp-project-switch
description: statevia の Serena プロジェクト（csharp / ui）ごとに独立した MCP サーバーを、Cursor UI トグルで On/Off して切り替える。質問・設計・実装の主戦場が決まったときに使う。
disable-model-invocation: true
---

# Serena MCP プロジェクト切り替え（UI トグル）

## 目的

statevia の Serena は **2 つ**。それぞれに **独立した MCP サーバー** を `mcp.json` へ静的定義する。切替は **`mcp.json` 編集でも `activate_project` でもなく**、Cursor Customize の **トグル（On/Off）のみ**。

同時に On にする Serena は **原則 1 つ**（複数 On は非推奨）。

- C# は **`serena-csharp`**（`dotnet/` / `statevia.sln`）
- UI は **`serena-ui`**（`nodejs/studio/`）

## いつ読むか

次のときに、このスキルの手順で対象サーバーの起動を促す。

- C#（プロダクトコード。複数コンポーネントを含む）→ **`serena-csharp`**
- UI（`nodejs/studio/`）中心の調査や変更 → **`serena-ui`**

横断変更でも、まず主戦場 1 つのサーバーだけを On にする。

## サーバーの選び方（エージェント）

1. **UI 作業** → `user-serena-ui`。
2. **C# 作業** → `user-serena-csharp`。
3. 対象が未接続 → **作業を停止**し、主戦場の `serena-csharp` または `serena-ui` を On、もう一方を Off にするよう促す。
4. ユーザーが `/serena-project-csharp` または `/serena-project-ui` を明示したときは、そのサーバーを使う。

## Cursor コマンド（明示指定）

- `/serena-project-csharp` → `.cursor/commands/serena-project-csharp.md`
- `/serena-project-ui` → `.cursor/commands/serena-project-ui.md`

## プロジェクト ↔ MCP 対応表（正本）

| Serena プロジェクト | mcp.json 名 | Cursor 上の識別子（目安） | パス（リポジトリ相対） | 用途 |
| --- | --- | --- | --- | --- |
| csharp | `serena-csharp` | `user-serena-csharp` | `dotnet/`（`statevia.sln`） | C# |
| ui | `serena-ui` | `user-serena-ui` | `nodejs/studio/` | UI |

Windows 絶対パス例（`mcp.json` の `--project`）:

| キー | `--project` |
| --- | --- |
| csharp | `C:\Git\statevia\dotnet` |
| ui | `C:\Git\statevia\nodejs\studio` |

`serena-csharp` の索引対象と除外は `dotnet/.serena/project.yml`（`ls_workspace_folders` / `ignored_paths`）が正本。`dotnet/` の中でも、プロダクト C# 以外（サンプル、Fixture、生成物等）は除外する。

## 手順（エージェント）

1. **主戦場プロジェクトを決める**（質問のみでも可）。未確定ならユーザーに確認する。C# なら `serena-csharp`、UI なら `serena-ui`。
2. 対応する **`user-serena-*` が ready か**を確認する（`GetMcpTools` 等）。
3. **ready でないとき**
   - 実装・調査を **一旦停止**する。
   - ユーザーへ次を促す（日本語で簡潔に）:
     1. Cursor の Customize / MCP 設定を開く
     2. もう一方の `serena-*` を **Off**
     3. 対象の `serena-*` を **On**
   - エージェントから Cursor MCP を自動 On/Off する公式手段は **ない**。トグルはユーザー操作必須。
4. ユーザーがトグル完了を報告したら、再度 ready を確認してから Serena ツールを使う。
5. ユーザーが「Serena なしで続行」と明示したときだけ、Read / Grep 等へフォールバックする。

**禁止:** セッション中の `activate_project` によるプロジェクト切替。切替のたびに `mcp.json` の `--project` を書き換えない。

## 手順（開発者・初回セットアップ）

1. `~/.cursor/mcp.json` に上表の 2 エントリ（`serena-csharp` と `serena-ui`）を定義する。
2. Cursor を開いた直後は、**使わない方を Off** にし、使う 1 つだけ On にする（新規が既定 On の場合への対策）。
3. 以降の切替はトグルのみ。

## 関連

- 調査・実装時の優先利用: `.cursor/skills/serena-workflow-research-impl/SKILL.md`
- 旧 10 サーバー構成の仕様（参照のみ）: `.spec/archive/specs/serena-mcp-project-toggle/`

## メンテナンス

リポジトリ配置やサーバー命名が変わったら、上表と `mcp.json` 例を更新する。
