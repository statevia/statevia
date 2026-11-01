using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.FSM;
using System.Collections.Concurrent;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// 状態の失敗（Failed 事実）で <c>on.Failed</c>（Nodes の <c>error</c>）を辿ること、
/// 遷移が無ければ実行を Failed で終え Cancelled とは記録しないことを検証する。
/// </summary>
public sealed class ExecutionEngineFailedTransitionTests
{
    /// <summary>on.Failed.next を持つ状態が失敗すると、遷移先が実行され実行は Completed で終わる。</summary>
    [Fact]
    public async Task Start_FailedStateWithOnFailedNext_RunsRecoveryAndCompletes()
    {
        // Arrange
        var executed = new ConcurrentQueue<string>();
        var def = CreateDefinition(
            new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Charge"] = Transitions(failed: new TransitionTarget { Next = "Recover" }, completed: new TransitionTarget { End = true }),
                ["Recover"] = Transitions(completed: new TransitionTarget { End = true }),
            },
            new Dictionary<string, IStateExecutor>
            {
                ["Charge"] = Throwing(),
                ["Recover"] = Recording("Recover", executed),
            });
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true } or { IsFailed: true } or { IsCancelled: true });
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
        Assert.False(snapshot.IsCancelled);
        Assert.Equal(["Recover"], executed);
    }

    /// <summary>on.Failed.end を持つ状態が失敗すると、失敗を処理済みとして実行は Completed で終わる。</summary>
    [Fact]
    public async Task Start_FailedStateWithOnFailedEnd_CompletesExecution()
    {
        // Arrange
        var def = CreateDefinition(
            new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Charge"] = Transitions(failed: new TransitionTarget { End = true }, completed: new TransitionTarget { End = true }),
            },
            new Dictionary<string, IStateExecutor> { ["Charge"] = Throwing() });
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true } or { IsFailed: true } or { IsCancelled: true });
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
        Assert.False(snapshot.IsCancelled);
    }

    /// <summary>on.Failed を持たない状態が失敗すると、実行は Failed で終わり Cancelled とは記録しない。</summary>
    [Fact]
    public async Task Start_FailedStateWithoutOnFailed_MarksFailedButNotCancelled()
    {
        // Arrange
        var def = CreateDefinition(
            new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Charge"] = Transitions(completed: new TransitionTarget { End = true }),
            },
            new Dictionary<string, IStateExecutor> { ["Charge"] = Throwing() });
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true } or { IsFailed: true } or { IsCancelled: true });
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsFailed);
        Assert.False(snapshot.IsCancelled);
        Assert.False(snapshot.IsCompleted);
    }

    /// <summary>on.Failed が条件ケースを持つとき、Completed と同じ条件評価で遷移先が決まる。</summary>
    [Fact]
    public async Task Start_FailedStateWithConditionalOnFailed_RoutesByCondition()
    {
        // Arrange
        var executed = new ConcurrentQueue<string>();
        var def = CreateDefinition(
            new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Retry"] = Transitions(completed: new TransitionTarget { End = true }),
                ["Fallback"] = Transitions(completed: new TransitionTarget { End = true }),
            },
            new Dictionary<string, IStateExecutor>
            {
                ["Charge"] = Throwing(),
                ["Retry"] = Recording("Retry", executed),
                ["Fallback"] = Recording("Fallback", executed),
            },
            conditionalTransitions: new Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>
            {
                ["Charge"] = new Dictionary<string, CompiledFactTransition>
                {
                    [Fact.Failed] = new CompiledFactTransition
                    {
                        Cases =
                        [
                            new CompiledTransitionCase
                            {
                                Order = 10,
                                DeclarationIndex = 0,
                                When = new ConditionExpressionDefinition { Path = "$.input.retryable", Op = "eq", Value = true },
                                Target = new TransitionTarget { Next = "Retry" },
                            },
                        ],
                        DefaultTarget = new TransitionTarget { Next = "Fallback" },
                    },
                },
            });
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var input = new Dictionary<string, object?> { ["retryable"] = true };

        // Act
        var id = engine.Start(def, input: input);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsCompleted: true } or { IsFailed: true } or { IsCancelled: true });
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Equal(["Retry"], executed);
    }

    /// <summary>
    /// プロセス内 Fork で一方の枝が on.Failed なしで失敗すると、並行する枝の Action は止まり、
    /// 止まった枝の Cancelled 事実は抑止されて実行は Failed のまま（Cancelled にならない）。
    /// </summary>
    [Fact]
    public async Task Start_FailedBranchWhileSiblingRunning_HaltsSiblingAndStaysFailed()
    {
        // Arrange
        var siblingHalted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var def = CreateDefinition(
            new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = Transitions(completed: new TransitionTarget { Fork = ["Charge", "Ship"] }),
                ["Charge"] = Transitions(completed: new TransitionTarget { Next = "Join1" }),
                ["Ship"] = Transitions(completed: new TransitionTarget { Next = "Join1" }),
                ["Join1"] = new Dictionary<string, TransitionTarget> { [Fact.Joined] = new TransitionTarget { End = true } },
            },
            new Dictionary<string, IStateExecutor>
            {
                ["Start"] = new DefaultStateExecutor((_, _, _) => Task.FromResult<object?>(Unit.Value)),
                ["Charge"] = new DefaultStateExecutor(async (_, _, _) =>
                {
                    await siblingStarted.Task.ConfigureAwait(false);
                    throw new InvalidOperationException("charge failed");
                }),
                ["Ship"] = new DefaultStateExecutor(async (_, _, ct) =>
                {
                    siblingStarted.TrySetResult(true);
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        siblingHalted.TrySetResult(true);
                        throw;
                    }

                    return Unit.Value;
                }),
            },
            initialState: "Start",
            forkTable: new Dictionary<string, IReadOnlyList<string>> { ["Start"] = ["Charge", "Ship"] },
            joinTable: new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = ["Charge", "Ship"] });
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);

        // Act
        var id = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await siblingHalted.Task.WaitAsync(timeout.Token);
        await WaitUntilAsync(() => engine.GetSnapshot(id) is { IsFailed: true, ActiveStates.Count: 0 });
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsFailed);
        Assert.False(snapshot.IsCancelled);
        Assert.False(snapshot.IsCompleted);
    }

    private static IReadOnlyDictionary<string, TransitionTarget> Transitions(
        TransitionTarget? completed = null,
        TransitionTarget? failed = null)
    {
        var transitions = new Dictionary<string, TransitionTarget>(StringComparer.Ordinal);
        if (completed is not null)
        {
            transitions[Fact.Completed] = completed;
        }

        if (failed is not null)
        {
            transitions[Fact.Failed] = failed;
        }

        return transitions;
    }

    private static IStateExecutor Throwing() =>
        new DefaultStateExecutor((_, _, _) => Task.FromException<object?>(new InvalidOperationException("state failed")));

    private static IStateExecutor Recording(string stateName, ConcurrentQueue<string> executed) =>
        new DefaultStateExecutor((_, _, _) =>
        {
            executed.Enqueue(stateName);
            return Task.FromResult<object?>(Unit.Value);
        });

    private static CompiledWorkflowDefinition CreateDefinition(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> transitions,
        IReadOnlyDictionary<string, IStateExecutor> executors,
        string initialState = "Charge",
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>? conditionalTransitions = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? forkTable = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? joinTable = null) => new()
        {
            Name = "FailedTransition",
            Transitions = transitions,
            ConditionalTransitions = conditionalTransitions
                ?? new Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>(),
            ForkTable = forkTable ?? new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = joinTable ?? new Dictionary<string, IReadOnlyList<string>>(),
            WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
            InitialState = initialState,
            StateExecutorFactory = new DictionaryStateExecutorFactory(executors),
        };

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(true);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }
}
