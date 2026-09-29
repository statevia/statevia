namespace Statevia.Core.Application.Contracts.Services;

/// <summary>Active テナントにシステムスケジュールが無ければ 1 行足す。</summary>
public interface ISystemScheduleProvisioner
{
    /// <summary>
    /// <paramref name="tenantId"/> の報告行を保証する。既存の cron と <c>next_fire_at</c> は変えない。
    /// 一意制約の衝突は成功とする。
    /// </summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task EnsureAsync(Guid tenantId, CancellationToken cancellationToken);
}
