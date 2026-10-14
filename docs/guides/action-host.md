# Action Host ガイド

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.3 |
| 更新日 | 2026-10-14 |
| 関連 | [../concepts/actions.md](../concepts/actions.md), [operations-docker.md](operations-docker.md) |

---

**Version 1.3（2026-10-14）**: 既定の compose は Action Host を 2 台にし、`Endpoints` でそれぞれの URI を渡す。

**Version 1.2（2026-10-14）**: プロセスが 1 つのとき OutOfProcess は直列になる。同時に進める本数は `Endpoints` で増やす。環境変数は実行の前後で起動時の値に戻る。

**Version 1.1（2026-10-13）**: Action Host は秘密の環境変数で起動せず、modules は読み取り専用。Container は実行テナントのディレクトリだけをマウントする。

**Action Host**（`service/action-host/`）は、Policy が **OutOfProcess** を要求する Action を gRPC で実行するサンドボックスプロセスです。Service API から `Statevia:ActionHost:BaseUrl` で到達します。

## いつ必要か

- Community / Verified 等の Module で本番ポリシーが OutOfProcess を返す場合
- プロセス内（InProcess）では隔離が不十分な運用プロファイル（例: `saas-shared`）

未設定時、OutOfProcess が必要な実行は `ActionHostNotConfigured` で失敗します（安全側）。

## 同時実行

プロセスが 1 つのとき、OutOfProcess の Action はそのプロセスで 1 本ずつ実行されます。環境変数は各実行の前後で、そのプロセスが起動したときの値に戻ります。

同時に進める本数を増やすときは、Service API と専用 Worker の `Statevia:ActionHost:Endpoints` に絶対 URI を列挙します。`BaseUrl` と同時には設定しません。同じホストポートを複数の固定公開で重ねないでください。compose の既定は `BaseUrl` 1 件のままです。

## ローカル起動（例）

PostgreSQL と Service API と併せて起動する場合は Docker Compose が簡便です（[operations-docker.md](operations-docker.md)）。

単体で Action Host をホスト起動する例:

```bash
cd service/action-host
dotnet run --project Statevia.Service.ActionHost
```

既定 URL は launchSettings に依存します。Service API の `appsettings` または環境変数で合わせます:

```json
"Statevia": {
  "ActionHost": {
    "BaseUrl": "http://localhost:5001"
  }
}
```

### 平文 gRPC（h2c）

Action Host は **TLS 無し HTTP/2（h2c）** で待受する（Kestrel `HttpProtocols.Http2`）。  
`Http1AndHttp2` のまま TLS を付けないと HTTP/1.1 のみになり、Service API / Container サンドボックスからの gRPC が `Unavailable` / handshake 失敗になる。  
Service API 側は `System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport` を有効にして接続する。

## Docker Compose

`docker compose` は `action-host-1` と `action-host-2` の 2 台です。コンテナ内の待受はどちらも `5001` で、ホストへは `5001` と `5002` を別々に出します。Service API と専用 Worker は `Statevia:ActionHost:Endpoints` に `http://action-host-1:5001` と `http://action-host-2:5001` を渡し、`BaseUrl` は空にします。

`action-host-1` と `action-host-2` の `./modules` は読み取り専用でマウントする。`service-api` の同じパスは、配置とリモート取得のために読み書きのままである。

## 環境変数

次の名前が空白以外の値でプロセス環境にあると、Action Host は起動しない。失敗メッセージに値は出ない。

- `DATABASE_URL`
- `STATEVIA_SECRETS_KEY`
- `Statevia__Secrets__EncryptionKey`
- `STATEVIA_SMTP_HOST`、`STATEVIA_SMTP_PORT`、`STATEVIA_SMTP_USER`、`STATEVIA_SMTP_PASSWORD`、`STATEVIA_SMTP_FROM`
- `STATEVIA_NOTIFICATION_SMTP_SOURCE`
- `STATEVIA_BOOTSTRAP_PASSWORD`
- `Auth__Jwt__SigningKey`

未設定または空白だけの名前は拒否理由にしない。許可リスト外のその他は、Module をロードする前にプロセス環境から消して起動を続ける。残すのは `ASPNETCORE_`、`DOTNET_`、`COMPlus_` で始まる名前、`STATEVIA_MODULES_PATH`、プロセスとロケール（`PATH`、`HOME`、`USER`、`HOSTNAME`、`LANG`、`LC_ALL`、`TZ`）、一時ディレクトリ（`TMP`、`TEMP`、`TMPDIR`）、TLS 信頼ストア（`SSL_CERT_FILE`、`SSL_CERT_DIR`）である。Service API の環境変数は削らない。

Container が短命 Action Host を起動するとき渡す環境変数は `ASPNETCORE_URLS` と `STATEVIA_MODULES_PATH` だけである。`ModulesHostPath` があるときは、実行テナントのディレクトリだけを読み取り専用でマウントする。テナントを解決できないとき、またはパスが modules ルートの外に出るときは、コンテナを起動せずその Action を失敗させる。`ModulesHostPath` が未設定のときはマウントしない。

## Module の配置

常駐の Action Host は、起動時に `{modulesRoot}/{tenantKey}/{moduleId}/` の入口 DLL を読む。modules ルートの直下はテナントのディレクトリであり、Module 本体とはみなさない。入口 DLL が無い子と、`bin` や `obj` の中は読まない。Container がマウントする範囲は、実行テナントのディレクトリだけのままである。

## Module の reload

Module zip を配置したあと、Service API が新 DLL を読み込むには再起動または `POST /internal/modules/reload` が必要です（[operations-docker.md](operations-docker.md)、[cli-reference.md](cli-reference.md)）。

## 次に読むもの

- Action プラットフォーム仕様: [../specifications/actions/platform.md](../specifications/actions/platform.md)
- Module zip: [../specifications/actions/module-zip-layout.md](../specifications/actions/module-zip-layout.md)
