---
name: workspace-docs-ops
description: 旧 .workspace-docs アーカイブの参照ルール。新規編集は禁止。配置・命名の履歴確認が必要なときに使う。
disable-model-invocation: true
---

# Workspace Docs Operations（アーカイブ）

## 対象

- `.spec/archive/workspace-docs/**/*.md`（旧 `.workspace-docs/`）

## 参照元（履歴）

- `.spec/archive/workspace-docs/20_discussion/docs-format-unification.md`

## ルール

1. **新規ファイル作成・仕様の更新はしない。** 作業用仕様の正本は `.spec/specs/`。
2. 参照が必要なときだけアーカイブを読む。パスはすべて `.spec/archive/workspace-docs/` 配下。
3. 履歴上のカテゴリ配置（変更しない）:
   - `00_inbox/` 一時置き
   - `10_notes/` 思想・メモ・調査
   - `20_discussion/` 合意前議論
   - `30_specs/` 仕様
   - `40_plans/` 計画・方針
   - `50_tasks/` タスク（`backlog` もここ）
4. 状態サブフォルダは `10_in-progress/` → `20_done/` → `30_archived/`（メタに Status を書かない方針の履歴）。
5. アーカイブ内容を `docs/` へ昇格する依頼があった場合のみ、該当文書を `docs/` に写し、アーカイブ側は触らないか注記に留める。
