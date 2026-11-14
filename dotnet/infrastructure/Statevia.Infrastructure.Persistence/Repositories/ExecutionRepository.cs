using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary><see cref="IExecutionRepository"/> の EF Core 実装。<c>executions</c> と <c>execution_graph_snapshots</c> を読み書きする。</summary>
internal sealed class ExecutionRepository : IExecutionRepository
{
    /// <inheritdoc />
    public Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct) =>
        uow.GetDb().Executions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId && x.TenantId == tenantId, ct);

    /// <inheritdoc />
    public Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.GetDb().Executions.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    /// <summary>
    /// テナント内 execution をページングする。件数と items は <c>execution_branches.execution_id</c> に無いルートのみ。
    /// </summary>
    /// <remarks>
    /// 範囲と絞り込みは <see cref="ExecutionListScope"/>、待機の判定は <see cref="ExecutionWaitingQueries"/> を通る。
    /// <c>Waiting</c> / <c>NeedsAttention</c> は他の条件と AND で効く。各要素の待機中かどうかもあわせて返す。
    /// </remarks>
    /// <param name="uow">読み取り単位。</param>
    /// <param name="tenantId">対象テナント。</param>
    /// <param name="principalId">呼び出し元 Principal。</param>
    /// <param name="query">フィルタ、ソート、ページ。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>ルートだけの件数と、そのページの execution と displayId と待機中かどうか。</returns>
    public async Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId, bool Waiting)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        ExecutionListPageQuery query,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        var scoped = await ExecutionListScope.BuildAsync(
            db,
            tenantId,
            principalId,
            new ExecutionScopeFilter(
                query.StatusFilter,
                query.DefinitionIdFilter,
                query.NameContains,
                query.ProjectId,
                query.StartedFrom,
                query.StartedTo),
            ct).ConfigureAwait(false);
        var waitingIds = await ExecutionWaitingQueries.LoadWaitingExecutionIdsAsync(db, tenantId, ct).ConfigureAwait(false);
        const string running = ExecutionProjectionStatuses.Running;
        const string failed = ExecutionProjectionStatuses.Failed;

        if (query.Waiting is bool waiting)
        {
            scoped = scoped.Where(x =>
                (x.Execution.Status == running && waitingIds.Contains(x.Execution.ExecutionId)) == waiting);
        }

        if (query.NeedsAttention is bool needsAttention)
        {
            scoped = scoped.Where(x =>
                ((x.Execution.Status == running && waitingIds.Contains(x.Execution.ExecutionId))
                    || x.Execution.Status == failed) == needsAttention);
        }

        var total = await scoped.CountAsync(ct).ConfigureAwait(false);
        var page = await ApplyExecutionsSort(scoped, query.Sort.SortBy, query.Sort.SortOrder)
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var waitingSet = waitingIds.ToHashSet();
        var list = page.ConvertAll(x => (
            Execution: x.Execution,
            DisplayId: x.DisplayId,
            Waiting: x.Execution.Status == running && waitingSet.Contains(x.Execution.ExecutionId)));
        return (total, list);
    }

    /// <inheritdoc />
    public Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow execution,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct)
    {
        uow.GetDb().Executions.Add(execution);
        uow.GetDb().ExecutionGraphSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.GetDb().ExecutionGraphSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    /// <inheritdoc />
    public Task<DateTime?> TryGetSnapshotUpdatedAtByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.GetDb().ExecutionGraphSnapshots.AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<string?> TryGetExecutionStatusByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.GetDb().Executions.AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .Select(x => (string?)x.Status)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>status は <c>ExecuteUpdate</c>（テナント query filter を無視する。Worker など未解決テナントでも ID 指定更新できるようにする）。</para>
    /// <para>snapshot は Ordinal 比較で未変化なら UPDATE しない（TOAST 回避）。</para>
    /// <para>変化した graph は終端時も含めて必ず書く。</para>
    /// <para>明示の Cancel 以外で終端 status を Running に戻さない。graph も逆行しない。</para>
    /// </remarks>
    public async Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        var now = DateTime.UtcNow;

        var currentStatus = await db.Executions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .Select(x => (string?)x.Status)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        var rejectRunningOverTerminal = cancelRequested is null
            && string.Equals(status, ExecutionProjectionStatuses.Running, StringComparison.Ordinal)
            && currentStatus is not null
            && ExecutionProjectionStatuses.IsTerminal(currentStatus);
        if (rejectRunningOverTerminal)
            return;

        var snapshot = await db.ExecutionGraphSnapshots
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct)
            .ConfigureAwait(false);
        if (snapshot is not null
            && !string.Equals(snapshot.GraphJson, graphJson, StringComparison.Ordinal))
        {
            snapshot.GraphJson = graphJson;
            snapshot.UpdatedAt = now;
        }

        if (cancelRequested is { } cancel)
        {
            await db.Executions
                .IgnoreQueryFilters()
                .Where(x => x.ExecutionId == executionId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, status)
                        .SetProperty(x => x.UpdatedAt, now)
                        .SetProperty(x => x.CancelRequested, cancel),
                    ct)
                .ConfigureAwait(false);
            return;
        }

        await db.Executions
            .IgnoreQueryFilters()
            .Where(x => x.ExecutionId == executionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.UpdatedAt, now),
                ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkRestartLostAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct)
    {
        var row = await uow.GetDb().Executions
            .FirstOrDefaultAsync(item => item.ExecutionId == executionId, ct)
            .ConfigureAwait(false);
        if (row is null)
            return;

        row.RestartLost = true;
        row.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>実行の一覧の並び順を付ける。同じ値のときは最後に実行 ID で順を決め、ページをまたいで重複・欠落させない。</summary>
    /// <remarks>許可する値の検証は API の <c>ExecutionListQuery</c> が行う。ここに届く値は <c>null</c> か許可する値だけ。</remarks>
    private static IOrderedQueryable<ExecutionWithDisplay> ApplyExecutionsSort(
        IQueryable<ExecutionWithDisplay> query,
        string? sortBy,
        string? sortOrder)
    {
        var isAsc = ListSortOrders.IsAscending(sortOrder);

        return sortBy switch
        {
            null or ExecutionListSortKeys.UpdatedAt => isAsc
                ? query.OrderBy(x => x.Execution.UpdatedAt)
                    .ThenBy(x => x.Execution.StartedAt)
                    .ThenBy(x => x.Execution.ExecutionId)
                : query.OrderByDescending(x => x.Execution.UpdatedAt)
                    .ThenByDescending(x => x.Execution.StartedAt)
                    .ThenByDescending(x => x.Execution.ExecutionId),
            ExecutionListSortKeys.DisplayId => isAsc
                ? query.OrderBy(x => x.DisplayId)
                    .ThenBy(x => x.Execution.StartedAt)
                    .ThenBy(x => x.Execution.ExecutionId)
                : query.OrderByDescending(x => x.DisplayId)
                    .ThenByDescending(x => x.Execution.StartedAt)
                    .ThenByDescending(x => x.Execution.ExecutionId),
            ExecutionListSortKeys.StartedAt => isAsc
                ? query.OrderBy(x => x.Execution.StartedAt).ThenBy(x => x.Execution.ExecutionId)
                : query.OrderByDescending(x => x.Execution.StartedAt).ThenByDescending(x => x.Execution.ExecutionId),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy), sortBy, "Unsupported execution sort key.")
        };
    }
}
