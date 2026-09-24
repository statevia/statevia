using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Scheduling;

namespace Statevia.Core.Application.Services;

/// <summary>due スケジュールを claim し、run-as SA 文脈で既存 <c>StartAsync</c> を呼ぶ。</summary>
/// <remarks>
/// <para>欠発は Start せず <c>next_fire_at</c> だけ進める。overlap skip は非終端実行があるとき Start しない。</para>
/// <para>枠の一意制約と Start 冪等キーで二重 Dispatcher を 1 実行に畳む。</para>
/// </remarks>
/// <param name="schedules">claim / run / next 更新。</param>
/// <param name="executions">既存 Start 受理。</param>
/// <param name="principals">テナントと run-as SA。</param>
/// <param name="tenantContext">発火時に SA を載せる。</param>
/// <param name="executor">claim と skip/fail/start 結果を同一トランザクションにする。</param>
/// <param name="ids">schedule_runs ID。</param>
/// <param name="logger">構造化ログ。</param>
internal sealed class ExecutionScheduleDispatchService(
    IExecutionScheduleRepository schedules,
    IExecutionService executions,
    IPrincipalDataAccess principals,
    ITenantContextAccessor tenantContext,
    ICoreTransactionExecutor executor,
    IIdGenerator ids,
    ILogger<ExecutionScheduleDispatchService> logger) : IExecutionScheduleDispatchService
{
    private const string SchedulerMethod = "SCHEDULER";
    private const string StartFailedCode = "START_FAILED";
    private const string NotFoundCode = "NOT_FOUND";
    private const string ValidationCode = "VALIDATION_ERROR";

    /// <inheritdoc />
    public Task<int> DispatchDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var now = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return executor.ExecuteReadCommittedAsync(
            (uow, innerCt) => DispatchDueCoreAsync(uow, now, limit, innerCt),
            cancellationToken);
    }

    private async Task<int> DispatchDueCoreAsync(
        ICoreUnitOfWork uow,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken)
    {
        var due = await schedules.ClaimDueAsync(uow, utcNow, limit, cancellationToken).ConfigureAwait(false);
        var processed = 0;
        foreach (var row in due)
        {
            var handled = await ProcessDueRowAsync(uow, row, utcNow, cancellationToken).ConfigureAwait(false);
            if (handled)
                processed++;
        }

        return processed;
    }

    private async Task<bool> ProcessDueRowAsync(
        ICoreUnitOfWork uow,
        ExecutionScheduleRow row,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        DateTime nextAfterSlot;
        try
        {
            nextAfterSlot = CronNextFireCalculator.NextStrictlyAfter(
                row.CronExpression,
                row.TimeZone,
                row.NextFireAt);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await FailSlotAsync(uow, row, row.NextFireAt.AddMinutes(1), ValidationCode, cancellationToken)
                .ConfigureAwait(false);
            logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, ValidationCode);
            return true;
        }

        if (nextAfterSlot <= utcNow)
        {
            var skipTo = CronNextFireCalculator.NextStrictlyAfter(row.CronExpression, row.TimeZone, utcNow);
            await schedules.AdvanceNextFireAtAsync(uow, row.ScheduleId, skipTo, utcNow, cancellationToken)
                .ConfigureAwait(false);
            logger.ScheduleFireMissedSlot(row.ScheduleId, row.TenantId);
            return true;
        }

        var tenant = await principals.FindTenantAsync(row.TenantId, cancellationToken).ConfigureAwait(false);
        var principal = await principals.FindPrincipalAsync(row.RunAsPrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null ||
            tenant.Lifecycle != TenantLifecycle.Active ||
            !IsUsableServiceAccount(principal, row.TenantId))
        {
            await FailSlotAsync(uow, row, nextAfterSlot, StartFailedCode, cancellationToken).ConfigureAwait(false);
            logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, StartFailedCode);
            return true;
        }

        if (row.OverlapPolicy == ExecutionScheduleOverlapPolicies.Skip &&
            await schedules.HasNonTerminalStartedRunAsync(uow, row.ScheduleId, cancellationToken)
                .ConfigureAwait(false))
        {
            var occupied = await TryOccupySlotAsync(
                    uow,
                    row,
                    nextAfterSlot,
                    ExecutionScheduleRunOutcomes.SkippedOverlap,
                    executionId: null,
                    errorCode: null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!occupied)
                return false;

            logger.ScheduleFireSkippedOverlap(row.ScheduleId, row.TenantId);
            return true;
        }

        var run = NewRun(row, ExecutionScheduleRunOutcomes.Started, executionId: null, errorCode: null);
        var reserved = await TryOccupySlotAsync(
                uow,
                row,
                nextAfterSlot,
                run,
                cancellationToken)
            .ConfigureAwait(false);
        if (!reserved)
            return false;

        var permissions = await principals
            .ExpandPrincipalPermissionKeysAsync(row.RunAsPrincipalId, cancellationToken)
            .ConfigureAwait(false);
        using (tenantContext.SetContext(new TenantContextState(
                   tenant.TenantId,
                   tenant.TenantKey,
                   row.RunAsPrincipalId,
                   tenant.Lifecycle,
                   permissions.ToHashSet(StringComparer.Ordinal))))
        {
            try
            {
                var started = await executions.StartAsync(
                        CreateStartRequest(row),
                        CreateIdempotencyKey(row),
                        new CommandRequestContext(
                            SchedulerMethod,
                            $"/v1/schedules/{row.ScheduleId:D}/fires",
                            ScheduleId: row.ScheduleId),
                        cancellationToken)
                    .ConfigureAwait(false);
                run.ExecutionId = started.ResourceId;
                await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.ScheduleFireStarted(row.ScheduleId, row.TenantId, started.ResourceId);
            }
#pragma warning disable CA1031
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                run.Outcome = ExecutionScheduleRunOutcomes.Failed;
                run.ErrorCode = MapErrorCode(exception);
                await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.ScheduleFireStartException(exception, row.ScheduleId, row.TenantId);
                logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, run.ErrorCode);
            }
#pragma warning restore CA1031
        }

        return true;
    }

    private async Task FailSlotAsync(
        ICoreUnitOfWork uow,
        ExecutionScheduleRow row,
        DateTime nextFireAt,
        string errorCode,
        CancellationToken cancellationToken)
    {
        _ = await TryOccupySlotAsync(
                uow,
                row,
                nextFireAt,
                ExecutionScheduleRunOutcomes.Failed,
                executionId: null,
                errorCode,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<bool> TryOccupySlotAsync(
        ICoreUnitOfWork uow,
        ExecutionScheduleRow row,
        DateTime nextFireAt,
        string outcome,
        Guid? executionId,
        string? errorCode,
        CancellationToken cancellationToken) =>
        TryOccupySlotAsync(uow, row, nextFireAt, NewRun(row, outcome, executionId, errorCode), cancellationToken);

    private async Task<bool> TryOccupySlotAsync(
        ICoreUnitOfWork uow,
        ExecutionScheduleRow row,
        DateTime nextFireAt,
        ExecutionScheduleRunRow run,
        CancellationToken cancellationToken)
    {
        await schedules.AddRunAsync(uow, run, cancellationToken).ConfigureAwait(false);
        try
        {
            await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return false;
        }

        await schedules.AdvanceNextFireAtAsync(
                uow,
                row.ScheduleId,
                nextFireAt,
                DateTime.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private ExecutionScheduleRunRow NewRun(
        ExecutionScheduleRow row,
        string outcome,
        Guid? executionId,
        string? errorCode) =>
        new()
        {
            ScheduleRunId = ids.NewSequentialGuid(),
            ScheduleId = row.ScheduleId,
            TenantId = row.TenantId,
            ScheduledFireAt = DateTime.SpecifyKind(row.NextFireAt, DateTimeKind.Utc),
            Manual = false,
            Outcome = outcome,
            ExecutionId = executionId,
            ErrorCode = errorCode,
            CreatedAt = DateTime.UtcNow
        };

    private static bool IsUsableServiceAccount(PrincipalInfo? principal, Guid tenantId) =>
        principal is not null
        && principal.TenantId == tenantId
        && principal.PrincipalType == PrincipalType.ServiceAccount
        && principal.IsActive
        && principal.DisabledAt is null
        && principal.DeletedAt is null;

    private static StartExecutionRequest CreateStartRequest(ExecutionScheduleRow row) =>
        new()
        {
            DefinitionId = row.DefinitionId.ToString("D"),
            DefinitionVersionId = row.DefinitionVersionId,
            Input = DeserializeInput(row.InputJson)
        };

    private static string CreateIdempotencyKey(ExecutionScheduleRow row)
    {
        var slot = DateTime.SpecifyKind(row.NextFireAt, DateTimeKind.Utc);
        return $"{row.ScheduleId:N}:{slot:o}";
    }

    private static string MapErrorCode(Exception exception) =>
        exception switch
        {
            ForbiddenException forbidden => forbidden.Code,
            UnauthorizedException unauthorized => unauthorized.Code,
            NotFoundException => NotFoundCode,
            ApiValidationException => ValidationCode,
            _ => StartFailedCode
        };

    private static JsonElement? DeserializeInput(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
