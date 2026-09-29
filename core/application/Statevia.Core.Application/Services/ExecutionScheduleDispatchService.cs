using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Scheduling;
using Statevia.Core.Application.Scheduling;

namespace Statevia.Core.Application.Services;

/// <summary>due スケジュールを claim し、run-as SA 文脈で既存 <c>StartAsync</c> を呼ぶ。</summary>
/// <remarks>
/// <para>欠発は Start せず <c>next_fire_at</c> だけ進める。overlap skip は非終端実行があるとき Start しない。</para>
/// <para>システム行は Start しない。停滞は実効 Action タイムアウトと投影の <c>updated_at</c> で数える。</para>
/// <para>枠の一意制約と Start 冪等キーで二重 Dispatcher を 1 実行に畳む。</para>
/// </remarks>
/// <param name="schedules">claim / run / next 更新。</param>
/// <param name="executions">既存 Start 受理。</param>
/// <param name="principals">テナントと run-as SA。</param>
/// <param name="tenantContext">発火時に SA を載せる。</param>
/// <param name="executor">claim と skip/fail/start 結果を同一トランザクションにする。</param>
/// <param name="ids">schedule_runs ID。</param>
/// <param name="fire">構造化ログと停滞判定の実効 Action タイムアウト。</param>
internal sealed class ExecutionScheduleDispatchService(
    IExecutionScheduleRepository schedules,
    IExecutionService executions,
    IPrincipalDataAccess principals,
    ITenantContextAccessor tenantContext,
    ICoreTransactionExecutor executor,
    IIdGenerator ids,
    ExecutionScheduleFireSupport fire) : IExecutionScheduleDispatchService
{
    private const string SchedulerMethod = "SCHEDULER";
    private const string StartFailedCode = "START_FAILED";
    private const string NotFoundCode = "NOT_FOUND";
    private const string ValidationCode = "VALIDATION_ERROR";
    private const string ScheduleNotFound = "Schedule not found";
    private const string ScheduleDisabled = "schedule is disabled.";
    private const string RunAsInvalid = "runAsPrincipalId must be an active ServiceAccount in the tenant.";

    /// <inheritdoc />
    public Task<int> DispatchDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var now = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return executor.ExecuteReadCommittedAsync(
            (uow, innerCt) => DispatchDueCoreAsync(uow, now, limit, innerCt),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ExecutionResponse> RunManuallyAsync(
        Guid scheduleId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var row = await schedules.GetByIdAsync(scheduleId, cancellationToken).ConfigureAwait(false);
        if (row is null || row.TenantId != tenantContext.GetRequiredTenantId())
            throw new NotFoundException(ScheduleNotFound);
        if (!row.Enabled)
            throw new ApiValidationException(ScheduleDisabled, new { field = "enabled" });

        var tenant = await principals.FindTenantAsync(row.TenantId, cancellationToken).ConfigureAwait(false);
        var principal = await principals.FindPrincipalAsync(row.RequireRunAsPrincipalId(), cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null ||
            tenant.Lifecycle != TenantLifecycle.Active ||
            !IsUsableServiceAccount(principal, row.TenantId))
        {
            throw new ApiValidationException(RunAsInvalid, new { field = "runAsPrincipalId" });
        }

        Exception? failure = null;
        ExecutionResponse? response = null;
        await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var run = NewManualRun(row);
                await schedules.AddRunAsync(uow, run, innerCt).ConfigureAwait(false);
                await uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                var permissions = await principals
                    .ExpandPrincipalPermissionKeysAsync(row.RequireRunAsPrincipalId(), innerCt)
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
                        response = await executions.StartAsync(
                                CreateStartRequest(row),
                                CreateManualIdempotencyKey(row.ScheduleId, idempotencyKey),
                                new CommandRequestContext(
                                    SchedulerMethod,
                                    $"/v1/schedules/{row.ScheduleId:D}/run",
                                    ScheduleId: row.ScheduleId),
                                innerCt)
                            .ConfigureAwait(false);
                        run.ExecutionId = response.ResourceId;
                        await uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                        fire.Logger.ScheduleManualRunStarted(row.ScheduleId, row.TenantId, response.ResourceId);
                    }
#pragma warning disable CA1031
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        run.Outcome = ExecutionScheduleRunOutcomes.Failed;
                        run.ErrorCode = MapErrorCode(exception);
                        await uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                        fire.Logger.ScheduleFireStartException(exception, row.ScheduleId, row.TenantId);
                        fire.Logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, run.ErrorCode);
                        failure = exception;
                    }
#pragma warning restore CA1031
                }
            },
            cancellationToken).ConfigureAwait(false);

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        return response ?? throw new InvalidOperationException("Manual schedule run did not start.");
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
            fire.Logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, ValidationCode);
            return true;
        }

        if (nextAfterSlot <= utcNow)
        {
            var skipTo = CronNextFireCalculator.NextStrictlyAfter(row.CronExpression, row.TimeZone, utcNow);
            await schedules.AdvanceNextFireAtAsync(uow, row.ScheduleId, skipTo, utcNow, cancellationToken)
                .ConfigureAwait(false);
            fire.Logger.ScheduleFireMissedSlot(row.ScheduleId, row.TenantId);
            return true;
        }

        if (row.JobKey is not null)
            return await ProcessSystemRowAsync(uow, row, utcNow, nextAfterSlot, cancellationToken).ConfigureAwait(false);

        var tenant = await principals.FindTenantAsync(row.TenantId, cancellationToken).ConfigureAwait(false);
        var principal = await principals.FindPrincipalAsync(row.RequireRunAsPrincipalId(), cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null ||
            tenant.Lifecycle != TenantLifecycle.Active ||
            !IsUsableServiceAccount(principal, row.TenantId))
        {
            await FailSlotAsync(uow, row, nextAfterSlot, StartFailedCode, cancellationToken).ConfigureAwait(false);
            fire.Logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, StartFailedCode);
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

            fire.Logger.ScheduleFireSkippedOverlap(row.ScheduleId, row.TenantId);
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
            .ExpandPrincipalPermissionKeysAsync(row.RequireRunAsPrincipalId(), cancellationToken)
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
                fire.Logger.ScheduleFireStarted(row.ScheduleId, row.TenantId, started.ResourceId);
            }
#pragma warning disable CA1031
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                run.Outcome = ExecutionScheduleRunOutcomes.Failed;
                run.ErrorCode = MapErrorCode(exception);
                await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                fire.Logger.ScheduleFireStartException(exception, row.ScheduleId, row.TenantId);
                fire.Logger.ScheduleFireFailed(row.ScheduleId, row.TenantId, run.ErrorCode);
            }
#pragma warning restore CA1031
        }

        return true;
    }

    private async Task<bool> ProcessSystemRowAsync(
        ICoreUnitOfWork uow,
        ExecutionScheduleRow row,
        DateTime utcNow,
        DateTime nextAfterSlot,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(row.JobKey, SystemScheduleRows.StuckExecutionReportJobKey, StringComparison.Ordinal) ||
            !fire.ActionTimeout.TryResolve(out var timeout))
        {
            await FailSlotAsync(uow, row, nextAfterSlot, SystemScheduleRows.ReportFailedErrorCode, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var tenant = await principals.FindTenantAsync(row.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.Lifecycle != TenantLifecycle.Active)
        {
            await FailSlotAsync(uow, row, nextAfterSlot, SystemScheduleRows.TenantNotActiveErrorCode, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        int stuck;
        int failedRuns;
        try
        {
            var threshold = utcNow - timeout;
            stuck = await schedules
                .CountStuckExecutionsAsync(uow, row.TenantId, threshold, cancellationToken)
                .ConfigureAwait(false);
            var lastCompleted = await schedules
                .FindLatestCompletedFireAtAsync(uow, row.ScheduleId, row.TenantId, cancellationToken)
                .ConfigureAwait(false);
            var createdAfter = lastCompleted ?? utcNow.AddHours(-1);
            failedRuns = await schedules
                .CountFailedTenantScheduleRunsAsync(uow, row.TenantId, createdAfter, cancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            fire.Logger.ScheduleFireStartException(exception, row.ScheduleId, row.TenantId);
            await FailSlotAsync(uow, row, nextAfterSlot, SystemScheduleRows.ReportFailedErrorCode, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
#pragma warning restore CA1031

        var run = NewRun(row, ExecutionScheduleRunOutcomes.Completed, executionId: null, errorCode: null);
        run.SummaryJson = JsonSerializer.Serialize(
            new StuckExecutionReportSummary(stuck, failedRuns),
            SummaryJsonOptions);
        var occupied = await TryOccupySlotAsync(uow, row, nextAfterSlot, run, cancellationToken)
            .ConfigureAwait(false);
        if (!occupied)
            return false;

        fire.Logger.StuckExecutionReportCompleted(row.TenantId, stuck, failedRuns);
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
            DefinitionId = row.RequireDefinitionId().ToString("D"),
            DefinitionVersionId = row.DefinitionVersionId,
            Input = DeserializeInput(row.InputJson)
        };

    private static string CreateIdempotencyKey(ExecutionScheduleRow row)
    {
        var slot = DateTime.SpecifyKind(row.NextFireAt, DateTimeKind.Utc);
        return $"{row.ScheduleId:N}:{slot:o}";
    }

    /// <summary>手動実行の冪等キー。<paramref name="idempotencyKey"/> が空なら新規 ID を接尾辞にする。</summary>
    private string CreateManualIdempotencyKey(Guid scheduleId, string? idempotencyKey)
    {
        var token = string.IsNullOrWhiteSpace(idempotencyKey)
            ? ids.NewSequentialGuid().ToString("N")
            : idempotencyKey.Trim();
        return $"{scheduleId:N}:manual:{token}";
    }

    private ExecutionScheduleRunRow NewManualRun(ExecutionScheduleRow row) =>
        new()
        {
            ScheduleRunId = ids.NewSequentialGuid(),
            ScheduleId = row.ScheduleId,
            TenantId = row.TenantId,
            ScheduledFireAt = null,
            Manual = true,
            Outcome = ExecutionScheduleRunOutcomes.Started,
            CreatedAt = DateTime.UtcNow
        };

    private static string MapErrorCode(Exception exception) =>
        exception switch
        {
            ForbiddenException forbidden => forbidden.Code,
            UnauthorizedException unauthorized => unauthorized.Code,
            NotFoundException => NotFoundCode,
            ApiValidationException => ValidationCode,
            _ => StartFailedCode
        };

    private static readonly JsonSerializerOptions SummaryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record StuckExecutionReportSummary(int StuckExecutionCount, int FailedScheduleRunCount);

    private static JsonElement? DeserializeInput(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
