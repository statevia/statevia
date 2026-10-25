using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Service.ActionHost.Modules;
using System.Text.Json;

namespace Statevia.Service.ActionHost.Execution;

/// <summary>登録済み Module Action を OutOfProcess 向けに実行する。</summary>
/// <remarks>
/// <para>プロセス内の実行は 1 本に制限する。開始前と終了時に、待受開始時のプロセス環境へ戻す。</para>
/// <para>復元に失敗したときは、その Action を失敗させてプロセスを終了する。静的フィールドは消さない。</para>
/// </remarks>
internal sealed class ActionHostExecutor
{
    private const string EnvironmentRestoreFailedCode = "EnvironmentRestoreFailed";
    private const string EnvironmentRestoreFailedMessage =
        "Action Host stopped because restoring the process environment failed.";

    private static readonly EmptyEventProvider EmptyEvents = new();
    private static readonly EmptyStateStore EmptyStore = new();

    private readonly ActionHostActionRegistry _registry;
    private readonly ILogger<ActionHostExecutor> _logger;
    private readonly ProcessEnvironmentBaseline _baseline;
    private readonly Action _terminateProcess;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private int _waitingExecutions;
    private int _stopped;

    /// <summary>新しいインスタンスを初期化する。復元失敗時はプロセスを終了する。</summary>
    /// <param name="registry">Action レジストリ。</param>
    /// <param name="logger">ログ。</param>
    /// <param name="baseline">待受開始時のプロセス環境。</param>
    public ActionHostExecutor(
        ActionHostActionRegistry registry,
        ILogger<ActionHostExecutor> logger,
        ProcessEnvironmentBaseline baseline)
        : this(registry, logger, baseline, static () => Environment.Exit(1))
    {
    }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="registry">Action レジストリ。</param>
    /// <param name="logger">ログ。</param>
    /// <param name="baseline">待受開始時のプロセス環境。</param>
    /// <param name="terminateProcess">復元失敗時にプロセスを止める処理。環境変数の値は読まない。</param>
    internal ActionHostExecutor(
        ActionHostActionRegistry registry,
        ILogger<ActionHostExecutor> logger,
        ProcessEnvironmentBaseline baseline,
        Action terminateProcess)
    {
        _registry = registry;
        _logger = logger;
        _baseline = baseline;
        _terminateProcess = terminateProcess;
    }

    /// <summary>ロック取得を待っている実行数。</summary>
    internal int WaitingExecutions => Volatile.Read(ref _waitingExecutions);

    /// <summary>gRPC 契約相当のリクエストで Action を実行する。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsStopped)
        {
            return EnvironmentRestoreFailure();
        }

        try
        {
            await _baseline.WaitUntilCapturedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure("Cancelled", "Action execution was cancelled or timed out.");
        }

        if (IsStopped)
        {
            return EnvironmentRestoreFailure();
        }

        Interlocked.Increment(ref _waitingExecutions);
        try
        {
            await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure("Cancelled", "Action execution was cancelled or timed out.");
        }
        finally
        {
            Interlocked.Decrement(ref _waitingExecutions);
        }

        try
        {
            if (IsStopped)
            {
                return EnvironmentRestoreFailure();
            }

            if (!TryRestore())
            {
                return EnvironmentRestoreFailure();
            }

            ActionExecutionResult result;

            bool restoreAfterFailed;
            try
            {
                result = await ExecuteCoreAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                restoreAfterFailed = !TryRestore();
            }

            if (restoreAfterFailed)
            {
                return EnvironmentRestoreFailure();
            }

            return result;
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private async Task<ActionExecutionResult> ExecuteCoreAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(request.ActionId, out var registration) || registration is null)
        {
            return Failure("UnknownAction", $"Unknown action '{request.ActionId}'.");
        }

        if (request.Deadline is { } deadline && deadline <= DateTimeOffset.UtcNow)
        {
            return Failure("DeadlineExceeded", "Action execution deadline has passed.");
        }

        using var linkedCts = CreateCancellationSource(request.Deadline, cancellationToken);
        var stateContext = new StateContext
        {
            Events = EmptyEvents,
            Store = EmptyStore,
            ExecutionId = request.ExecutionId,
            StateName = request.StateName,
            Logger = NullLogger.Instance,
        };

        try
        {
            using (ActionBindingScope.Begin(request.Bindings))
            using (NotificationSmtpDispatch.BeginScopeForAction(request))
            {
                var runtimeOutput = await registration.Executor
                    .ExecuteAsync(stateContext, request.Input, linkedCts.Token)
                    .ConfigureAwait(false);

                return new ActionExecutionResult
                {
                    Success = true,
                    Output = SerializeOutput(runtimeOutput),
                };
            }
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            return Failure("Cancelled", "Action execution was cancelled or timed out.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            ActionHostExecutorLog.ExecutionFailed(_logger, ex, request.ActionId, request.ExecutionId);
            return Failure("ActionExecutionFailed", ex.Message);
        }
    }

    private bool TryRestore()
    {
        try
        {
            _baseline.Restore();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StopProcess(ex.GetType().Name);
            return false;
        }
    }

    private void StopProcess(string exceptionType)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        ActionHostExecutorLog.EnvironmentRestoreFailed(_logger, exceptionType);
        _terminateProcess();
    }

    private bool IsStopped => Volatile.Read(ref _stopped) == 1;

    private static CancellationTokenSource CreateCancellationSource(
        DateTimeOffset? deadline,
        CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (deadline is { } deadlineValue)
        {
            var remaining = deadlineValue - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                linked.Cancel();
            }
            else
            {
                linked.CancelAfter(remaining);
            }
        }

        return linked;
    }

    private static JsonElement? SerializeOutput(object? output)
    {
        if (output is null)
        {
            return null;
        }

        if (output is JsonElement jsonElement)
        {
            return jsonElement;
        }

        return JsonSerializer.SerializeToElement(output);
    }

    private static ActionExecutionResult EnvironmentRestoreFailure() =>
        Failure(EnvironmentRestoreFailedCode, EnvironmentRestoreFailedMessage);

    private static ActionExecutionResult Failure(string code, string message) =>
        new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message,
        };
}

internal static partial class ActionHostExecutorLog
{
    [LoggerMessage(EventId = 62201, Level = LogLevel.Warning, Message = "Action execution failed for {ActionId} (ExecutionId={ExecutionId})")]
    public static partial void ExecutionFailed(ILogger logger, Exception exception, string actionId, string executionId);

    /// <summary>プロセス環境の復元失敗を記録する。値と例外メッセージは出さない。</summary>
    [LoggerMessage(EventId = 62202, Level = LogLevel.Critical, Message = "Process environment restore failed ({ExceptionType}). Stopping Action Host.")]
    public static partial void EnvironmentRestoreFailed(ILogger logger, string exceptionType);
}
