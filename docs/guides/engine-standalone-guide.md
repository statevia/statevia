# Engine 単体ガイド

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.3 |
| 更新日 | 2026-11-05 |
| 関連 | [../../dotnet/core/engine/samples/hello-statevia/README.md](../../dotnet/core/engine/samples/hello-statevia/README.md) |

---

Core-Engine は **C# ライブラリ**です。Service API なしで、サンプルプロジェクトから定義をロードしてインメモリ実行できます。

## サンプル: hello-statevia

```bash
cd dotnet/core/engine/samples/hello-statevia
dotnet run
```

`hello.yaml` に定義があり、コンソール上でワークフローが進行します。永続化・認証・Action Module は含みません。

## 定義の検証

`statevia def validate` は Service API の `POST /v1/definitions/validate` を呼びます。ホストなしのオフライン検証ではありません。手順は [cli-reference.md](cli-reference.md) を参照してください。

Engine 単体でロードと実行を確認するには、上の hello-statevia か、次の Engine テストを使います。

## 状態の実行器の登録

Join 以外のすべての状態に、`IStateExecutorFactory`（`DictionaryStateExecutorFactory` など）で実行器を登録してください。Engine が実行器を使わずに処理するのは Join の状態だけです。

| 定義の書き方 | 扱い | 実行器 |
| --- | --- | --- |
| 開始状態 | どこからも参照されない普通の状態（名前は任意） | 要る |
| `fork: [A, B]` | 遷移の書き方。`fork` を書いた状態は普通の状態で、その完了後に枝が始まる | `fork` を書いた状態にも、枝の各状態にも要る |
| `end: true` | 遷移の書き方。`end` という種類の状態は無い | `end: true` を書いた状態にも要る |
| `wait:` | 待つ処理を実行器で書く（`ctx.Events.WaitForEventAsync(ctx.NodeId, …)` で待つ） | 要る |
| `join:` | Engine が枝の到着をまとめる | 要らない |

業務の処理が無い状態（fork するだけ・終わるだけの状態）にも、何もせずに完了する実行器を登録します。Service API 経由では、Wait の実行器と、Action を書いていない状態の NoOp を API が用意するため、この登録は要りません。

`DefinitionCompiler.Compile` は既定で、実行器の無い状態があると `DefinitionValidationException` を投げます。例外の `Issues` には、実行器の無い状態ごとに 1 件、`RuleId` が `Compile.StateExecutorMissing`、`StateName` が状態名の指摘が入ります。

```csharp
var compiler = new DefinitionCompiler(new DictionaryStateExecutorFactory(executors));
var compiled = compiler.Compile(definition); // 登録漏れがあればここで例外
```

検査を止めるには `new DefinitionCompiler(factory, requireStateExecutors: false)` とします。実行器を後から用意するファクトリなど、コンパイル時に `GetExecutor` を呼べない場合に使います。検査を止めて実行器の無い状態に遷移すると、実行は Failed になります（[fsm.md](../specifications/execution/fsm.md) の「実行器の無い状態」）。

## Engine テストの実行

```bash
cd dotnet/core/engine
dotnet test statevia-engine.sln
```

## Service API 経由との違い

| 項目 | Engine 単体 | Service API |
| --- | --- | --- |
| 永続化 | なし | PostgreSQL |
| Action Module | サンプル内のスタブ | Catalog / Policy / Host |
| HTTP | なし | `/v1/*` |
| read-model | in-memory のみ | DB projection |

本番相当の動作確認は [getting-started.md](getting-started.md) を参照してください。

## 次に読むもの

- 定義の書き方: [../concepts/definition.md](../concepts/definition.md)
- 実行モデル: [../concepts/execution-model.md](../concepts/execution-model.md)
- 定義仕様: [../specifications/definition.md](../specifications/definition.md)
