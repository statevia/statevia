# Permission Keys 一覧

| 項目 | 値 |
| --- | --- |
| 種別 | Reference |
| Version | 1.6 |
| 更新日 | 2026-10-25 |

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
| `projects.create` | Create projects | `permissions.projectsCreate` |

`schedules.read` / `schedules.write` はカタログに無い。定期実行は `executions.read` / `executions.write` を使う。

- DB の `permission_definitions` テーブルへ `EnsurePermissionCatalogAsync` で seed される。
- **`tenant.admin`**: テナント管理者（`/v1/admin/*`）。JWT で `is_tenant_admin` の Principal は **全 catalog key** を Live 展開で持つ。
- **`modules.reload` / `modules.read`**: API キーの `allowed_scopes` およびグループ権限として発行可能（`tenant.admin` は発行対象外）。Module 管理 API での評価は `is_tenant_admin` **または** 当該 key。

## Runtime API との対応

| 操作（概要） | 必要な permission key |
| --- | --- |
| GET `/v1/definitions*`、`/v1/graphs/*`、`/v1/definitions/schema/nodes`、`/v1/actions/schema*` | `definitions.read` |
| POST / PUT `/v1/definitions`、`POST /v1/definitions/validate` | `definitions.write` |
| POST `/v1/projects` | `projects.create` |
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

定義と実行の閲覧、publish、Start、既存実行の Cancel・Resume・イベント送信、Project の更新とメンバー操作は、**global permission** に加え呼び出し元 Principal の `project_accesses` を評価する。既存実行の操作は Owner でも開始時ではなく現在の所属を見る。Principal の無い Worker 経路は受理時の判定に任せる。`tenant.admin` は所属が無い定義を読めない。メンバー操作だけは `project.admin` または `tenant.admin`。

| 状況 | HTTP | `error.code` |
| --- | --- | --- |
| プロジェクト未登録・Reader 未満 | 404 | `NOT_FOUND`（存在秘匿） |
| Reader のみで Start・Cancel・Resume・イベント送信、または Publisher 未満で定義作成 | 403 | `PROJECT_ACCESS_DENIED` |

ロールは reader、executor、publisher、admin の順に強い。PATCH は admin のみ。Project の DELETE は無い。

### 実行リソース許可

専用の permission key は増やさない。User と ServiceAccount は、所属のあと `principal_resource_grants` を見る。対象は定義と実行の閲覧、Start、Resume、Cancel。publish、Project 設定、メンバー、Group は見ない。

| 順 | 状況 | HTTP | `error.code` |
| --- | --- | --- | --- |
| 1 | 必要な catalog key が不足 | 403 | `PERMISSION_DENIED` |
| 2 | project 未登録、または Reader 未満 | 404 | `NOT_FOUND` |
| 3 | 操作に必要なロールが不足 | 403 | `PROJECT_ACCESS_DENIED` |
| 4 | その種別の許可行があり、対象が外れる | 403 | `RESOURCE_GRANT_DENIED` |
| 4 | その種別の許可行が無い | 追加制限なし | — |

project の行と definition の行は独立する。両方にあるときは両方を満たすものだけが通る。一覧の件数も同じ条件で絞る。System と、親から始まる継承子 Start は 4 を見ない。Resume と Cancel の Owner も 4 は Live の名簿を見る。許可は Snapshot に焼かない。

詳細: [security-runtime.md](../specifications/platform/security-runtime.md)、[api-http.md](../specifications/api-http.md) §2.7 / §3.12 / §4.1.3。

### Execution Security Snapshot（Resume / Cancel）

- **Owner**（Start 発行者）: Snapshot 上の `effectivePermissionKeys` で評価可（権限剥奪後も — Principal が有効なら）。実行リソース許可は Live。定期実行では Owner は run-as の ServiceAccount。作成者や後から Resume する人間は Operator。
- **Operator**: 常に Live の `executions.write` と Live の実行リソース許可を要求。

詳細: [security-runtime.md](../specifications/platform/security-runtime.md) Execution Security Snapshot、[execution-security-snapshot.md](../specifications/platform/execution-security-snapshot.md)。

## 関連

- HTTP 契約: [api-http.md](../specifications/api-http.md) §4.1.2.1
- セキュリティ境界: [security-runtime.md](../specifications/platform/security-runtime.md)
- エラーコード: [error-codes.md](error-codes.md)
