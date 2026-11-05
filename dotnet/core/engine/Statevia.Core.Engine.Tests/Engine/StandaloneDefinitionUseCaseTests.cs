using System.Collections.Concurrent;
using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// Engine をライブラリとして単体で使うときの定義のユースケースを、利用者と同じ手順で検証する。
/// </summary>
/// <remarks>
/// 各テストは YAML（States 形式）を <see cref="StateWorkflowDefinitionLoader"/> で読み、<see cref="DefinitionValidator"/> で検証し、
/// 状態名ごとに <see cref="IStateExecutor"/> を登録して <see cref="DefinitionCompiler"/> でコンパイルし、<see cref="ExecutionEngine"/> で走らせる。
/// 手組みの <see cref="CompiledWorkflowDefinition"/> では見えない、ローダー・検証・コンパイラ・Engine のつなぎ目を確かめるのが目的。
/// Fork 展開ハンドラは登録しない（枝は同じ execution の中で走る）。
/// </remarks>
public sealed class StandaloneDefinitionUseCaseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 直線の定義で、開始 input を <c>$.input…</c> で読み、リテラル・ネスト map・ドットキーを組み合わせた input を受け取れる。
    /// <c>input</c> を書かない状態には直前の状態の output がそのまま渡る。
    /// </summary>
    [Fact]
    public async Task Start_LinearDefinition_MapsWorkflowInputAndPassesPreviousOutput()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: OrderLinear
            states:
              Start:
                on:
                  Completed:
                    next: Enrich
              Enrich:
                input:
                  orderId: $.input.orderId
                  currency: JPY
                  quantity: 2
                  body:
                    customer: $.input.customer.name
                  meta.source: standalone
                on:
                  Completed:
                    next: Ship
              Ship:
                on:
                  Completed:
                    end: true
            """;
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var states = new StateTable()
            .Add("Start", _ => "started")
            .Add("Enrich", input => { captured["Enrich"] = input; return "enriched"; })
            .Add("Ship", input => { captured["Ship"] = input; return null; });
        using var engine = ExecutionEngineTestHarness.Create();
        var workflowInput = new Dictionary<string, object?>
        {
            ["orderId"] = "ORD-1",
            ["customer"] = new Dictionary<string, object?> { ["name"] = "Sato" }
        };

        // Act
        var id = engine.Start(Compile(yaml, states), input: workflowInput);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var enrichInput = AsMap(captured["Enrich"]);
        Assert.Equal("ORD-1", enrichInput["orderId"]);
        Assert.Equal("JPY", enrichInput["currency"]);
        Assert.Equal(2, Convert.ToInt32(enrichInput["quantity"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Sato", AsMap(enrichInput["body"])["customer"]);
        Assert.Equal("standalone", AsMap(enrichInput["meta"])["source"]);
        Assert.Equal("enriched", captured["Ship"]);
    }

    /// <summary>
    /// <c>output: $.vars.user</c> で書いた値を、後続が <c>$.vars…</c> で読める。
    /// <c>$.states.&lt;Name&gt;.output</c> と <c>$.sys.execution.id</c> / <c>$.sys.definition.name</c> も読める。
    /// </summary>
    [Fact]
    public async Task Start_OutputToVars_LaterStateReadsVarsStatesAndSys()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: VarsSample
            states:
              GetUser:
                output: $.vars.user
                on:
                  Completed:
                    next: Notify
              Notify:
                input:
                  email: $.vars.user.email
                  name: $.states.GetUser.output.name
                  executionId: $.sys.execution.id
                  definitionName: $.sys.definition.name
                on:
                  Completed:
                    end: true
            """;
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var states = new StateTable()
            .Add("GetUser", _ => new Dictionary<string, object?> { ["name"] = "Sato", ["email"] = "sato@example.test" })
            .Add("Notify", input => { captured["Notify"] = input; return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var notifyInput = AsMap(captured["Notify"]);
        Assert.Equal("sato@example.test", notifyInput["email"]);
        Assert.Equal("Sato", notifyInput["name"]);
        Assert.Equal(id, notifyInput["executionId"]);
        Assert.Equal("VarsSample", notifyInput["definitionName"]);
    }

    /// <summary>条件遷移（<c>cases</c> / <c>default</c>）で、開始 input の金額により行き先が変わる。</summary>
    [Theory]
    [InlineData(5000, "Manual")]
    [InlineData(100, "Auto")]
    public async Task Start_ConditionalTransition_RoutesByWorkflowInput(int amount, string expectedState)
    {
        // Arrange
        const string yaml = """
            workflow:
              name: Approval
            states:
              Check:
                on:
                  Completed:
                    cases:
                      - when:
                          path: $.input.amount
                          op: gt
                          value: 1000
                        next: Manual
                    default:
                      next: Auto
              Manual:
                on:
                  Completed:
                    end: true
              Auto:
                on:
                  Completed:
                    end: true
            """;
        var visited = new ConcurrentQueue<string>();
        var states = new StateTable()
            .Add("Check", _ => null)
            .Add("Manual", _ => { visited.Enqueue("Manual"); return null; })
            .Add("Auto", _ => { visited.Enqueue("Auto"); return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states), input: new Dictionary<string, object?> { ["amount"] = amount });
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        Assert.Equal([expectedState], visited.ToArray());
    }

    /// <summary>
    /// 条件遷移で前の状態に戻るループ（再試行）が書ける。<c>Try</c> が 3 回目に成功するまで繰り返し、その後 <c>Done</c> で終わる。
    /// </summary>
    [Fact]
    public async Task Start_RetryLoop_RepeatsUntilConditionIsMet()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: RetryLoop
            states:
              Try:
                on:
                  Completed:
                    next: Check
              Check:
                on:
                  Completed:
                    cases:
                      - when:
                          path: $.states.Try.output.ok
                          op: eq
                          value: true
                        next: Done
                    default:
                      next: Try
              Done:
                on:
                  Completed:
                    end: true
            """;
        var attempts = 0;
        var states = new StateTable()
            .Add("Try", _ => new Dictionary<string, object?> { ["ok"] = Interlocked.Increment(ref attempts) >= 3 })
            .Add("Check", _ => null)
            .Add("Done", _ => null);
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        Assert.Equal(3, Volatile.Read(ref attempts));
    }

    /// <summary>状態が例外を投げると <c>on.Failed</c> の補償へ進み、実行は Completed で終わる。</summary>
    [Fact]
    public async Task Start_StateThrows_OnFailedRoutesToCompensation()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: Compensation
            states:
              Charge:
                on:
                  Completed:
                    next: Ship
                  Failed:
                    next: Refund
              Ship:
                on:
                  Completed:
                    end: true
              Refund:
                on:
                  Completed:
                    end: true
            """;
        var visited = new ConcurrentQueue<string>();
        var states = new StateTable()
            .Add("Charge", _ => throw new InvalidOperationException("card declined"))
            .Add("Ship", _ => { visited.Enqueue("Ship"); return null; })
            .Add("Refund", _ => { visited.Enqueue("Refund"); return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });

        // Assert
        var snapshot = engine.GetSnapshot(id)!;
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
        Assert.Equal(["Refund"], visited.ToArray());
    }

    /// <summary><c>on.Failed</c> の無い状態が例外を投げると、実行は Failed で終わり後続へ進まない。</summary>
    [Fact]
    public async Task Start_StateThrowsWithoutOnFailed_ExecutionFails()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: NoCompensation
            states:
              Charge:
                on:
                  Completed:
                    next: Ship
              Ship:
                on:
                  Completed:
                    end: true
            """;
        var shipped = false;
        var states = new StateTable()
            .Add("Charge", _ => throw new InvalidOperationException("card declined"))
            .Add("Ship", _ => { shipped = true; return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });

        // Assert
        Assert.True(engine.GetSnapshot(id)!.IsFailed);
        Assert.False(shipped);
    }

    /// <summary>開始状態の実行器を登録し忘れると、実行は Running のまま止まらずに Failed で終わる。ノードは作らない。</summary>
    [Fact]
    public async Task Start_InitialStateExecutorMissing_ExecutionFails()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: MissingInitial
            states:
              Receive:
                on:
                  Completed:
                    end: true
            """;
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, new StateTable(), requireStateExecutors: false));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });

        // Assert
        var snapshot = engine.GetSnapshot(id)!;
        Assert.True(snapshot.IsFailed);
        Assert.False(snapshot.IsCancelled);
        Assert.Empty(NodeNames(engine, id));
    }

    /// <summary>
    /// 途中の状態の実行器を登録し忘れると、前の状態までは実行され、その状態に遷移した時点で実行は Failed で終わる。
    /// <c>on.Failed</c> があっても辿らない（設定の誤りで、業務の失敗ではない）。
    /// </summary>
    [Fact]
    public async Task Start_LaterStateExecutorMissing_RunsPreviousStateThenFailsWithoutOnFailed()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: MissingLater
            states:
              Receive:
                on:
                  Completed:
                    next: Charge
              Charge:
                on:
                  Completed:
                    end: true
                  Failed:
                    next: Compensate
              Compensate:
                on:
                  Completed:
                    end: true
            """;
        var compensated = false;
        var states = new StateTable()
            .Add("Receive", _ => "received")
            .Add("Compensate", _ => { compensated = true; return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states, requireStateExecutors: false));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });

        // Assert
        Assert.True(engine.GetSnapshot(id)!.IsFailed);
        Assert.False(compensated);
        Assert.Equal(["Receive"], NodeNames(engine, id));
    }

    /// <summary>
    /// Fork の枝の中の状態の実行器を登録し忘れると、実行は Failed で終わり、他の枝で走っている状態は止まる。
    /// 止まった状態の Cancelled は終端失敗の後として抑止され、実行は Cancelled にならない。
    /// </summary>
    [Fact]
    public async Task Start_ExecutorMissingInForkBranch_FailsAndHaltsOtherBranch()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: MissingInBranch
            states:
              Receive:
                on:
                  Completed:
                    fork: [Slow, Check]
              Slow:
                on:
                  Completed:
                    next: Join1
              Check:
                on:
                  Completed:
                    next: Missing
              Missing:
                on:
                  Completed:
                    next: Join1
              Join1:
                join:
                  all: [Slow, Check]
                on:
                  Joined:
                    end: true
            """;
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowHalted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var states = new StateTable()
            .Add("Receive", _ => null)
            .Add("Slow", async (_, _, ct) =>
            {
                slowStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    slowHalted.SetResult();
                    throw;
                }

                return null;
            })
            .Add("Check", async (_, _, _) =>
            {
                // Slow が走り出してから Missing へ進み、止める対象があることを確かにする。
                await slowStarted.Task;
                return null;
            });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states, requireStateExecutors: false));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });
        await slowHalted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        var snapshot = engine.GetSnapshot(id)!;
        Assert.True(snapshot.IsFailed);
        Assert.False(snapshot.IsCancelled);
        Assert.DoesNotContain("Missing", NodeNames(engine, id));
    }

    /// <summary><c>wait.events</c> の Wait を <see cref="ExecutionEngine.ResumeWaitNode"/> で再開すると、渡したイベントの遷移先へ進む。</summary>
    [Theory]
    [InlineData("approve", "Approved")]
    [InlineData("reject", "Rejected")]
    public async Task ResumeWaitNode_WaitEvents_FollowsResumedEvent(string eventName, string expectedState)
    {
        // Arrange
        const string yaml = """
            workflow:
              name: Review
            states:
              Submit:
                on:
                  Completed:
                    next: Review
              Review:
                wait:
                  events:
                    approve: Approved
                    reject: Rejected
              Approved:
                on:
                  Completed:
                    end: true
              Rejected:
                on:
                  Completed:
                    end: true
            """;
        var visited = new ConcurrentQueue<string>();
        var states = new StateTable()
            .Add("Submit", _ => null)
            .AddWait("Review", "approve", "reject")
            .Add("Approved", _ => { visited.Enqueue("Approved"); return null; })
            .Add("Rejected", _ => { visited.Enqueue("Rejected"); return null; });
        using var engine = ExecutionEngineTestHarness.Create();
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine, id).Count == 1);

        // Act
        engine.ResumeWaitNode(id, ActiveWaitNodeIds(engine, id)[0], eventName);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        Assert.Equal([expectedState], visited.ToArray());
    }

    /// <summary>
    /// Wait の状態がイベント名を返さず業務の値（ここでは承認者名）を返しても、Engine は再開に使ったイベントで遷移先を決める。
    /// </summary>
    [Theory]
    [InlineData("approve", "Approved")]
    [InlineData("reject", "Rejected")]
    public async Task ResumeWaitNode_WaitStateReturnsDomainValue_RoutesByResumedEvent(string eventName, string expectedState)
    {
        // Arrange
        var visited = new ConcurrentQueue<string>();
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var states = new StateTable()
            .Add("Submit", _ => null)
            .Add("Review", async (ctx, _, ct) =>
            {
                await ctx.Events.WaitForEventAsync(ctx.NodeId!, ["approve", "reject"], ct);
                return "reviewer-1";
            })
            .Add("Approved", input => { visited.Enqueue("Approved"); captured["next"] = input; return null; })
            .Add("Rejected", input => { visited.Enqueue("Rejected"); captured["next"] = input; return null; });
        using var engine = ExecutionEngineTestHarness.Create();
        var id = engine.Start(Compile(ReviewYaml, states));
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine, id).Count == 1);

        // Act
        engine.ResumeWaitNode(id, ActiveWaitNodeIds(engine, id)[0], eventName);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        Assert.Equal([expectedState], visited.ToArray());
        Assert.Equal("reviewer-1", captured["next"]);
    }

    /// <summary>
    /// Wait の状態がイベントを待たずに完了すると（再開イベントが分からない）、遷移先が決まらないので実行は Failed で終わる。
    /// </summary>
    [Fact]
    public async Task Start_WaitStateCompletesWithoutWaiting_ExecutionFails()
    {
        // Arrange
        var visited = new ConcurrentQueue<string>();
        var states = new StateTable()
            .Add("Submit", _ => null)
            .Add("Review", _ => "no-wait")
            .Add("Approved", _ => { visited.Enqueue("Approved"); return null; })
            .Add("Rejected", _ => { visited.Enqueue("Rejected"); return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(ReviewYaml, states));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });

        // Assert
        Assert.True(engine.GetSnapshot(id)!.IsFailed);
        Assert.Empty(visited);
    }

    /// <summary><c>Submit</c> → Wait <c>Review</c>（approve → Approved / reject → Rejected）の定義。</summary>
    private const string ReviewYaml = """
        workflow:
          name: Review
        states:
          Submit:
            on:
              Completed:
                next: Review
          Review:
            wait:
              events:
                approve: Approved
                reject: Rejected
          Approved:
            on:
              Completed:
                end: true
          Rejected:
            on:
              Completed:
                end: true
        """;

    /// <summary>旧形式（<c>wait.event</c> + <c>on.Completed.next</c>）の Wait も、ローダーの正規化を経て再開できる。</summary>
    [Fact]
    public async Task PublishEvent_LegacyWaitEventSyntax_ResumesToNext()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: LegacyWait
            states:
              Hold:
                wait:
                  event: Go
                on:
                  Completed:
                    next: Done
              Done:
                on:
                  Completed:
                    end: true
            """;
        var done = false;
        var states = new StateTable()
            .AddWait("Hold", "Go")
            .Add("Done", _ => { done = true; return null; });
        using var engine = ExecutionEngineTestHarness.Create();
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine, id).Count == 1);

        // Act
        engine.PublishEvent(id, "Go");
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        Assert.True(done);
    }

    /// <summary>
    /// hello-statevia サンプルと同じ定義（Fork の片方の枝が Wait）。Wait の状態はサンプルと同じく <c>bool</c> を返す。
    /// イベントで Wait を再開すると Join が成立し、<c>Work</c> は枝の先頭の名前をキーにした辞書を受け取る。
    /// </summary>
    [Fact]
    public async Task PublishEvent_HelloSampleDefinition_JoinsAndPassesBranchOutputs()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: HelloWorkflow
            states:
              Start:
                on:
                  Completed:
                    fork: [Prepare, AskUser]
              Prepare:
                on:
                  Completed:
                    next: Join1
              AskUser:
                wait:
                  events:
                    UserApproved: Join1
              Join1:
                join:
                  all: [Prepare, AskUser]
                on:
                  Joined:
                    next: Work
              Work:
                on:
                  Completed:
                    next: End
              End:
                on:
                  Completed:
                    end: true
            """;
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var states = new StateTable()
            .Add("Start", _ => null)
            .Add("Prepare", _ => "prepared")
            .Add("AskUser", async (ctx, _, ct) =>
            {
                await ctx.Events.WaitForEventAsync(ctx.NodeId!, ["UserApproved"], ct);
                return true;
            })
            .Add("Work", input => { captured["Work"] = input; return null; })
            .Add("End", _ => null);
        using var engine = ExecutionEngineTestHarness.Create();
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine, id).Count == 1);

        // Act
        engine.PublishEvent(id, "UserApproved");
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var workInput = AsMap(captured["Work"]);
        Assert.Equal("prepared", workInput["Prepare"]);
        Assert.True(Assert.IsType<bool>(workInput["AskUser"]));
    }

    /// <summary>
    /// 受注の並行処理：Fork の枝が 2 ノード（在庫確保 → 確定）と 1 ノード（決済）。
    /// 各枝が <c>$.vars.order</c> の別のキーに書いた値は、Join 後に Fork 前の値とともに後続から読める。
    /// </summary>
    [Fact]
    public async Task Start_ForkJoinWithMultiNodeBranch_MergesBranchWritesAfterJoin()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: OrderParallel
            states:
              Receive:
                output: $.vars.order
                on:
                  Completed:
                    fork: [Reserve, Charge]
              Reserve:
                input:
                  orderId: $.vars.order.id
                on:
                  Completed:
                    next: ConfirmReserve
              ConfirmReserve:
                output: $.vars.order.reservation
                on:
                  Completed:
                    next: Join1
              Charge:
                output: $.vars.order.payment
                on:
                  Completed:
                    next: Join1
              Join1:
                join:
                  all: [Reserve, Charge]
                on:
                  Joined:
                    next: Summary
              Summary:
                input:
                  order: $.vars.order
                  reserved: $.states.ConfirmReserve.output
                on:
                  Completed:
                    end: true
            """;
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var states = new StateTable()
            .Add("Receive", _ => new Dictionary<string, object?> { ["id"] = "ORD-1" })
            .Add("Reserve", input => AsMap(input)["orderId"])
            .Add("ConfirmReserve", input => $"reserved:{input}")
            .Add("Charge", _ => "paid")
            .Add("Summary", input => { captured["Summary"] = input; return null; });
        using var engine = ExecutionEngineTestHarness.Create();

        // Act
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var summaryInput = AsMap(captured["Summary"]);
        var order = AsMap(summaryInput["order"]);
        Assert.Equal("ORD-1", order["id"]);
        Assert.Equal("reserved:ORD-1", order["reservation"]);
        Assert.Equal("paid", order["payment"]);
        Assert.Equal("reserved:ORD-1", summaryInput["reserved"]);
    }

    /// <summary>
    /// 枝の Wait で止まっている実行を checkpoint に保存し（JSON で往復）、別の Engine に読み込んで再開できる。
    /// 再開後の Join で、保存前に終わった枝の書き込みと Wait の枝の書き込みがどちらも親に戻る。
    /// </summary>
    [Fact]
    public async Task ImportCheckpoint_WaitInBranch_ResumesOnAnotherEngineAndJoins()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: ApprovalParallel
            states:
              Request:
                output: $.vars.request
                on:
                  Completed:
                    fork: [AutoCheck, Approval]
              AutoCheck:
                output: $.vars.result.auto
                on:
                  Completed:
                    next: Join1
              Approval:
                wait:
                  events:
                    approve: Record
              Record:
                output: $.vars.result.manual
                on:
                  Completed:
                    next: Join1
              Join1:
                join:
                  all: [AutoCheck, Approval]
                on:
                  Joined:
                    next: Final
              Final:
                input:
                  request: $.vars.request
                  result: $.vars.result
                on:
                  Completed:
                    end: true
            """;
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        StateTable States() => new StateTable()
            .Add("Request", _ => new Dictionary<string, object?> { ["id"] = "REQ-1" })
            .Add("AutoCheck", _ => "ok")
            .AddWait("Approval", "approve")
            .Add("Record", _ => "approved")
            .Add("Final", input => { captured["Final"] = input; return null; });
        using var engine1 = ExecutionEngineTestHarness.Create();
        var id = engine1.Start(Compile(yaml, States()));
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine1, id).Count == 1
            && engine1.ExportCheckpoint(id)?.BranchScopes?.Any(scope => scope.ArrivalOrder is not null) == true);
        var json = JsonSerializer.Serialize(engine1.ExportCheckpoint(id), JsonOptions);
        var waitNodeId = ActiveWaitNodeIds(engine1, id)[0];
        Assert.True(engine1.Unload(id));

        // Act
        using var engine2 = ExecutionEngineTestHarness.Create();
        engine2.ImportCheckpoint(Compile(yaml, States()), JsonSerializer.Deserialize<ExecutionRuntimeCheckpoint>(json, JsonOptions)!);
        engine2.ResumeWaitNode(id, waitNodeId, "approve");
        await WaitUntilAsync(() => engine2.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var finalInput = AsMap(captured["Final"]);
        Assert.Equal("REQ-1", ReadString(AsMapOrJson(finalInput["request"]), "id"));
        var result = AsMapOrJson(finalInput["result"]);
        Assert.Equal("ok", ReadString(result, "auto"));
        Assert.Equal("approved", ReadString(result, "manual"));
    }

    /// <summary>Wait で止まっている実行を取り消すと、実行は Cancelled で終わり Wait の遷移先へ進まない。</summary>
    [Fact]
    public async Task CancelAsync_WhileWaiting_CancelsExecution()
    {
        // Arrange
        const string yaml = """
            workflow:
              name: CancelWait
            states:
              Hold:
                wait:
                  events:
                    go: Done
              Done:
                on:
                  Completed:
                    end: true
            """;
        var done = false;
        var states = new StateTable()
            .AddWait("Hold", "go")
            .Add("Done", _ => { done = true; return null; });
        using var engine = ExecutionEngineTestHarness.Create();
        var id = engine.Start(Compile(yaml, states));
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine, id).Count == 1);

        // Act
        await engine.CancelAsync(id);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsTerminal: true });

        // Assert
        Assert.True(engine.GetSnapshot(id)!.IsCancelled);
        Assert.False(done);
    }

    /// <summary>YAML を読み、検証を通し、登録した状態でコンパイルする（利用者の手順そのもの）。</summary>
    /// <param name="yaml">States 形式の定義。</param>
    /// <param name="states">状態名 → 実行器。</param>
    /// <param name="requireStateExecutors">false のとき、実行器の無い状態があってもコンパイルを通す（実行時の失敗を見るテスト用）。</param>
    private static CompiledWorkflowDefinition Compile(string yaml, StateTable states, bool requireStateExecutors = true)
    {
        var definition = new StateWorkflowDefinitionLoader().Load(yaml);
        var validation = DefinitionValidator.Validate(definition);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        return new DefinitionCompiler(new DictionaryStateExecutorFactory(states.Executors), requireStateExecutors).Compile(definition);
    }

    private static IReadOnlyDictionary<string, object?> AsMap(object? value) =>
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);

    /// <summary>checkpoint 往復後の値は JSON 要素で戻ることがあるため、辞書か JSON 要素のどちらでも読めるようにする。</summary>
    private static object AsMapOrJson(object? value) => value switch
    {
        IReadOnlyDictionary<string, object?> map => map,
        JsonElement { ValueKind: JsonValueKind.Object } element => element,
        _ => throw new Xunit.Sdk.XunitException($"Expected an object but was {value!.GetType().Name ?? "null"}."),
    };

    private static string? ReadString(object container, string key) => container switch
    {
        IReadOnlyDictionary<string, object?> map => map[key]?.ToString(),
        JsonElement element => element.GetProperty(key).GetString(),
        _ => null,
    };

    /// <summary>実行グラフのノードの状態名を、グラフに出た順に返す。</summary>
    private static List<string> NodeNames(ExecutionEngine engine, string executionId)
    {
        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(executionId));
        if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
            return [];
        return nodes.EnumerateArray()
            .Select(node => node.GetProperty("nodeName").GetString()!)
            .ToList();
    }

    private static List<string> ActiveWaitNodeIds(ExecutionEngine engine, string executionId)
    {
        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(executionId));
        if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
            return [];
        return nodes.EnumerateArray()
            .Where(node => node.TryGetProperty("nodeType", out var type) && type.GetString() == "Wait"
                && (!node.TryGetProperty("completedAt", out var completedAt) || completedAt.ValueKind is JsonValueKind.Null))
            .Select(node => node.GetProperty("nodeId").GetString()!)
            .ToList();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }

        Assert.Fail("Condition was not met within the timeout.");
    }

    /// <summary>利用者が状態名ごとに登録する実行器の表。</summary>
    private sealed class StateTable
    {
        /// <summary>状態名 → 実行器（大文字小文字を区別しない）。</summary>
        public Dictionary<string, IStateExecutor> Executors { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>input を受けて output を返す同期の状態を登録する。</summary>
        public StateTable Add(string name, Func<object?, object?> run)
        {
            Executors[name] = new DefaultStateExecutor((_, input, _) => Task.FromResult(run(input)));
            return this;
        }

        /// <summary>StateContext を使う非同期の状態を登録する。</summary>
        public StateTable Add(string name, Func<StateContext, object?, CancellationToken, Task<object?>> run)
        {
            Executors[name] = new DefaultStateExecutor(run);
            return this;
        }

        /// <summary>
        /// <c>wait.events</c> の状態を登録する。ノード単位でイベントを待ち、受け取ったイベント名を <c>event</c> に載せて返す。
        /// </summary>
        public StateTable AddWait(string name, params string[] eventNames) =>
            Add(name, async (ctx, _, ct) =>
            {
                var eventName = await ctx.Events.WaitForEventAsync(ctx.NodeId!, eventNames, ct);
                return new Dictionary<string, object?> { ["event"] = eventName };
            });
    }
}
