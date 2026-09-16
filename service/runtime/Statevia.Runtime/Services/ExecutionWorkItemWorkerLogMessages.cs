using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Statevia.Runtime.Services;

/// <summary>
/// <see cref="ExecutionWorkItemWorkerHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
/// <remarks>
/// work item 処理でテナントが手元にある行には <c>TenantId</c>（内部 UUID）を載せる。
/// 反復失敗やスロット数などプロセス全体の行には載せない。
/// </remarks>
internal static partial class ExecutionWorkItemWorkerLogMessages
{
    [LoggerMessage(
        EventId = 3301,
        Level = LogLevel.Error,
        Message = "Execution work item worker iteration failed.")]
    public static partial void WorkerIterationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3302,
        Level = LogLevel.Error,
        Message = "Execution work item {WorkItemId} failed. TenantId={tenantId}")]
    public static partial void WorkItemFailed(
        this ILogger logger,
        Exception exception,
        Guid workItemId,
        Guid? tenantId);

    [LoggerMessage(
        EventId = 3303,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} lost lease during processing; another worker may reclaim it. TenantId={tenantId}")]
    public static partial void WorkItemLeaseLost(this ILogger logger, Guid workItemId, Guid? tenantId);

    [LoggerMessage(
        EventId = 3304,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} heartbeat failed. TenantId={tenantId}")]
    public static partial void WorkItemHeartbeatFailed(
        this ILogger logger,
        Exception exception,
        Guid workItemId,
        Guid? tenantId);

    [LoggerMessage(
        EventId = 3305,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} could not acquire checkpoint ownership for execution {ExecutionId}. TenantId={tenantId}")]
    public static partial void WorkItemOwnershipAcquireFailed(
        this ILogger logger,
        Guid workItemId,
        Guid executionId,
        Guid tenantId);

    [LoggerMessage(
        EventId = 3306,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} failed to end owned session. TenantId={tenantId}")]
    public static partial void WorkItemSessionEndFailed(
        this ILogger logger,
        Exception exception,
        Guid workItemId,
        Guid? tenantId);

    [LoggerMessage(
        EventId = 3308,
        Level = LogLevel.Information,
        Message = "Worker interrupted locally owned execution for Cancel. ExecutionId={executionId} TenantId={tenantId}")]
    public static partial void WorkerLocalCancelInterrupt(this ILogger logger, Guid executionId, Guid? tenantId);

    [LoggerMessage(
        EventId = 3310,
        Level = LogLevel.Information,
        Message = "Worker applied Engine cancel before local IRQ. ExecutionId={executionId} TenantId={tenantId}")]
    public static partial void WorkerLocalCancelApplied(this ILogger logger, Guid executionId, Guid? tenantId);

    [LoggerMessage(
        EventId = 3309,
        Level = LogLevel.Information,
        Message = "Worker lifecycle slots changed. ActiveLifecycleSlots={activeLifecycleSlots} MaxConcurrency={maxConcurrency}")]
    public static partial void WorkerLifecycleSlotsChanged(
        this ILogger logger,
        int activeLifecycleSlots,
        int maxConcurrency);

    [LoggerMessage(
        EventId = 3311,
        Level = LogLevel.Error,
        Message = "Work item permanent failure. ExecutionId={executionId} WorkItemId={workItemId} WorkItemKind={workItemKind} FailureReason={failureReason} TenantId={tenantId}")]
    [SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "LoggerMessage のテンプレートはプレースホルダごとに引数が必要。")]
    public static partial void WorkItemPermanentFailure(
        this ILogger logger,
        Exception exception,
        Guid workItemId,
        Guid executionId,
        string workItemKind,
        string failureReason,
        Guid? tenantId);
}
