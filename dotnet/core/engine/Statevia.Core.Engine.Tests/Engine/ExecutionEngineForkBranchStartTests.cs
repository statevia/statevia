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
            forkBranch: new ForkBranchStart(CreateWorkflowInput()));
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
            forkBranch: new ForkBranchStart(CreateWorkflowInput()));
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

    private static Dictionary<string, object?> CreateWorkflowInput() => new()
    {
        ["billingUrl"] = "https://pay.example",
        ["orderId"] = "o-1"
    };

    /// <summary>
    /// 枝の先頭 <c>Charge</c>（<c>url: $.input.billingUrl</c>）から <c>Notify</c>（<c>orderId: $.input.orderId</c>）へ進む定義。
    /// </summary>
    private static CompiledWorkflowDefinition CreateBranchDefinition(IStateExecutor charge, IStateExecutor notify) => new()
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
        StateInputs = new Dictionary<string, StateInputDefinition>(StringComparer.OrdinalIgnoreCase)
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

    private static CompiledWorkflowDefinition CreateForkDefinition() => new()
    {
        Name = "ForkBranchStartParent",
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
