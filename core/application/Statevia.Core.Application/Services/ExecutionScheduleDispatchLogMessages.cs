using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary>
/// <see cref="ExecutionScheduleDispatchService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionScheduleDispatchLogMessages
{
    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Information,
        Message = "Schedule fire started. ScheduleId={scheduleId} TenantId={tenantId} ExecutionId={executionId}")]
    public static partial void ScheduleFireStarted(
        this ILogger logger,
        Guid scheduleId,
        Guid tenantId,
        Guid executionId);

    [LoggerMessage(
        EventId = 3031,
        Level = LogLevel.Information,
        Message = "Schedule fire skipped overlap. ScheduleId={scheduleId} TenantId={tenantId}")]
    public static partial void ScheduleFireSkippedOverlap(this ILogger logger, Guid scheduleId, Guid tenantId);

    [LoggerMessage(
        EventId = 3032,
        Level = LogLevel.Information,
        Message = "Schedule fire missed slot. ScheduleId={scheduleId} TenantId={tenantId}")]
    public static partial void ScheduleFireMissedSlot(this ILogger logger, Guid scheduleId, Guid tenantId);

    [LoggerMessage(
        EventId = 3033,
        Level = LogLevel.Warning,
        Message = "Schedule fire failed. ScheduleId={scheduleId} TenantId={tenantId} ErrorCode={errorCode}")]
    public static partial void ScheduleFireFailed(
        this ILogger logger,
        Guid scheduleId,
        Guid tenantId,
        string? errorCode);

    [LoggerMessage(
        EventId = 3034,
        Level = LogLevel.Warning,
        Message = "Schedule fire Start failed. ScheduleId={scheduleId} TenantId={tenantId}")]
    public static partial void ScheduleFireStartException(
        this ILogger logger,
        Exception exception,
        Guid scheduleId,
        Guid tenantId);
}
