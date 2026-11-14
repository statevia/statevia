using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary><see cref="IExecutionSummaryRepository"/> の EF Core 実装。</summary>
/// <remarks>範囲は <see cref="ExecutionListScope"/>、待機の判定は <see cref="ExecutionWaitingQueries"/> を実行の一覧と共有する。</remarks>
internal sealed class ExecutionSummaryRepository : IExecutionSummaryRepository
{
    /// <inheritdoc />
    public async Task<ExecutionCounts> CountAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        ExecutionCountFilter filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var db = uow.GetDb();
        var scoped = await ExecutionListScope.BuildAsync(
            db,
            tenantId,
            principalId,
            new ExecutionScopeFilter(
                StatusFilter: null,
                filter.DefinitionIdFilter,
                filter.NameContains,
                filter.ProjectId,
                filter.StartedFrom,
                filter.StartedTo),
            ct).ConfigureAwait(false);
        var waitingIds = await ExecutionWaitingQueries.LoadWaitingExecutionIdsAsync(db, tenantId, ct).ConfigureAwait(false);
        const string running = ExecutionProjectionStatuses.Running;
        const string completed = ExecutionProjectionStatuses.Completed;
        const string failed = ExecutionProjectionStatuses.Failed;
        const string cancelled = ExecutionProjectionStatuses.Cancelled;

        var counts = await scoped
            .GroupBy(_ => 1)
            .Select(group => new ExecutionCounts(
                group.Count(),
                group.Count(x => x.Execution.Status == running),
                group.Count(x => x.Execution.Status == completed),
                group.Count(x => x.Execution.Status == failed),
                group.Count(x => x.Execution.Status == cancelled),
                group.Count(x => x.Execution.Status == running && waitingIds.Contains(x.Execution.ExecutionId)),
                group.Count(x =>
                    (x.Execution.Status == running && waitingIds.Contains(x.Execution.ExecutionId))
                    || x.Execution.Status == failed)))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return counts ?? new ExecutionCounts(0, 0, 0, 0, 0, 0, 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, LatestRootExecution>> ListLatestRootByDefinitionIdsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(definitionIds);
        if (definitionIds.Count == 0)
            return new Dictionary<Guid, LatestRootExecution>();

        var db = uow.GetDb();
        var definitionIdArray = definitionIds.ToArray();
        var latestRows = await db.Executions.AsNoTracking()
            .Where(execution => execution.TenantId == tenantId
                && definitionIdArray.Contains(execution.DefinitionId)
                && !db.ExecutionBranches.Any(branch => branch.ExecutionId == execution.ExecutionId))
            .GroupBy(execution => execution.DefinitionId)
            .Select(group => group
                .OrderByDescending(execution => execution.StartedAt)
                .ThenByDescending(execution => execution.ExecutionId)
                .First())
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (latestRows.Count == 0)
            return new Dictionary<Guid, LatestRootExecution>();

        var latestExecutionIds = latestRows.ConvertAll(row => row.ExecutionId);
        var displayIds = await db.DisplayIds.AsNoTracking()
            .Where(display => display.Kind == "execution" && latestExecutionIds.Contains(display.ResourceId))
            .ToDictionaryAsync(display => display.ResourceId, display => display.DisplayId, ct)
            .ConfigureAwait(false);
        var waitingIds = (await ExecutionWaitingQueries.LoadWaitingExecutionIdsAsync(db, tenantId, ct).ConfigureAwait(false))
            .ToHashSet();

        return latestRows.ToDictionary(
            row => row.DefinitionId,
            row => new LatestRootExecution(
                row.DefinitionId,
                row.ExecutionId,
                displayIds.GetValueOrDefault(row.ExecutionId),
                row.Status,
                row.StartedAt,
                row.UpdatedAt,
                row.Status == ExecutionProjectionStatuses.Running && waitingIds.Contains(row.ExecutionId)));
    }

    /// <inheritdoc />
    public async Task<bool> IsWaitingAsync(ICoreUnitOfWork uow, ExecutionRow execution, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (execution.Status != ExecutionProjectionStatuses.Running)
            return false;

        return await ExecutionWaitingQueries
            .HasAttentionWaitInTreeAsync(uow.GetDb(), execution.ExecutionId, ct)
            .ConfigureAwait(false);
    }
}
