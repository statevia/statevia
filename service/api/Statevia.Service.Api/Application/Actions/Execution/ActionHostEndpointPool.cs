using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>エンドポイントごとに進行中件数を持ち、空いている宛先を選ぶ。</summary>
/// <remarks>
/// 進行中件数はこの呼び出し側プロセスのメモリだけである。ホスト側の排他が環境変数の境界になる。
/// 空きが無いときは期限またはキャンセルまで待ち、待てなければ <c>ActionHostBusy</c> を返す。
/// </remarks>
internal sealed class ActionHostEndpointPool : IDisposable
{
    private readonly IActionHostCall[] _calls;
    private readonly int[] _inFlight;
    private readonly object _gate = new();
    private TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    /// <summary>呼び出し先の一覧でプールを作る。空なら未設定として扱う。</summary>
    /// <param name="calls">エンドポイントごとの呼び出し。起動時に検証済みの URI から作る。</param>
    public ActionHostEndpointPool(IReadOnlyList<IActionHostCall> calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        _calls = calls.ToArray();
        _inFlight = new int[_calls.Length];
    }

    /// <summary>進行中 0 の宛先を優先して実行する。</summary>
    /// <param name="request">実行リクエスト。期限があれば待ちの上限に使う。</param>
    /// <param name="cancellationToken">キャンセル。待ちを打ち切る。</param>
    /// <returns>RPC 結果、未設定、または <c>ActionHostBusy</c>。</returns>
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_calls.Length == 0)
        {
            return NotConfigured();
        }

        using var wait = CreateWaitSource(request.Deadline, cancellationToken);
        int index;
        try
        {
            index = await AcquireIdleAsync(wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Busy();
        }

        try
        {
            return await _calls[index].ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Release(index);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var call in _calls)
        {
            if (call is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private async Task<int> AcquireIdleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task wait;
            lock (_gate)
            {
                for (var index = 0; index < _inFlight.Length; index++)
                {
                    if (_inFlight[index] == 0)
                    {
                        _inFlight[index] = 1;
                        return index;
                    }
                }

                wait = _released.Task;
            }

            await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void Release(int index)
    {
        TaskCompletionSource released;
        lock (_gate)
        {
            _inFlight[index] = 0;
            released = _released;
            _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        released.TrySetResult();
    }

    private static CancellationTokenSource CreateWaitSource(
        DateTimeOffset? deadline,
        CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (deadline is not { } deadlineValue)
        {
            return linked;
        }

        var remaining = deadlineValue - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            linked.Cancel();
        }
        else
        {
            linked.CancelAfter(remaining);
        }

        return linked;
    }

    private static ActionExecutionResult NotConfigured() =>
        new()
        {
            Success = false,
            ErrorCode = "ActionHostNotConfigured",
            ErrorMessage = "Action Host base URL is not configured.",
        };

    private static ActionExecutionResult Busy() =>
        new()
        {
            Success = false,
            ErrorCode = "ActionHostBusy",
            ErrorMessage = "Action Host endpoints were busy until the deadline or cancellation.",
        };
}
