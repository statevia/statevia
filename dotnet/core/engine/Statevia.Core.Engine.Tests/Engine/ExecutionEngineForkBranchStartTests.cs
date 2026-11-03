using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// Hosted Fork の枝（子 execution）としての開始。枝の先頭の input を再評価せず、<c>$.input</c> を親の開始 input にそろえる。
/// </summary>
public sealed class ExecutionEngineForkBranchStartTests
{
    /// <summary>
    /// 枝として開始すると、先頭状態の input 写像を子の Context で再評価せず、親で評価済みの値をそのまま渡す。
    /// </summary>
    [Fact]
    public async Task Start_WithForkBranch_PassesMappedInputWithoutReevaluation()
    {
        // Arrange
        object? branchHeadInput = "unset";
        var def = CreateBranchDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                branchHeadInput = input;
                return Task.FromResult<object?>(null);
            })),
            DefaultStateExecutor.Create(new ImmediateState()));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var mappedInput = new Dictionary<string, object?> { ["url"] = "https://pay.example" };

        // Act
        var id = engine.Start(
            def,
            input: mappedInput,
            initialState: "Charge",
            forkBranch: new ForkBranchStart(CreateWorkflowInput(), new ForkParentContext(null, null, "parent-1")));
        await WaitUntilCompletedAsync(engine, id);

        // Assert
        Assert.Same(mappedInput, branchHeadInput);
    }

    /// <summary>
    /// 枝の 2 番目以降の状態でも、<c>$.input</c> は枝の input ではなく親のワークフロー開始 input に解決する。
    /// </summary>
    [Fact]
    public async Task Start_WithForkBranch_ResolvesDollarInputToWorkflowInputInLaterState()
    {
        // Arrange
        object? laterInput = "unset";
        var def = CreateBranchDefinition(
            DefaultStateExecutor.Create(new ImmediateState()),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                laterInput = input;
                return Task.FromResult<object?>(null);
            })));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(
            def,
            input: new Dictionary<string, object?> { ["url"] = "https://pay.example" },
            initialState: "Charge",
            forkBranch: new ForkBranchStart(CreateWorkflowInput(), new ForkParentContext(null, null, "parent-1")));
        await WaitUntilCompletedAsync(engine, id);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(laterInput);
        Assert.Equal("o-1", dict["orderId"]);
    }

    /// <summary>通常の開始では、先頭状態の input 写像を開始 input に対して評価する。</summary>
    [Fact]
    public async Task Start_WithoutForkBranch_AppliesStateInputToStartInput()
    {
        // Arrange
        object? headInput = "unset";
        var def = CreateBranchDefinition(
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                headInput = input;
                return Task.FromResult<object?>(null);
            })),
            DefaultStateExecutor.Create(new ImmediateState()));
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def, input: CreateWorkflowInput(), initialState: "Charge");
        await WaitUntilCompletedAsync(engine, id);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(headInput);
        Assert.Equal("https://pay.example", dict["url"]);
    }

    /// <summary>Hosted の Fork 展開通知には、親のワークフロー開始 input が載る。</summary>
    [Fact]
    public async Task ForkExpansion_CarriesParentWorkflowInput()
    {
        // Arrange
        var def = CreateForkDefinition();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var expanded = new TaskCompletionSource<ForkExpansionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(evt =>
        {
            expanded.TrySetResult(evt);
            return Task.CompletedTask;
        });
        var workflowInput = CreateWorkflowInput();

        // Act
        engine.Start(def, input: workflowInput);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var evt = await expanded.Task.WaitAsync(timeout.Token);

        // Assert
        Assert.Same(workflowInput, evt.WorkflowInput);
    }

    /// <summary>
    /// 枝の 2 番目以降の状態でも、Fork 到達時点の親の <c>$.vars</c> / <c>$.states</c> を同じパスで読める（payload 経由の JSON を想定）。
    /// </summary>
    [Fact]
    public async Task Start_WithForkBranch_LaterStateReadsInheritedVarsAndStates()
    {
        // Arrange
        object? laterInput = "unset";
        var def = CreateInheritingBranchDefinition(
            new Dictionary<string, StateInputValueDefinition>
            {
                ["orderId"] = new() { Path = "$.vars.order.orderId" },
                ["received"] = new() { Path = "$.states.Receive.output.x" }
            },
            input => laterInput = input);
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        using var vars = System.Text.Json.JsonDocument.Parse("""{"order":{"orderId":"o-1"}}""");
        using var states = System.Text.Json.JsonDocument.Parse("""{"Receive":{"output":{"x":"r-1"}}}""");

        // Act
        var id = engine.Start(
            def,
            input: null,
            initialState: "Charge",
            forkBranch: new ForkBranchStart(null, new ForkParentContext(vars.RootElement, states.RootElement, "parent-1")));
        await WaitUntilCompletedAsync(engine, id);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(laterInput);
        Assert.Equal("o-1", dict["orderId"]);
        Assert.Equal("r-1", dict["received"]);
    }

    /// <summary>枝の中の <c>$.sys.execution.id</c> は、子実行自身の ID ではなく親の論理実行 ID になる。</summary>
    [Fact]
    public async Task Start_WithForkBranch_SysExecutionIdIsLogicalExecutionId()
    {
        // Arrange
        object? laterInput = "unset";
        var def = CreateInheritingBranchDefinition(
            new Dictionary<string, StateInputValueDefinition> { ["id"] = new() { Path = "$.sys.execution.id" } },
            input => laterInput = input);
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(
            def,
            executionId: "child-1",
            initialState: "Charge",
            forkBranch: new ForkBranchStart(null, new ForkParentContext(null, null, "parent-1")));
        await WaitUntilCompletedAsync(engine, id);

        // Assert
        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(laterInput);
        Assert.Equal("parent-1", dict["id"]);
    }

    /// <summary>展開通知に、Fork 到達時点の親の vars / states と論理実行 ID が載る。</summary>
    [Fact]
    public async Task ForkExpansion_CarriesParentVarsStatesAndLogicalExecutionId()
    {
        // Arrange
        var def = CreateForkDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Start"] = "$.vars.seed" });
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var expanded = new TaskCompletionSource<ForkExpansionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(evt =>
        {
            expanded.TrySetResult(evt);
            return Task.CompletedTask;
        });

        // Act
        var parentId = engine.Start(def, executionId: "parent-1");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var evt = await expanded.Task.WaitAsync(timeout.Token);

        // Assert
        Assert.Equal(parentId, evt.ParentContext.LogicalExecutionId);
        var vars = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(evt.ParentContext.Vars);
        Assert.True(vars.ContainsKey("seed"));
        var states = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(evt.ParentContext.States);
        Assert.True(states.ContainsKey("Start"));
    }

    /// <summary>
    /// 枝の先頭 <c>Charge</c> から <c>Notify</c>（指定した input 写像）へ進む定義。<c>Notify</c> は受け取った input を記録する。
    /// </summary>
    private static CompiledWorkflowDefinition CreateInheritingBranchDefinition(
        Dictionary<string, StateInputValueDefinition> notifyInput,
        Action<object?> captureNotifyInput) =>
        CreateBranchDefinition(
            DefaultStateExecutor.Create(new ImmediateState()),
            DefaultStateExecutor.Create(new DelegateState((_, input, _) =>
            {
                captureNotifyInput(input);
                return Task.FromResult<object?>(null);
            })),
            new Dictionary<string, StateInputDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Notify"] = new StateInputDefinition { Values = notifyInput }
            });

    private static Dictionary<string, object?> CreateWorkflowInput() => new()
    {
        ["billingUrl"] = "https://pay.example",
        ["orderId"] = "o-1"
    };

    /// <summary>
    /// 枝の先頭 <c>Charge</c>（<c>url: $.input.billingUrl</c>）から <c>Notify</c>（<c>orderId: $.input.orderId</c>）へ進む定義。
    /// </summary>
    /// <param name="charge">Charge の実行器。</param>
    /// <param name="notify">Notify の実行器。</param>
    /// <param name="stateInputs">input 写像。省略時は上記の既定。</param>
    private static CompiledWorkflowDefinition CreateBranchDefinition(
        IStateExecutor charge,
        IStateExecutor notify,
        Dictionary<string, StateInputDefinition>? stateInputs = null) => new()
        {
            Name = "ForkBranchStart",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Charge"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Notify" } },
                ["Notify"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
            StateInputs = stateInputs ?? new Dictionary<string, StateInputDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Charge"] = new StateInputDefinition
                {
                    Values = new Dictionary<string, StateInputValueDefinition>
                    {
                        ["url"] = new StateInputValueDefinition { Path = "$.input.billingUrl" }
                    }
                },
                ["Notify"] = new StateInputDefinition
                {
                    Values = new Dictionary<string, StateInputValueDefinition>
                    {
                        ["orderId"] = new StateInputValueDefinition { Path = "$.input.orderId" }
                    }
                }
            },
            InitialState = "Charge",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Charge"] = charge,
                ["Notify"] = notify
            })
        };

    /// <summary>Start から A / B へ Fork し、Join1 で合流する親の定義。</summary>
    /// <param name="stateOutputs">状態の output を書く vars のパス。省略時は無し。</param>
    private static CompiledWorkflowDefinition CreateForkDefinition(Dictionary<string, string>? stateOutputs = null) => new()
    {
        Name = "ForkBranchStartParent",
        StateOutputs = stateOutputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = ["A", "B"] } },
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = ["A", "B"] },
        JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = ["A", "B"] },
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
        InitialState = "Start",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["A"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["B"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private static async Task WaitUntilCompletedAsync(ExecutionEngine engine, string executionId, int maxMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (DateTime.UtcNow < deadline)
        {
            if (engine.GetSnapshot(executionId) is { IsCompleted: true })
                return;

            await Task.Delay(20);
        }

        Assert.Fail("Execution did not complete in time.");
    }

    /// <summary>受け取った input を委譲先で観測する状態。</summary>
    /// <param name="fn">実行本体。</param>
    private sealed class DelegateState(Func<StateContext, object?, CancellationToken, Task<object?>> fn) : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) => fn(ctx, input, ct);
    }

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }
}
