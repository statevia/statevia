# Permission Keys 一覧

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.5 |
| 更新日 | 2026-09-29 |

---

Runtime API の **semantic permission key** の調べ物。Normative 契約は [api-http.md](../specifications/api-http.md) §4.1.2.1、[security-runtime.md](../specifications/platform/security-runtime.md) を正とする。

正本実装: `WellKnownPermissionKeys` / `PermissionCatalog`（`core/application/Statevia.Core.Application.Contracts/Security/`）。

## カタログ（初版）

| Permission key | 表示ラベル（参考） | i18n キー（参考） |
| --- | --- | --- |
| `definitions.read` | Read definitions | `permissions.definitionsRead` |
| `definitions.write` | Write definitions | `permissions.definitionsWrite` |
| `executions.read` | Read executions | `permissions.executionsRead` |
| `executions.write` | Write executions | `permissions.executionsWrite` |
| `tenant.admin` | Tenant administration | `permissions.tenantAdmin` |
| `modules.reload` | Reload action modules | `permissions.modulesReload` |
| `modules.read` | Read action modules | `permissions.modulesRead` |

`schedules.read` / `schedules.write` はカタログに無い。定期実行は `executions.read` / `executions.write` を使う。

- DB の `permission_definitions` テーブルへ `EnsurePermissionCatalogAsync` で seed される。
- **`tenant.admin`**: テナント管理者（`/v1/admin/*`）。JWT で `is_tenant_admin` の Principal は **全 catalog key** を Live 展開で持つ。
- **`modules.reload` / `modules.read`**: API キーの `allowed_scopes` およびグループ権限として発行可能（`tenant.admin` は発行対象外）。Module 管理 API での評価は `is_tenant_admin` **または** 当該 key。

## Runtime API との対応

| 操作（概要） | 必要な permission key |
| --- | --- |
| GET `/v1/definitions*`、`/v1/graphs/*`、`/v1/definitions/schema/nodes`、`/v1/actions/schema*` | `definitions.read` |
| POST / PUT `/v1/definitions`、`POST /v1/definitions/validate` | `definitions.write` |
| GET `/v1/executions*`（一覧・詳細・graph・state・events・stream）、GET `/v1/schedules*` | `executions.read` |
| POST start / cancel / publish event / resume、**`POST /v1/events`**、スケジュールの作成・更新・削除・手動実行 | `executions.write` |
| POST `/internal/modules/reload` | `modules.reload`（または `is_tenant_admin`） |
| GET `/v1/admin/modules` | `modules.read`（または `is_tenant_admin`） |

不足時: **403**、`error.code = PERMISSION_DENIED`（[error-codes.md](error-codes.md)）。`POST /v1/events` も同じ（Principal は必須。読み取り専用では Wait を進められない）。

## 評価ルール（抜粋）

| 認証方式 | 有効 permission の決まり方 |
| --- | --- |
| JWT（ユーザー） | 所属グループの permission を Live 展開（`ExpandPrincipalPermissionKeysAsync`） |
| API キー | **`effective = 展開許可 ∩ allowed_scopes`**（交差のみ。deny リストなし） |

### プロジェクト認可（併用）

定義取得・publish・Start は **global permission** に加え `project_accesses` を評価する。

| 状況 | HTTP | `error.code` |
| --- | --- | --- |
| プロジェクト未登録・Reader 未満 | 404 | `NOT_FOUND`（存在秘匿） |
| Reader のみで Start | 403 | `PROJECT_ACCESS_DENIED` |

### 実行リソース許可（Start のみ）

専用の permission key は増やさない。`executions.write` と project の executor のあと、User と ServiceAccount の Start だけが `principal_resource_grants` を見る。

| 順 | 状況 | HTTP | `error.code` |
| --- | --- | --- | --- |
| 1 | `executions.write` 不足 | 403 | `PERMISSION_DENIED` |
| 2 | project 未登録、または Reader 未満 | 404 | `NOT_FOUND` |
| 3 | Reader のみ | 403 | `PROJECT_ACCESS_DENIED` |
| 4 | その種別の許可行があり、対象が外れる | 403 | `RESOURCE_GRANT_DENIED` |
| 4 | その種別の許可行が無い | 追加制限なし | — |

project の行と definition の行は独立する。両方にあるときは両方を満たす定義だけが通る。System と、親から始まる継承子 Start は 4 を見ない。定義の取得、Resume、Cancel も見ない。

詳細: [security-runtime.md](../specifications/platform/security-runtime.md)、[api-http.md](../specifications/api-http.md) §3.12 / §4.1.3。

### Execution Security Snapshot（Resume / Cancel）

- **Owner**（Start 発行者）: Snapshot 上の `effectivePermissionKeys` で評価可（権限剥奪後も — Principal が有効なら）。定期実行では Owner は run-as の ServiceAccount。作成者や後から Resume する人間は Operator。
- **Operator**: 常に Live の `executions.write` を要求。

詳細: [security-runtime.md](../specifications/platform/security-runtime.md) Execution Security Snapshot、[execution-security-snapshot.md](../specifications/platform/execution-security-snapshot.md)。

## 関連

- HTTP 契約: [api-http.md](../specifications/api-http.md) §4.1.2.1
- セキュリティ境界: [security-runtime.md](../specifications/platform/security-runtime.md)
- エラーコード: [error-codes.md](error-codes.md)
