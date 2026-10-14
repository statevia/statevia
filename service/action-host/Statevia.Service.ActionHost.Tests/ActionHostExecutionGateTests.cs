using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using Statevia.Service.ActionHost.Execution;
using Statevia.Service.ActionHost.Modules;

namespace Statevia.Service.ActionHost.Tests;

/// <summary>Action Host の実行排他とプロセス環境の復元。</summary>
public sealed class ActionHostExecutionGateTests
{
    private const string ProbeName = "STATEVIA_TEST_POOL_PROBE";
    private const string AddedName = "STATEVIA_TEST_POOL_ADDED";
    private const string ProbeValue = "probe-baseline";
    private const string ChangedValue = "probe-changed";
    private const string AddedValue = "probe-added";

    /// <summary>重なった実行は、先行が終わるまで開始しない。</summary>
    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenCallsOverlap_SecondWaitsUntilFirstCompletes()
    {
        // Arrange
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var environment = new DictionaryProcessEnvironment();
        var executor = CreateExecutor(
            environment,
            "test.module.block",
            new BlockingState(environment, () => Interlocked.Increment(ref entered), release.Task));
        using var waitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));

        // Act
        var first = executor.ExecuteAsync(Request("test.module.block"), CancellationToken.None);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 1, waitTimeout.Token);
        var second = executor.ExecuteAsync(Request("test.module.block"), CancellationToken.None);
        await WaitUntilAsync(() => executor.WaitingExecutions == 1, waitTimeout.Token);
        var enteredWhileSecondWaits = Volatile.Read(ref entered);
        release.TrySetResult();
        await first;
        await second;

        // Assert
        Assert.Equal(1, enteredWhileSecondWaits);
        Assert.Equal(2, Volatile.Read(ref entered));
        Assert.False(environment.Contains(AddedName));
    }

    /// <summary>実行中の上書きと追加は終了後に基準へ戻り、次の実行からは見えない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionChangesEnvironment_RestoresBaselineBeforeNextExecution()
    {
        // Arrange
        var environment = new DictionaryProcessEnvironment();
        environment.Seed(ProbeName, ProbeValue);
        var state = new MutatingState(environment);
        var executor = CreateExecutor(environment, "test.module.mutate", state);

        // Act
        var first = await executor.ExecuteAsync(Request("test.module.mutate"), CancellationToken.None);
        var second = await executor.ExecuteAsync(Request("test.module.mutate"), CancellationToken.None);

        // Assert
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(environment.ValueEquals(ProbeName, ProbeValue), "baseline value was not restored");
        Assert.False(environment.Contains(AddedName));
        Assert.False(state.SawChangedProbe, "next execution saw a changed environment value");
        Assert.False(state.SawAdded, "next execution saw an environment variable written by the previous execution");
    }

    /// <summary>キャンセルされた実行のあとも基準へ戻る。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionIsCancelled_RestoresBaseline()
    {
        // Arrange
        var environment = new DictionaryProcessEnvironment();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = CreateExecutor(environment, "test.module.cancel", new CancelState(environment, started));
        using var cts = new CancellationTokenSource();

        // Act
        var running = executor.ExecuteAsync(Request("test.module.cancel"), cts.Token);
        await started.Task;
        await cts.CancelAsync();
        var result = await running;

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Cancelled", result.ErrorCode);
        Assert.False(environment.Contains(AddedName));
        Assert.True(
            result.ErrorMessage?.Contains(AddedValue, StringComparison.Ordinal) != true,
            "error message contains an environment value");
    }

    /// <summary>開始前の復元に失敗したときは Module を動かさず、次の実行も受けない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenRestoreFails_StopsProcessAndRejectsNextExecution()
    {
        // Arrange
        var environment = new DictionaryProcessEnvironment();
        var terminations = 0;
        var ran = 0;
        var executor = CreateExecutor(
            environment,
            "test.module.count",
            new CountingState(() => Interlocked.Increment(ref ran)),
            () => Interlocked.Increment(ref terminations));
        environment.Seed(AddedName, AddedValue);
        environment.ThrowOnRemove = true;

        // Act
        var first = await executor.ExecuteAsync(Request("test.module.count"), CancellationToken.None);
        var second = await executor.ExecuteAsync(Request("test.module.count"), CancellationToken.None);

        // Assert
        Assert.False(first.Success);
        Assert.Equal("EnvironmentRestoreFailed", first.ErrorCode);
        Assert.Equal("EnvironmentRestoreFailed", second.ErrorCode);
        Assert.Equal(0, ran);
        Assert.Equal(1, terminations);
        Assert.True(
            first.ErrorMessage?.Contains(AddedValue, StringComparison.Ordinal) != true,
            "error message contains an environment value");
    }

    /// <summary>実行後の復元に失敗したときは成功にせず、プロセス停止を 1 回だけ行う。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenRestoreAfterExecutionFails_ReturnsFailureAndStopsProcess()
    {
        // Arrange
        var environment = new DictionaryProcessEnvironment();
        var terminations = 0;
        var executor = CreateExecutor(
            environment,
            "test.module.add",
            new AddingState(environment),
            () => Interlocked.Increment(ref terminations));
        environment.ThrowOnRemove = true;

        // Act
        var result = await executor.ExecuteAsync(Request("test.module.add"), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("EnvironmentRestoreFailed", result.ErrorCode);
        Assert.Equal(1, terminations);
        Assert.True(
            result.ErrorMessage?.Contains(AddedValue, StringComparison.Ordinal) != true,
            "error message contains an environment value");
    }

    /// <summary>実行が静的フィールドへ残した値は、終了後も残る。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenActionSetsStaticField_LeavesStaticField()
    {
        // Arrange
        StaticMarkerState.Marker = 0;
        var environment = new DictionaryProcessEnvironment();
        var executor = CreateExecutor(environment, "test.module.static", new StaticMarkerState());

        // Act
        var result = await executor.ExecuteAsync(Request("test.module.static"), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, StaticMarkerState.Marker);
    }

    private static ActionHostExecutor CreateExecutor(
        DictionaryProcessEnvironment environment,
        string actionId,
        IState<object?, object?> state,
        Action? terminateProcess = null)
    {
        var baseline = new ProcessEnvironmentBaseline(environment);
        baseline.Capture();
        var registry = new ActionHostActionRegistry();
        Assert.True(registry.TryRegister(new LoadedActionRegistration(
            actionId,
            DefaultStateExecutor.Create(state),
            "test.module")));
        return new ActionHostExecutor(
            registry,
            NullLogger<ActionHostExecutor>.Instance,
            baseline,
            terminateProcess ?? (static () => { }));
    }

    private static ActionExecutionRequest Request(string actionId) =>
        new()
        {
            ExecutionId = "exec-gate",
            StateName = "Gate",
            ActionId = actionId,
            TenantId = "00000000-0000-4000-8000-000000000001",
        };

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private sealed class BlockingState(
        DictionaryProcessEnvironment environment,
        Func<int> onEnter,
        Task release) : IState<object?, object?>
    {
        /// <inheritdoc />
        public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            var count = onEnter();
            if (count == 1)
            {
                environment.Set(AddedName, AddedValue);
                await release.WaitAsync(ct).ConfigureAwait(false);
            }

            return null;
        }
    }

    private sealed class MutatingState(DictionaryProcessEnvironment environment) : IState<object?, object?>
    {
        private int _runs;

        /// <summary>次の実行の開始時に、基準と違う値が見えた。</summary>
        public bool SawChangedProbe { get; private set; }

        /// <summary>次の実行の開始時に、前の実行が足した名前が見えた。</summary>
        public bool SawAdded { get; private set; }

        /// <inheritdoc />
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            var run = Interlocked.Increment(ref _runs);
            if (run > 1)
            {
                SawAdded = environment.Contains(AddedName);
                SawChangedProbe = environment.Contains(ProbeName)
                    && !environment.ValueEquals(ProbeName, ProbeValue);
            }

            environment.Set(ProbeName, ChangedValue);
            environment.Set(AddedName, AddedValue);
            return Task.FromResult<object?>(null);
        }
    }

    private sealed class CancelState(
        DictionaryProcessEnvironment environment,
        TaskCompletionSource started) : IState<object?, object?>
    {
        /// <inheritdoc />
        public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            environment.Set(AddedName, AddedValue);
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return null;
        }
    }

    private sealed class CountingState(Action onEnter) : IState<object?, object?>
    {
        /// <inheritdoc />
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            onEnter();
            return Task.FromResult<object?>(null);
        }
    }

    private sealed class AddingState(DictionaryProcessEnvironment environment) : IState<object?, object?>
    {
        /// <inheritdoc />
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            environment.Set(AddedName, AddedValue);
            return Task.FromResult<object?>(null);
        }
    }

    private sealed class StaticMarkerState : IState<object?, object?>
    {
        /// <summary>実行が残した静的値。</summary>
        public static int Marker { get; set; }

        /// <inheritdoc />
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            Marker++;
            return Task.FromResult<object?>(null);
        }
    }
}
