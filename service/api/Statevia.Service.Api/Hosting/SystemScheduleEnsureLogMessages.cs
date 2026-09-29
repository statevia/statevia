using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Hosting;

/// <summary>システムスケジュール補完の構造化ログ。</summary>
internal static partial class SystemScheduleEnsureLogMessages
{
    /// <summary>1 テナントの補完に失敗した。</summary>
    [LoggerMessage(
        EventId = 3040,
        Level = LogLevel.Warning,
        Message = "System schedule ensure failed. TenantId={TenantId}")]
    public static partial void EnsureTenantFailed(this ILogger logger, Exception exception, Guid tenantId);

    /// <summary>Active テナント一覧の取得に失敗した。</summary>
    [LoggerMessage(
        EventId = 3041,
        Level = LogLevel.Warning,
        Message = "System schedule ensure could not list active tenants")]
    public static partial void EnsureListFailed(this ILogger logger, Exception exception);
}
