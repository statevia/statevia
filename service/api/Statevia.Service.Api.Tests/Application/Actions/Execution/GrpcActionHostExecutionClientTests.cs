using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary>Action Host プールの宛先選択。</summary>
public sealed class GrpcActionHostExecutionClientTests
{
    /// <summary>エンドポイントが無いときは待ちに入らず未設定を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoEndpoints_ReturnsNotConfigured()
    {
        // Arrange
        using var client = new GrpcActionHostExecutionClient([]);

        // Act
        var result = await client.ExecuteAsync(Request(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("ActionHostNotConfigured", result.ErrorCode);
    }

    /// <summary>進行中の宛先を避け、空いているエンドポイントへ送る。</summary>
    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenFirstEndpointIsBusy_UsesIdleEndpoint()
    {
        // Arrange
        var first = new GateCall();
        var second = new GateCall();
        using var client = new GrpcActionHostExecutionClient([first, second]);
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(4));

        // Act
        var firstRun = client.ExecuteAsync(Request(), CancellationToken.None);
        await first.Started.Task.WaitAsync(wait.Token);
        var secondRun = client.ExecuteAsync(Request(), CancellationToken.None);
        await second.Started.Task.WaitAsync(wait.Token);
        first.Release.TrySetResult();
        second.Release.TrySetResult();
        await firstRun;
        await secondRun;

        // Assert
        Assert.Equal(1, first.StartedCount);
        Assert.Equal(1, second.StartedCount);
    }

    /// <summary>空きが期限までに無いときは ActionHostBusy を返し、宛先の値は出さない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineExpiresWhileBusy_ReturnsActionHostBusy()
    {
        // Arrange
        var call = new GateCall();
        using var client = new GrpcActionHostExecutionClient([call]);
        var holding = client.ExecuteAsync(Request(), CancellationToken.None);
        await call.Started.Task;

        // Act
        var result = await client.ExecuteAsync(
            Request() with { Deadline = DateTimeOffset.UtcNow.AddMilliseconds(-1) },
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("ActionHostBusy", result.ErrorCode);
        Assert.True(
            result.ErrorMessage?.Contains("http", StringComparison.Ordinal) != true,
            "error message contains an endpoint");
        call.Release.TrySetResult();
        await holding;
    }

    /// <summary>待ちのキャンセルも ActionHostBusy にする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledWhileBusy_ReturnsActionHostBusy()
    {
        // Arrange
        var call = new GateCall();
        using var client = new GrpcActionHostExecutionClient([call]);
        var holding = client.ExecuteAsync(Request(), CancellationToken.None);
        await call.Started.Task;
        using var cts = new CancellationTokenSource();

        // Act
        var waiting = client.ExecuteAsync(Request(), cts.Token);
        await cts.CancelAsync();
        var result = await waiting;

        // Assert
        Assert.Equal("ActionHostBusy", result.ErrorCode);
        call.Release.TrySetResult();
        await holding;
    }

    private static ActionExecutionRequest Request() =>
        new()
        {
            ExecutionId = "exec-pool",
            StateName = "Call",
            ActionId = "statevia.action.reference.http.request",
            TenantId = "00000000-0000-4000-8000-000000000001",
        };

    private sealed class GateCall : IActionHostCall
    {
        private int _startedCount;

        /// <summary>この宛先で実行が始まった回数。</summary>
        public int StartedCount => Volatile.Read(ref _startedCount);

        /// <summary>最初の実行が始まったとき完了する。</summary>
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>完了させるまで実行を止める。</summary>
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public async Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _startedCount) == 1)
            {
                Started.TrySetResult();
            }

            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ActionExecutionResult { Success = true };
        }
    }
}
