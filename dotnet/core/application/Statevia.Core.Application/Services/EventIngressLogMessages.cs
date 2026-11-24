using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary>集合配送のログ（<see cref="LoggerMessageAttribute"/>）。</summary>
/// <remarks>
/// topic・key・範囲の外の購読の実行 ID は出さない。key に業務の識別子が入りうるため。
/// 運用者が個別に調べるときは、topic と key を配信者から聞いて照合する。
/// </remarks>
internal static partial class EventIngressLogMessages
{
    /// <summary>
    /// 判定する Principal の範囲の外にある購読を、Resume を積まずに除いた。
    /// </summary>
    /// <remarks>所属の外を除くのは正常な認可の結果なので Information とする。範囲の外が 0 件のときは出さない。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="tenantId">テナント。</param>
    /// <param name="origin">入口。<c>http</c> または <c>execution</c>。</param>
    /// <param name="matchedCount">topic / key が一致した購読の件数（範囲の外を含む）。</param>
    /// <param name="outOfScopeCount">範囲の外で除いた購読の件数。</param>
    [LoggerMessage(
        EventId = 30501,
        Level = LogLevel.Information,
        Message = "Event ingress skipped out-of-scope subscriptions. TenantId={tenantId} Origin={origin} MatchedCount={matchedCount} OutOfScopeCount={outOfScopeCount}")]
    public static partial void EventIngressSubscriptionsOutOfScope(
        this ILogger logger,
        Guid tenantId,
        string origin,
        int matchedCount,
        int outOfScopeCount);

    /// <summary>
    /// <c>event.publish</c> で、発行した実行の開始者が無効・削除済みのため照合しなかった。
    /// </summary>
    /// <remarks>定義や運用の見直しが要る可能性があるので Warning とする。</remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="tenantId">テナント。</param>
    /// <param name="executionId">発行した実行。</param>
    [LoggerMessage(
        EventId = 30502,
        Level = LogLevel.Warning,
        Message = "Event publish skipped because the execution starter is not active. TenantId={tenantId} ExecutionId={executionId}")]
    public static partial void EventPublishStarterUnavailable(
        this ILogger logger,
        Guid tenantId,
        Guid executionId);
}
