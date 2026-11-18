# Specifications

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.2 |
| 更新日 | 2026-11-18 |

---

**契約・振る舞い** の Normative 正本（必須 / 推奨 / 任意 / 禁止）。背景の「なぜ」は [`../concepts/`](../concepts/) へ。

**Version 1.2（2026-11-18）**: 未実装の設計だった監査・再現性（hash chain）を [`../future/audit-hash-chain.md`](../future/audit-hash-chain.md) へ移した。

**Version 1.1（2026-09-16）**: 運用ログ契約（JSON stdout）を platform に追加。

## 中核

| ドキュメント | 領域 |
| --- | --- |
| [`definition.md`](definition.md) | ワークフロー定義 YAML |
| [`api-http.md`](api-http.md) | Service API HTTP 契約 |
| [`data-integration.md`](data-integration.md) | Engine / API / UI データ連携 |

## execution/

| ドキュメント | 領域 |
| --- | --- |
| [`execution/fsm.md`](execution/fsm.md) | FSM・ステータス |
| [`execution/events-and-commands.md`](execution/events-and-commands.md) | イベント・コマンド |
| [`execution/wait-cancel.md`](execution/wait-cancel.md) | Wait / Cancel |
| [`execution/fork-join.md`](execution/fork-join.md) | Fork / Join |
| [`execution/execution-graph.md`](execution/execution-graph.md) | ExecutionGraph JSON |

## actions/

| ドキュメント | 領域 |
| --- | --- |
| [`actions/platform.md`](actions/platform.md) | Catalog / Policy / ModuleHost |
| [`actions/module-zip-layout.md`](actions/module-zip-layout.md) | Module zip レイアウト |

## platform/

| ドキュメント | 領域 |
| --- | --- |
| [`platform/security-runtime.md`](platform/security-runtime.md) | Runtime Security Boundary |
| [`platform/execution-security-snapshot.md`](platform/execution-security-snapshot.md) | Execution Security Snapshot |
| [`platform/io-log-masking.md`](platform/io-log-masking.md) | workflow IO ログマスキング |
| [`platform/logging-operations.md`](platform/logging-operations.md) | 運用ログ（JSON stdout。ベンダー非必須） |

## ui/

| ドキュメント | 領域 |
| --- | --- |
| [`ui/visual.md`](ui/visual.md) | UI 可視化 |
| [`ui/push-api.md`](ui/push-api.md) | Push API（SSE） |

Concept との対応は [`../concepts/README.md`](../concepts/README.md)。
