using System.Collections.Concurrent;
using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.FSM;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// Engine 単体（Fork 展開ハンドラ未登録）の Fork / Join が Hosted と同じ契約で動くことを検証する。
/// </summary>
/// <remarks>
/// Join は各枝が Join に入った時点で成立し、Join の input は「枝の先頭の名前 → Join に入った状態の output」。
/// 枝の Context は Fork 到達時点の写しで、兄弟の書き込みは見えない。Join では枝が書いたパスだけを、枝の完了順に後勝ちで戻す。
/// </remarks>
public sealed class ExecutionEngineInProcessForkJoinTests
{
    /// <summary>
    /// 2 ノードの枝（A1→A2、B1→B2）では、両方の枝の最後の状態の後に Join が成立し、
    /// 最後に完了した先頭（B1）の後続 B2 も実行される。Join の input は先頭の名前 → 最後の状態の output。
    /// </summary>
    [Fact]
    public async Task Start_TwoNodeBranches_JoinsAfterLastStateOfEachBranch()
    {
        // Arrange
        var a1Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured)
            .State("A1", _ => { a1Done.TrySetResult(); return "a1"; }, next: "A2")
            .StateAsync("B1", async _ => { await a1Done.Task; return "b1"; }, next: "B2")
            .State("A2", _ => "a2", next: "Join1")
            .State("B2", input => { captured["B2"] = input; return "b2"; }, next: "Join1")
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        Assert.True(captured.ContainsKey("B2"));
        var joinInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        Assert.Equal("a2", joinInput["A1"]);
        Assert.Equal("b2", joinInput["B1"]);
        Assert.Equal(2, joinInput.Count);
    }

    /// <summary>枝 B は、枝 A が Join 前に書いた <c>$.vars.a</c> を読めない（兄弟参照不可）。</summary>
    [Fact]
    public async Task Start_SiblingBranchWriteBeforeJoin_IsNotVisible()
    {
        // Arrange
        var aWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured)
            .State("A1", _ => "from-a", next: "A2", output: "$.vars.a")
            .State("A2", _ => { aWritten.TrySetResult(); return "a2"; }, next: "Join1")
            .StateAsync("B1", async _ => { await aWritten.Task; return "b1"; }, next: "B2")
            .State("B2", input => { captured["B2"] = input; return "b2"; }, next: "Join1", input: new() { ["seen"] = "$.vars.a" })
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var b2Input = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["B2"]);
        Assert.False(b2Input.TryGetValue("seen", out var seen) && Equals(seen, "from-a"));
    }

    /// <summary>枝の 2 番目のノードでも、Fork 前に親が書いた <c>$.vars</c> / <c>$.states</c> を読める。</summary>
    [Fact]
    public async Task Start_BranchSecondNode_ReadsParentValuesWrittenBeforeFork()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured, startOutput: () => new Dictionary<string, object?> { ["id"] = "o-1" })
            .State("A1", _ => "a1", next: "A2")
            .State("A2", _ => "a2", next: "Join1")
            .State("B1", _ => "b1", next: "B2")
            .State("B2", input => { captured["B2"] = input; return "b2"; }, next: "Join1", input: new()
            {
                ["fromVars"] = "$.vars.order.id",
                ["fromStates"] = "$.states.Start.output.id"
            })
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var b2Input = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["B2"]);
        Assert.Equal("o-1", b2Input["fromVars"]);
        Assert.Equal("o-1", b2Input["fromStates"]);
    }

    /// <summary>枝 A の <c>order.status</c> と枝 B の <c>order.total</c> は、Fork 前の <c>order.id</c> とともに Join 後の親にすべて残る。</summary>
    [Fact]
    public async Task Start_BranchesWriteDifferentPaths_JoinKeepsAllPaths()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(
                captured,
                startOutput: () => new Dictionary<string, object?> { ["id"] = "o-1" },
                afterInput: new() { ["order"] = "$.vars.order" })
            .State("A1", _ => "a1", next: "A2")
            .State("A2", _ => "charged", next: "Join1", output: "$.vars.order.status")
            .State("B1", _ => "b1", next: "B2")
            .State("B2", _ => 100, next: "Join1", output: "$.vars.order.total")
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var afterInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        var order = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(afterInput["order"]);
        Assert.Equal("o-1", order["id"]);
        Assert.Equal("charged", order["status"]);
        Assert.Equal(100, order["total"]);
    }

    /// <summary>
    /// 枝 A と枝 B が同じ <c>$.vars.x</c> を書くと、後に完了した枝が勝つ。
    /// 宣言順で後の枝 B を先に完了させ、宣言順ではなく完了順で決まることを確かめる。
    /// </summary>
    [Fact]
    public async Task Start_BranchesWriteSamePath_LastCompletedBranchWins()
    {
        // Arrange
        var bDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured, afterInput: new() { ["x"] = "$.vars.x" })
            .StateAsync("A1", async _ => { await bDone.Task; await Task.Delay(200); return "a1"; }, next: "A2")
            .State("A2", _ => "a", next: "Join1", output: "$.vars.x")
            .State("B1", _ => "b1", next: "B2")
            .State("B2", _ => { bDone.TrySetResult(); return "b"; }, next: "Join1", output: "$.vars.x")
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var afterInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        Assert.Equal("a", afterInput["x"]);
    }

    /// <summary>Join の後の状態は、枝で完了した状態の <c>$.states.&lt;名前&gt;.output</c> を読める。</summary>
    [Fact]
    public async Task Start_AfterJoin_ReadsBranchStateOutputFromStates()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured, afterInput: new() { ["a2"] = "$.states.A2.output", ["b1"] = "$.states.B1.output" })
            .State("A1", _ => "a1", next: "A2")
            .State("A2", _ => "a2-out", next: "Join1")
            .State("B1", _ => "b1-out", next: "B2")
            .State("B2", _ => "b2", next: "Join1")
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var afterInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        Assert.Equal("a2-out", afterInput["a2"]);
        Assert.Equal("b1-out", afterInput["b1"]);
    }

    /// <summary>枝の先頭が失敗し <c>on.Failed</c> で枝の中を進んでから Join に入っても、Join は成立する（Hosted と同じ）。</summary>
    [Fact]
    public async Task Start_HeadFailsAndRecoversViaOnFailed_JoinStillCompletes()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured)
            .State("A1", _ => throw new InvalidOperationException("a1 failed"), onFailed: "A2")
            .State("A2", _ => "recovered", next: "Join1")
            .State("B1", _ => "b1", next: "Join1")
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var joinInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        Assert.Equal("recovered", joinInput["A1"]);
        Assert.Equal("b1", joinInput["B1"]);
    }

    /// <summary>枝の最後が Wait のとき、イベントで Join に入った時点でその枝を記録し、Join が成立する。</summary>
    [Fact]
    public async Task ResumeWaitNode_BranchEndsWithWait_JoinsAfterEvent()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured)
            .Wait("A1", "approve", next: "Join1")
            .State("B1", _ => "b1", next: "Join1")
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        var id = engine.Start(def);
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine, id).Count == 1);

        // Act
        engine.ResumeWaitNode(id, ActiveWaitNodeIds(engine, id)[0], "approve");
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var joinInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        var waitOutput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(joinInput["A1"]);
        Assert.Equal("approve", waitOutput["event"]);
        Assert.Equal("b1", joinInput["B1"]);
    }

    /// <summary>
    /// 枝の中のネストした Fork: 内側の Join は内側の枝だけを待ち、内側で書いた値は外側の枝を通って外側の Join の後に見える。
    /// 外側の Join の input は外側の先頭の名前 → 外側の Join に入った状態の output。
    /// </summary>
    [Fact]
    public async Task Start_NestedFork_InnerWritesReachOuterJoin()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = new DefinitionBuilder("Start")
            .Fork("Start", "A", "B")
            .Fork("A", "A1", "A2")
            .State("A1", _ => "inner-1", next: "InnerJoin", output: "$.vars.inner1")
            .State("A2", _ => "inner-2", next: "InnerJoin", output: "$.vars.inner2")
            .Join("InnerJoin", ["A1", "A2"], next: "A3")
            .State("A3", _ => "a3", next: "OuterJoin")
            .State("B", _ => "b", next: "OuterJoin")
            .Join("OuterJoin", ["A", "B"], next: "After")
            .State("After", input => { captured["After"] = input; return null; }, end: true, input: new()
            {
                ["inner1"] = "$.vars.inner1",
                ["inner2"] = "$.vars.inner2",
                ["a3"] = "$.states.A3.output"
            })
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var afterInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        Assert.Equal("inner-1", afterInput["inner1"]);
        Assert.Equal("inner-2", afterInput["inner2"]);
        Assert.Equal("a3", afterInput["a3"]);
    }

    /// <summary>
    /// 枝の Wait で止まっている間に checkpoint を JSON で往復し、別の Engine で再開しても、
    /// 枝の Context と Join に到着済みの枝の書き込みが落ちずに Join の後へ届く。
    /// </summary>
    [Fact]
    public async Task ImportCheckpoint_WhileBranchWaits_ResumesWithBranchContextAndArrivals()
    {
        // Arrange
        var captured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var def = TwoBranchDefinition(captured, afterInput: new() { ["a"] = "$.vars.a", ["b"] = "$.vars.b", ["fromWait"] = "$.vars.seen" })
            .State("A1", _ => "from-a", next: "WaitA", output: "$.vars.a")
            .Wait("WaitA", "go", next: "Join1")
            .State("B1", _ => "from-b", next: "Join1", output: "$.vars.b")
            .Build();
        using var engine1 = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        var id = engine1.Start(def);
        await WaitUntilAsync(() => ActiveWaitNodeIds(engine1, id).Count == 1
            && engine1.ExportCheckpoint(id)?.BranchScopes?.Any(scope => scope.ArrivalOrder is not null) == true);
        var waitNodeId = ActiveWaitNodeIds(engine1, id)[0];
        var json = JsonSerializer.Serialize(engine1.ExportCheckpoint(id), JsonOptions);
        engine1.Unload(id);
        var checkpoint = JsonSerializer.Deserialize<ExecutionRuntimeCheckpoint>(json, JsonOptions)!;
        using var engine2 = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        engine2.ImportCheckpoint(def, checkpoint);

        // Act
        engine2.ResumeWaitNode(id, waitNodeId, "go");
        await WaitUntilAsync(() => engine2.GetSnapshot(id) is { IsCompleted: true });

        // Assert
        var afterInput = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(captured["After"]);
        Assert.Equal("from-a", afterInput["a"]?.ToString());
        Assert.Equal("from-b", afterInput["b"]?.ToString());
    }

    /// <summary>対になる Join が無い Fork（検証を通らない定義）は、実行を終端失敗にする。</summary>
    [Fact]
    public async Task Start_ForkWithoutPairedJoin_FailsExecution()
    {
        // Arrange
        var def = new DefinitionBuilder("Start")
            .Fork("Start", "A", "B")
            .State("A", _ => "a", end: true)
            .State("B", _ => "b", end: true)
            .Build();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsFailed: true });

        // Assert
        Assert.False(engine.GetSnapshot(id)!.IsCompleted);
    }

    /// <summary>
    /// 同じ定義を Engine 単体と Hosted（テスト用ハンドラで子を同じプロセスの別 Engine で走らせ、完了順に物理 Join する）で実行すると、
    /// Join の input と Join 後の親の <c>$.vars</c> / <c>$.states</c> が一致する。
    /// </summary>
    /// <param name="scenario">2 ノードの枝 / 別パスの書き込み / 同じパスの書き込み。</param>
    [Theory]
    [InlineData("two-node")]
    [InlineData("different-paths")]
    [InlineData("same-path")]
    public async Task Start_SameDefinition_InProcessAndHostedAgree(string scenario)
    {
        // Arrange
        var inProcessCaptured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var hostedCaptured = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        using var inProcess = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var inProcessId = inProcess.Start(ParityDefinition(scenario, inProcessCaptured));
        await WaitUntilAsync(() => inProcess.GetSnapshot(inProcessId) is { IsCompleted: true });
        await RunHostedAsync(ParityDefinition(scenario, hostedCaptured));

        // Assert
        Assert.Equal(
            JsonSerializer.Serialize(hostedCaptured["After"], JsonOptions),
            JsonSerializer.Serialize(inProcessCaptured["After"], JsonOptions));
    }

    /// <summary>
    /// Engine 単体と Hosted で比べる定義。After は Join の input（<c>$.states.Join1.output</c>）と親の <c>vars</c> / <c>states</c> を受け取る。
    /// </summary>
    private static CompiledWorkflowDefinition ParityDefinition(string scenario, ConcurrentDictionary<string, object?> captured)
    {
        var afterInput = new Dictionary<string, string>
        {
            ["joinA"] = "$.states.Join1.output.A1",
            ["joinB"] = "$.states.Join1.output.B1",
            ["order"] = "$.vars.order",
            ["x"] = "$.vars.x",
            ["a2"] = "$.states.A2.output"
        };
        var definition = TwoBranchDefinition(captured, startOutput: () => new Dictionary<string, object?> { ["id"] = "o-1" }, afterInput: afterInput);
        var bDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return (scenario switch
        {
            "two-node" => definition
                .State("A1", _ => "a1", next: "A2")
                .State("A2", _ => "a2", next: "Join1")
                .State("B1", _ => "b1", next: "B2")
                .State("B2", _ => "b2", next: "Join1"),
            "different-paths" => definition
                .State("A1", _ => "a1", next: "A2")
                .State("A2", _ => "charged", next: "Join1", output: "$.vars.order.status")
                .State("B1", _ => "b1", next: "B2")
                .State("B2", _ => 100, next: "Join1", output: "$.vars.order.total"),
            "same-path" => definition
                .StateAsync("A1", async _ => { await bDone.Task; await Task.Delay(200); return "a1"; }, next: "A2")
                .State("A2", _ => "a", next: "Join1", output: "$.vars.x")
                .State("B1", _ => "b1", next: "B2")
                .State("B2", _ => { bDone.TrySetResult(); return "b"; }, next: "Join1", output: "$.vars.x"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        }).Build();
    }

    /// <summary>
    /// Hosted を同じプロセスで再現する。親の Fork で子を別 Engine で開始し（子は Join に入ると子として完了する）、
    /// 子の完了順に、子の終端 output と書いた値（<see cref="WorkflowExecutionContext.ExportBranchWrites(CheckpointContextData)"/>）で物理 Join する。
    /// </summary>
    private static async Task RunHostedAsync(CompiledWorkflowDefinition definition)
    {
        using var parent = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        using var children = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        children.SetForkExpansionHandler(_ => Task.CompletedTask);
        var expansion = new TaskCompletionSource<ForkExpansionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        parent.SetForkExpansionHandler(evt =>
        {
            expansion.TrySetResult(evt);
            return Task.CompletedTask;
        });
        var parentId = parent.Start(definition);
        var fork = await expansion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var running = fork.Branches
            .Select(plan => (Head: plan.BranchState, Id: children.Start(
                definition,
                input: plan.MappedInput,
                initialState: plan.BranchState,
                forkBranch: new ForkBranchStart(fork.WorkflowInput, fork.ParentContext))))
            .ToList();
        var completed = new List<(string Head, ExecutionRuntimeCheckpoint Checkpoint)>();
        await WaitUntilAsync(() =>
        {
            completed.AddRange(running
                .Where(child => completed.All(done => done.Head != child.Head)
                    && children.GetSnapshot(child.Id) is { IsCompleted: true })
                .Select(child => (child.Head, children.ExportCheckpoint(child.Id)!)));
            return completed.Count == running.Count;
        });

        parent.CompletePhysicalJoin(
            parentId,
            "Join1",
            completed.ToDictionary(done => done.Head, done => (object?)done.Checkpoint.Context.WorkflowOutput),
            completed.Select(done => WorkflowExecutionContext.ExportBranchWrites(done.Checkpoint.Context)).ToList());
        await WaitUntilAsync(() => parent.GetSnapshot(parentId) is { IsCompleted: true });
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <c>Start</c>（<c>output: $.vars.order</c>）→ Fork [A1, B1] → … → <c>Join1</c> → <c>After</c>（受け取った input を記録）→ 終了、の骨組み。
    /// 枝の状態は呼び出し側で足す。
    /// </summary>
    /// <param name="captured">状態が受け取った input の記録先（After は <c>captured["After"]</c>）。</param>
    /// <param name="startOutput">Start の output を作る関数。省略時は <c>"start"</c>。</param>
    /// <param name="afterInput">After の input 写像（キー → パス）。省略時は Join の input をそのまま受け取る。</param>
    private static DefinitionBuilder TwoBranchDefinition(
        ConcurrentDictionary<string, object?> captured,
        Func<object?>? startOutput = null,
        Dictionary<string, string>? afterInput = null) =>
        new DefinitionBuilder("Start")
            .Fork("Start", ["A1", "B1"], output: "$.vars.order", run: _ => startOutput?.Invoke() ?? "start")
            .Join("Join1", ["A1", "B1"], next: "After")
            .State("After", input => { captured["After"] = input; return null; }, end: true, input: afterInput);

    private static List<string> ActiveWaitNodeIds(ExecutionEngine engine, string executionId)
    {
        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(executionId));
        return doc.RootElement.GetProperty("nodes").EnumerateArray()
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

    /// <summary>テスト用の <see cref="CompiledWorkflowDefinition"/> を状態ごとに組み立てる。</summary>
    /// <param name="initialState">開始状態名。</param>
    private sealed class DefinitionBuilder(string initialState)
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>> _transitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IStateExecutor> _executors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _forks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _joins = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>> _waits = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, StateInputDefinition> _inputs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _outputs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>同期の状態を足す。</summary>
        public DefinitionBuilder State(
            string name,
            Func<object?, object?> run,
            string? next = null,
            string? onFailed = null,
            bool end = false,
            string? output = null,
            Dictionary<string, string>? input = null) =>
            StateAsync(name, value => Task.FromResult(run(value)), next, onFailed, end, output, input);

        /// <summary>非同期の状態を足す。</summary>
        public DefinitionBuilder StateAsync(
            string name,
            Func<object?, Task<object?>> run,
            string? next = null,
            string? onFailed = null,
            bool end = false,
            string? output = null,
            Dictionary<string, string>? input = null)
        {
            var transitions = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase);
            if (next is not null)
                transitions[Fact.Completed] = new TransitionTarget { Next = next };
            if (end)
                transitions[Fact.Completed] = new TransitionTarget { End = true };
            if (onFailed is not null)
                transitions[Fact.Failed] = new TransitionTarget { Next = onFailed };
            _transitions[name] = transitions;
            _executors[name] = new DefaultStateExecutor((_, value, _) => run(value));
            if (output is not null)
                _outputs[name] = output;
            if (input is not null)
            {
                _inputs[name] = new StateInputDefinition
                {
                    Values = input.ToDictionary(kv => kv.Key, kv => new StateInputValueDefinition { Path = kv.Value })
                };
            }

            return this;
        }

        /// <summary>完了で Fork する状態を足す。</summary>
        public DefinitionBuilder Fork(string name, params string[] heads) => Fork(name, heads, output: null, run: null);

        /// <summary>完了で Fork する状態を足す（output と実行本体を指定）。</summary>
        public DefinitionBuilder Fork(string name, string[] heads, string? output, Func<object?, object?>? run)
        {
            _transitions[name] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                [Fact.Completed] = new TransitionTarget { Fork = heads }
            };
            _forks[name] = heads;
            var body = run ?? (_ => name);
            _executors[name] = new DefaultStateExecutor((_, value, _) => Task.FromResult(body(value)));
            if (output is not null)
                _outputs[name] = output;
            return this;
        }

        /// <summary>Join 状態を足す。</summary>
        public DefinitionBuilder Join(string name, string[] heads, string next)
        {
            _transitions[name] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                [Fact.Joined] = new TransitionTarget { Next = next }
            };
            _joins[name] = heads;
            return this;
        }

        /// <summary>1 つのイベントで次へ進む Wait 状態を足す。</summary>
        public DefinitionBuilder Wait(string name, string eventName, string next)
        {
            _transitions[name] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase);
            _waits[name] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [eventName] = new WaitEventRouteDefinition { Next = next }
            };
            _executors[name] = new DefaultStateExecutor(async (ctx, _, ct) =>
            {
                var received = await ctx.Events.WaitForEventAsync(ctx.NodeId!, [eventName], ct);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["event"] = received };
            });
            return this;
        }

        /// <summary>定義を組み立てる。</summary>
        public CompiledWorkflowDefinition Build() =>
            new()
            {
                Name = "InProcessForkJoin",
                Transitions = _transitions,
                ForkTable = _forks,
                JoinTable = _joins,
                WaitEventRouteTable = _waits,
                StateInputs = _inputs,
                StateOutputs = _outputs,
                InitialState = initialState,
                StateExecutorFactory = new DictionaryStateExecutorFactory(_executors)
            };
    }
}
