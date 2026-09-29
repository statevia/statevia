using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Scheduling;
using Statevia.Core.Application.Infrastructure;

namespace Statevia.Core.Application.Services;

/// <summary>システムスケジュール行の補完。</summary>
/// <param name="schedules">スケジュール正本。</param>
/// <param name="ids">永続 ID。</param>
internal sealed class SystemScheduleProvisioner(
    IExecutionScheduleRepository schedules,
    IIdGenerator ids) : ISystemScheduleProvisioner
{
    /// <inheritdoc />
    public async Task EnsureAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (await schedules
                .SystemJobExistsAsync(tenantId, SystemScheduleRows.StuckExecutionReportJobKey, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await schedules
                .AddAsync(
                    SystemScheduleRows.Create(ids.NewSequentialGuid(), tenantId, DateTime.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (EventDeliveryRetryPolicy.IsUniqueConstraintViolation(exception))
        {
            // 同時起動で先行した補完が行を作っている。既存の cron は維持する。
        }
    }
}
