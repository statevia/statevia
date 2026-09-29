using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>PostgreSQL / SQLite 上のスケジュール正本。</summary>
/// <param name="dbFactory">テナント文脈付き DbContext 工場。</param>
internal sealed class ExecutionScheduleRepository(IDbContextFactory<CoreDbContext> dbFactory)
    : IExecutionScheduleRepository
{
    /// <inheritdoc />
    public async Task<ExecutionScheduleRow?> GetByIdAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.ExecutionSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.ScheduleId == scheduleId && row.DeletedAt == null && row.JobKey == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionScheduleRow>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.ExecutionSchedules
            .AsNoTracking()
            .Where(row => row.DeletedAt == null && row.JobKey == null)
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ExecutionScheduleRow?> GetByNameAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.ExecutionSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenantId && row.Name == name && row.DeletedAt == null && row.JobKey == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> SystemJobExistsAsync(
        Guid tenantId,
        string jobKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.ExecutionSchedules
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                row => row.TenantId == tenantId && row.JobKey == jobKey && row.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(ExecutionScheduleRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.ExecutionSchedules.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ExecutionScheduleRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.ExecutionSchedules.Update(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddRunAsync(ExecutionScheduleRunRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.ExecutionScheduleRuns.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task AddRunAsync(ICoreUnitOfWork uow, ExecutionScheduleRunRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(row);
        uow.GetDb().ExecutionScheduleRuns.Add(row);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> HasNonTerminalStartedRunAsync(
        ICoreUnitOfWork uow,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        var db = uow.GetDb();
        var executionIds = db.ExecutionScheduleRuns
            .IgnoreQueryFilters()
            .Where(run =>
                run.ScheduleId == scheduleId &&
                run.Outcome == ExecutionScheduleRunOutcomes.Started &&
                run.ExecutionId != null)
            .Select(run => run.ExecutionId!.Value);
        return db.Executions
            .IgnoreQueryFilters()
            .Where(execution => executionIds.Contains(execution.ExecutionId))
            .AnyAsync(
                execution =>
                    execution.Status != ExecutionProjectionStatuses.Completed &&
                    execution.Status != ExecutionProjectionStatuses.Cancelled &&
                    execution.Status != ExecutionProjectionStatuses.Failed,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task AdvanceNextFireAtAsync(
        ICoreUnitOfWork uow,
        Guid scheduleId,
        DateTime nextFireAtUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        return uow.GetDb().ExecutionSchedules
            .IgnoreQueryFilters()
            .Where(row => row.ScheduleId == scheduleId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(row => row.NextFireAt, nextFireAtUtc)
                    .SetProperty(row => row.UpdatedAt, updatedAtUtc),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionScheduleRow>> ClaimDueAsync(
        ICoreUnitOfWork uow,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var db = uow.GetDb();
        if (IsNpgsql(db))
            return await ClaimDuePostgresAsync(db, utcNow, limit, cancellationToken).ConfigureAwait(false);

        return await db.ExecutionSchedules
            .IgnoreQueryFilters()
            .Where(row =>
                row.Enabled &&
                row.DeletedAt == null &&
                row.NextFireAt <= utcNow)
            .OrderBy(row => row.NextFireAt)
            .ThenBy(row => row.ScheduleId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>全テナントの due 行を SKIP LOCKED で取る。ロックは UoW トランザクションが保持する。</summary>
    private static async Task<IReadOnlyList<ExecutionScheduleRow>> ClaimDuePostgresAsync(
        CoreDbContext db,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT schedule_id, tenant_id, job_key, definition_id, definition_version_id,
                   run_as_principal_id, created_by_principal_id, name, cron_expression,
                   time_zone, overlap_policy, input_json, enabled, deleted_at,
                   next_fire_at, created_at, updated_at
            FROM schedules
            WHERE enabled = TRUE
              AND deleted_at IS NULL
              AND next_fire_at <= @utcNow
            ORDER BY next_fire_at, schedule_id
            FOR UPDATE SKIP LOCKED
            LIMIT @limit
            """;
        AddParameter(command, "utcNow", utcNow);
        AddParameter(command, "limit", limit);

        var rows = new List<ExecutionScheduleRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadSchedule(reader));
        return rows;
    }

    private static ExecutionScheduleRow ReadSchedule(DbDataReader reader)
    {
        var jobKeyOrdinal = reader.GetOrdinal("job_key");
        var definitionOrdinal = reader.GetOrdinal("definition_id");
        var versionOrdinal = reader.GetOrdinal("definition_version_id");
        var runAsOrdinal = reader.GetOrdinal("run_as_principal_id");
        var inputOrdinal = reader.GetOrdinal("input_json");
        var deletedOrdinal = reader.GetOrdinal("deleted_at");
        return new ExecutionScheduleRow
        {
            ScheduleId = reader.GetGuid(reader.GetOrdinal("schedule_id")),
            TenantId = reader.GetGuid(reader.GetOrdinal("tenant_id")),
            JobKey = reader.IsDBNull(jobKeyOrdinal) ? null : reader.GetString(jobKeyOrdinal),
            DefinitionId = reader.IsDBNull(definitionOrdinal) ? null : reader.GetGuid(definitionOrdinal),
            DefinitionVersionId = reader.IsDBNull(versionOrdinal) ? null : reader.GetGuid(versionOrdinal),
            RunAsPrincipalId = reader.IsDBNull(runAsOrdinal) ? null : reader.GetGuid(runAsOrdinal),
            CreatedByPrincipalId = reader.GetGuid(reader.GetOrdinal("created_by_principal_id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            CronExpression = reader.GetString(reader.GetOrdinal("cron_expression")),
            TimeZone = reader.GetString(reader.GetOrdinal("time_zone")),
            OverlapPolicy = reader.GetString(reader.GetOrdinal("overlap_policy")),
            InputJson = reader.IsDBNull(inputOrdinal) ? null : reader.GetString(inputOrdinal),
            Enabled = reader.GetBoolean(reader.GetOrdinal("enabled")),
            DeletedAt = reader.IsDBNull(deletedOrdinal) ? null : reader.GetDateTime(deletedOrdinal),
            NextFireAt = reader.GetDateTime(reader.GetOrdinal("next_fire_at")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        };
    }

    /// <inheritdoc />
    public async Task<int> CountStuckExecutionsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DateTime updatedAtOrBefore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        var db = uow.GetDb();
        return await db.Executions
            .IgnoreQueryFilters()
            .Where(execution =>
                execution.TenantId == tenantId &&
                execution.Status == ExecutionProjectionStatuses.Running &&
                execution.UpdatedAt <= updatedAtOrBefore &&
                !db.ExecutionWaits.Any(wait => wait.ExecutionId == execution.ExecutionId))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountFailedTenantScheduleRunsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DateTime createdAfter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        var db = uow.GetDb();
        return await db.ExecutionScheduleRuns
            .IgnoreQueryFilters()
            .Where(run =>
                run.TenantId == tenantId &&
                run.Outcome == ExecutionScheduleRunOutcomes.Failed &&
                run.CreatedAt > createdAfter &&
                db.ExecutionSchedules.IgnoreQueryFilters().Any(schedule =>
                    schedule.ScheduleId == run.ScheduleId &&
                    schedule.TenantId == tenantId &&
                    schedule.JobKey == null))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DateTime?> FindLatestCompletedFireAtAsync(
        ICoreUnitOfWork uow,
        Guid scheduleId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uow);
        var db = uow.GetDb();
        return await db.ExecutionScheduleRuns
            .IgnoreQueryFilters()
            .Where(run =>
                run.ScheduleId == scheduleId &&
                run.TenantId == tenantId &&
                run.Outcome == ExecutionScheduleRunOutcomes.Completed &&
                run.ScheduledFireAt != null)
            .OrderByDescending(run => run.ScheduledFireAt)
            .Select(run => run.ScheduledFireAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsNpgsql(CoreDbContext db) =>
        string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
