# ドキュメント執筆標準

| 項目 | 値 |
| --- | --- |
| 種別 | 参照（執筆者向け） |
| Version | 1.4 |
| 更新日 | 2026-11-19 |

---

本文書は **Markdown の書き方** の正本である。知識体系・索引・学習導線は [`README.md`](README.md) を参照すること。

**Version 1.4（2026-11-19）**: CI の検査と除外一覧の書き方、種別ごとの構成、画像の決まりを足す。

**Version 1.3（2026-11-17）**: メタブロックを CI で検査する。Version は `x.y` か `x.y.z`、必須の項目は種別・Version・更新日とする。

**Version 1.2（2026-09-01）**: 第一読者を利用者・ライブラリ利用者と明示する。`AGENTS.md` を正本にしない。samples 導線を書く。

**第一読者**は API / UI / 運用の利用者と Engine ライブラリ利用者である。Cursor エージェントとコントリビュータは第一読者ではない。`AGENTS.md` はエージェント向け索引であり、利用者向け本文の正本にしない。

コントリビュータ文書（[`development-guidelines.md`](development-guidelines.md)、[`reference/logging-property-keys.md`](reference/logging-property-keys.md)、[`architecture/ui-studio-structure.md`](architecture/ui-studio-structure.md)、[`architecture/domain-model-boundaries.md`](architecture/domain-model-boundaries.md)）は `docs/` に残してよい。Quick Start の必須ステップにしない。冒頭で読者を明示する。

## メタブロック

`docs/` 配下の各 Markdown（本書・`README.md` を除く）のタイトル直後に次を置く。

```markdown
| 項目 | 値 |
| --- | --- |
| 種別 | Guide / Concept / Architecture / Reference / Specification / Decision / Future |
| Version | x.y |
| 更新日 | YYYY-MM-DD |
| 関連 | [他 doc へのリンク] |
```

- **必須の項目**は種別・Version・更新日。関連は任意。Decision の `ステータス` のように、種別に固有の項目を足してよい
- **Version** は `x.y` か `x.y.z`（数字だけ）。ファイルの中ではどちらかに揃える
- **更新日**は暦に存在する日付を `YYYY-MM-DD` で書く
- **変更履歴**: 重要な変更は `**Version x.y（日付）**: 概要` を最大 5 件まで。最初の履歴の版はメタブロックの Version と一致させる
- 上の決まりは CI の `Docs meta check`（[`scripts/docs/check-doc-meta.mjs`](../scripts/docs/check-doc-meta.mjs)）が検査する。関連の有無と本文の書き方は検査しない
- **種別**の定義一覧は [`README.md`](README.md) に委譲する

## 配置ルール

- **新規 doc は `docs/` ルート直下に作らない。** 種別に対応するサブフォルダへ置く
- ルートに置いてよいのは `README.md`、`DOCUMENTATION-STANDARD.md`、`development-guidelines.md` のみ

## 命名

- ファイル名は **kebab-case**、拡張子 `.md`
- 意味が伝わる短い名（例: `getting-started.md`, `execution-model.md`）

## 規範表現（Specification）

契約・仕様（**Specification**）では RFC 2119 風に次を用いる。

| 表現 | 意味 |
| --- | --- |
| **必須** | 実装・利用者が満たさなければならない |
| **推奨** | 強く望ましいが例外があり得る |
| **任意** | 省略可能 |
| **禁止** | してはならない |

Guide / Concept / Architecture / Reference / Decision / Future では MUST/SHOULD 等の規範キーワードを使わない。

## 叙述と契約の分離

- 背景・思想・「なぜ」→ **Concept**
- 手順 → **Guide**
- 一覧・辞書 → **Reference**
- 振る舞い・契約 → **Specification**
- 設計判断の理由 → **Decision（ADR）**

仕様 PR で長い経緯節を増やさない。必要なら Concept または ADR へ書く。

## 種別ごとの構成

新しい文書は種別に合わせて次の順に節を立てる。当てはまらない節は省いてよいが、順は入れ替えない。

| 種別 | 節の順 | 例 |
| --- | --- | --- |
| Guide | 前提 → 手順 → 確認 → 次に読むもの | [`guides/first-workflow-tutorial.md`](guides/first-workflow-tutorial.md) |
| Reference | 一覧の正本（実装の場所）→ 表 | [`reference/error-codes.md`](reference/error-codes.md) |
| Specification | 範囲 → 必須要件 → エラー | [`specifications/api-http.md`](specifications/api-http.md) |

- **Guide** の手順は、上から順に実行すれば動く形で書く。確認の節には、成功したと分かる応答や画面を書く
- 症状から引く Guide（[`guides/troubleshooting.md`](guides/troubleshooting.md)）は、節ごとに「症状 | 原因 | 対処」の表にする
- **Reference** の表が実装の一覧と対になるときは、冒頭に実装の正本（型やファイル）を書く。照合の対象の表は、列の見出しを変えない（[CI の検査](#ci-の検査)）
- Concept・Architecture・Future は構成を決めない。Decision は [`decisions/README.md`](decisions/README.md) の ADR テンプレート（Context → Decision → Consequences）に従う

## Markdown

- 本文は日本語（テンプレ由来の英語見出しは維持可）
- [`.markdownlint.json`](../.markdownlint.json) に準拠する
- 見出し・リスト・コードブロックの前後に空行を入れる
- **Mermaid**（`flowchart`）: エッジラベルの可読性調整が必要なときは、ラベル文字列の左右に `&nbsp;&nbsp;` を付け、同一図内で統一する

## 画像

- **画面の画像**は `docs/images/screenshots/<画面の ID>.webp` に置く。幅 1280px、1 枚 200KB 以下にする
- 画面の画像は Studio のモック E2E で生成し、手で撮らない。全サービスを起動せずに、同じデータ・同じ時刻で作り直せるようにするため
- 写すのは**架空のデータだけ**にする。資格情報・トークン・実在の個人情報・社内のホスト名を写さない
- すべての画像に、何が写っているかを示す**代替テキスト**を付ける（`![実行グラフ。Fork から 2 つの分岐が並ぶ](...)`）。「画像」「スクリーンショット」だけの代替テキストにしない
- 画面の文言や配置を変える PR では、その画面の画像も同じ PR で作り直す
- 図は画像にせず、Mermaid で書く。アイコンなど画面以外の画像は `docs/images/` に置く

## CI の検査

PR では次の検査が走る。手元では同じコマンドで確かめられる。

| 検査（CI のジョブ） | 対象 | 失敗する例 |
| --- | --- | --- |
| Markdown lint | `docs/**/*.md`、`.spec/**/*.md`、各所の `README.md` | [`.markdownlint.json`](../.markdownlint.json) の違反 |
| Docs link check（lychee） | `docs/**/*.md`、ルートと `dotnet/`・`scripts/` の `README.md`、`nodejs/studio/e2e/README.md` | 存在しないファイルへの相対リンク。アンカー（`#...`）と外部 URL は確かめない |
| Docs meta check | `docs/` のメタブロック | 種別が 7 種以外、Version と最初の履歴の不一致、存在しない更新日 |
| Docs meta check（画面の一覧） | [`guides/ui-user-guide.md`](guides/ui-user-guide.md) の「画面の一覧」の表 | Studio の画面（`nodejs/studio/app/**/page.tsx`）が表に無い、または表の画面が実装に無い |
| Architecture tests | [`specifications/api-http.md`](specifications/api-http.md) §1、[`reference/permission-keys.md`](reference/permission-keys.md)、[`reference/error-codes.md`](reference/error-codes.md) の表 | 実装のエンドポイント（コミット済みの OpenAPI）・権限キー・エラーコード（`ApiErrorCodes`）と、表の行が食い違う |

```bash
npx markdownlint-cli2 "docs/<変更したファイルパス>"
node scripts/docs/check-doc-meta.mjs "docs/<変更したファイルパス>"
node scripts/docs/check-ui-routes.mjs
dotnet test dotnet/tests/Statevia.Architecture.Tests/Statevia.Architecture.Tests.csproj
```

- 照合の対象の表は、見出し（節の名前）と列の見出し（`パス`・`メソッド`・`Permission key`・`error.code`）で探す。見出しを変えると、表が見つからずに検査が失敗する
- コミット済みの OpenAPI（`dotnet/service/api/openapi/core-api-v1.openapi.json`）が古いと、エンドポイントの照合も古い一覧と比べてしまう。エンドポイントを変えたら OpenAPI を書き出し直す。書き出し忘れは Service API のテストが検出する

### 除外一覧の書き方

文書に載せないことが正しい項目（利用者に公開しない内部用のエンドポイントなど）は、`dotnet/tests/Statevia.Architecture.Tests/Docs/docs-consistency-exclusions.json` に理由つきで書く。

```json
{
  "apiRoutes": [{ "value": "GET /v1/<パス>", "reason": "<文書に載せない理由>" }],
  "permissionKeys": [],
  "errorCodes": [],
  "uiRoutes": [{ "value": "/<画面のパス>", "reason": "<文書に載せない理由>" }]
}
```

- 領域は `apiRoutes`・`permissionKeys`・`errorCodes`・`uiRoutes` の 4 つ。`value` の形は、エンドポイントが `メソッド パス`、画面がパス、ほかは値そのもの
- `value` と `reason` はどちらも空にできない。同じ領域での重複と、未知の領域名も失敗にする
- 文書を直せば済む食い違いは、除外せずに文書を直す

## リンク

- `docs/` 内の相互リンクは相対パスを用いる
- **禁止:** `docs/` から `.spec/`（および `.spec/archive/workspace-docs/` 等の内部作業用フォルダ）へリンクしない
  - 理由: `docs/` は利用者・ライブラリ利用者向けの正本。作業中の Spec / 下書きは公開契約に混ぜない
  - 完了した契約・説明は `docs/` 側へ昇格してから参照する
  - `.spec/` → `docs/` の参照は可（実装後の正本への誘導）
- **禁止:** 利用者向け本文から `AGENTS.md` を正本として指すこと。逆方向（`AGENTS.md` → `docs/`）は可
- 定義 YAML の例は [`samples/`](samples/) に置く。サンプル本文の改訂は本書の対象外
