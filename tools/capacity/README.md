# 容量計測ハーネス

Service API に対する **L1 / L1O / L2 / L3 / D1 / C1** 負荷クライアントです。本番 API / Engine の責務は変更しません。公開件数の正本は [`docs/reference/service-capacity.md`](../../docs/reference/service-capacity.md) です。手順は [`docs/guides/capacity-load-testing.md`](../../docs/guides/capacity-load-testing.md) です。

CI の既定ジョブには載せません。

## 実行

```bash
dotnet test tools/capacity/Statevia.Tools.Capacity.sln

export STATEVIA_CAPACITY_PASSWORD='admin123'
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L1 --output tools/capacity/results/l1.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L1O --output tools/capacity/results/l1o.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L2 --output tools/capacity/results/l2.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L3 --output tools/capacity/results/l3.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario D1 --output tools/capacity/results/d1.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario C1 --output tools/capacity/results/c1.json
```

PowerShell の例:

```powershell
dotnet test tools/capacity/Statevia.Tools.Capacity.sln

$env:STATEVIA_CAPACITY_PASSWORD = 'admin123'
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L1 --output tools/capacity/results/l1.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L1O --output tools/capacity/results/l1o.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L2 --output tools/capacity/results/l2.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario L3 --output tools/capacity/results/l3.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario D1 --output tools/capacity/results/d1.json
dotnet run --project tools/capacity/Statevia.Tools.Capacity -- --scenario C1 --output tools/capacity/results/c1.json
```

認証は次のいずれかです。

- `--username` + `STATEVIA_CAPACITY_PASSWORD`（または `--password`）で `POST /v1/auth/login`
- `--token`（Bearer）
- `--api-key`（`X-Api-Key`）

`--tenant` 既定は `default`。`--base-url` 既定は `http://localhost:8080`。

主な引数: `--count`（実行数）、`--concurrency`（同時 HTTP）、`--hw-label`、`--hw-notes`、`--topology`（現行参照は `split-runtime`。既定値は `phase0-single-api`）。D1 は `--restart-service`（既定 `service-api`、git ルートで `docker compose restart`）。D1 / C1 / L1O の既定タイムアウトは 300 秒です。記録用: `--worker-replicas` / `--worker-max-concurrency` / `--worker-cancel-concurrency`（サーバ側は変えません）。C1 の settle は `--settle-ms`（既定 3000）。

Worker プロセス数 × スロットの縮小格子は `run-worker-grid.ps1` です（L1 16 セル + C1 角 16 セル。`ui-studio` は起動しません）。

JSON の隣に同名 `.csv` を書き出します。`tools/capacity/results/` は gitignore です。

## シナリオ

| ID | 内容 |
| --- | --- |
| L1 | builtin `noop` の短寿命 Definition を Start し、`Completed` まで待つ |
| L1O | builtin `sleep` 500ms でスロットを占有し、`Completed` まで待つ。CPU は焼かない |
| L2 | 単一イベント Wait まで進めてからノード Resume し、`Completed` まで待つ |
| L3 | `wait.timeout` 付き YAML で DelayWait / TimerFire が HTTP から起きるか確認する。起きなければ leftover を Cancel する |
| D1 | L2 相当の Wait の途中で Service API を再起動し、投影の非破壊と Resume 完了を合否で記録する |
| C1 | L2 と同じ Wait 定義を Start し、短い settle のあと一斉 Cancel する。GET `/waits` の WAITING snapshot は待たない |
