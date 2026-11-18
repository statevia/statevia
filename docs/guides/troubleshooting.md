# トラブルシュート

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-11-18 |
| 関連 | [getting-started.md](getting-started.md), [operations-docker.md](operations-docker.md), [../reference/error-codes.md](../reference/error-codes.md), [ui-user-guide.md](ui-user-guide.md) |

---

API と Studio を使っていて詰まったときに、症状から原因と対処を探す。エラー応答は `{ "error": { "code", "message", "details" } }` の形で、`code` の一覧は [error-codes.md](../reference/error-codes.md) にある。コンテナの起動・設定の検証・ポートの衝突など運用側の問題は [operations-docker.md](operations-docker.md) のトラブルシュートを参照する。

## 起動と接続

| 症状 | 原因 | 対処 |
| --- | --- | --- |
| `GET /v1/health` が応答しない | Service API が起動していない、または起動直後に落ちた | `docker compose ps` で `service-api` の状態を見て、`docker compose logs service-api` で理由を確かめる。設定の検証で落ちたときはキー名がログに出る（[operations-docker.md](operations-docker.md)） |
| 初回の起動で Service API が DB のエラーで落ちる | マイグレーションを適用していない | [getting-started.md](getting-started.md) §1 の `dotnet ef database update` を実行し、`docker compose restart service-api` で起動し直す |
| UI は開くが API の呼び出しが失敗する | UI から Service API に届いていない | compose では UI のプロキシ（`/api/core/...`）が内部の `service-api` に転送する。ブラウザから `localhost:8080` を直接呼ばない（[operations-docker.md](operations-docker.md)） |

## サインインと認証（401）

| 症状 | 原因 | 対処 |
| --- | --- | --- |
| ログインが 401（`UNAUTHORIZED`、`Invalid credentials.`） | テナントキー・ユーザー名・パスワードのどれかが違う。存在しないテナントキーも同じ応答になる | 値を確かめる。開発用の管理者は Development でだけ作られる（[getting-started.md](getting-started.md) §3） |
| 正しいパスワードでもログインが 401 | 15 分以内に 5 回失敗してロックされた。応答は資格情報の誤りと同じ | 15 分待つ。ロックは PostgreSQL に残るので、Service API を再起動しても解けない |
| API が 401（`UNAUTHORIZED`、`Authentication required.`） | `Authorization: Bearer` が無い、トークンの期限が切れた（既定 60 分。`Auth:Jwt:AccessTokenLifetimeMinutes`）、または署名が合わない | ログインし直して新しいトークンを使う。SSE（`/v1/executions/{id}/stream`）も同じ |
| API が 403（`TENANT_HEADER_MISMATCH`） | `X-Tenant-Id` が JWT のテナントと違う | `X-Tenant-Id` を JWT のテナントキー（ログイン応答の `tenantKey`）にそろえる。JWT で呼ぶときは、テナントは JWT から決まる |
| Studio が何度もログイン画面に戻る | Studio のセッション（Cookie）が無くなった、または中の JWT の期限が切れた | ログインし直す。ログアウトはセッションを消すだけで、Service API の JWT は期限まで有効 |

## 権限（403 / 404）

| 症状 | 原因 | 対処 |
| --- | --- | --- |
| 403（`PERMISSION_DENIED`、`Insufficient permission.`） | 操作に要る permission key が無い。API キーは発行時の権限の範囲でしか使えない | ユーザーは所属グループに権限を足す（Studio のグループ管理）。API キーは必要な権限で発行し直す。操作と権限の対応は [permission-keys.md](../reference/permission-keys.md) |
| 403（`PROJECT_ACCESS_DENIED`） | Project のロールが足りない（例: 閲覧者のまま実行を開始した） | Project の管理者に、実行者以上のロールを付けてもらう（[ui-user-guide.md](ui-user-guide.md) の所属 Project） |
| 定義や実行が 404（`NOT_FOUND`）だが、ほかの人には見える | その Project に所属していない。所属していない Project の定義・実行は、存在しないものとして扱う | Project の管理者にメンバーとして追加してもらう |
| 403（`RESOURCE_GRANT_DENIED`） | ユーザーまたはサービスアカウントの「操作できる定義」の外 | テナント管理者に、操作できる定義へ追加してもらう（[ui-user-guide.md](ui-user-guide.md) のテナント管理） |
| Studio にテナント管理や定期実行のメニューが無い | tenant.admin ではない。管理画面と定期実行は tenant.admin だけが使える | テナント管理者に依頼する。定期実行は HTTP（`executions.write`）でも作れる |
| Studio の定義一覧・実行一覧が空 | サイドメニュー先頭で選んでいる Project に、定義・実行が無い | 別の Project を選ぶ。所属が無いときは、Project の管理者に追加してもらうか、`projects.create` があれば Project を作る |

## 定義の登録と一覧（422）

| 症状 | 原因 | 対処 |
| --- | --- | --- |
| 定義の登録が 422（`projectId is required`） | `projectId` を送っていない。定義はどれか 1 つの Project に属する | `GET /v1/projects` で `projectId` を取って送る（[http-request-examples.md](http-request-examples.md)） |
| 定義の登録が 422（`must be a letter followed by letters, digits, dots, underscores, or hyphens.`） | 定義名・状態名・イベント名に、ASCII の識別子でない文字（日本語など）を使った | 名前は英字で始めて英数字と `.` `_` `-` だけにする。日本語はコメントと `description` に書く |
| 定義の登録・検証が 422（`details` に状態名や Action の ID が出る） | 定義の構造の誤り、または Action の `input` がスキーマに合わない | `POST /v1/definitions/validate` で保存せずに確かめる。書き方は [definition.md](../specifications/definition.md) |
| 一覧が 422（`limit is required`） | `GET /v1/definitions` などの一覧に `limit` を付けていない | `?limit=20` のように付ける |

## 実行

| 症状 | 原因 | 対処 |
| --- | --- | --- |
| 実行の開始が 404（`Definition not found`） | `definitionId` が違う、定義が削除済み、または所属していない Project の定義 | 定義の登録応答の `displayId`（または UUID）を使う。削除した定義は復元してから使う |
| 実行の開始が 409（`IDEMPOTENCY_KEY_CONFLICT`） | 同じ `X-Idempotency-Key` で、前と違う本文を送った | 同じキーは、同じ本文の再送にだけ使う（同じ本文なら同じ実行が返る）。別の実行には新しいキーを使う |
| 実行が `ACTIVE` のまま進まず、応答の `waiting` が true | Wait でイベントを待っている | `GET /v1/executions/{id}/waits` で待っているノードと `allowedEvents` を確かめ、`POST .../nodes/{nodeId}/resume` で進める。Studio では実行詳細の「イベントを送信」 |
| 実行が `ACTIVE` のまま進まず、`waiting` も false | 実行を進める Worker が動いていない。Worker を分ける構成（`compose/split-runtime.yml`）で `worker` コンテナが止まっているか、台数が 0 | `docker compose ps` で `worker` を確かめて起動する（[operations-docker.md](operations-docker.md) のランタイム分離）。既定の構成では Worker は Service API の中で動く |
| 再開（`resume`）が 422 | `resumeKey` がそのノードの `allowedEvents` に無い、ノードがもう待機中でない、または名前の形が不正 | `waits` の `allowedEvents` にあるイベント名を送る。終端した実行への再開は 204 で、何も起きない |
| イベント名の送信（`POST /v1/executions/{id}/events`）が 422 | 当てはまる待機が無い、または複数ある。1 つの待機が複数のイベントを受け付けるときも受け付けない | 正本の `resume` でノードを指定して送る |

## 次に読むもの

- エラーコードの一覧: [error-codes.md](../reference/error-codes.md)
- 運用側のトラブルシュート: [operations-docker.md](operations-docker.md)
- HTTP の例: [http-request-examples.md](http-request-examples.md)
