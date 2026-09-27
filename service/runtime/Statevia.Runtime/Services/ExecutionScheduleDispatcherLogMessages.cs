using Microsoft.Extensions.Logging;

namespace Statevia.Runtime.Services;

/// <summary>
/// <see cref="ExecutionScheduleDispatcherHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionScheduleDispatcherLogMessages
{
    [LoggerMessage(EventId = 3401, Level = LogLevel.Error, Message = "Schedule dispatch iteration failed.")]
    public static partial void DispatchIterationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3402,
        Level = LogLevel.Information,
        Message = "Dispatched {Count} due execution schedules.")]
    public static partial void DueSchedulesDispatched(this ILogger logger, int count);
}
