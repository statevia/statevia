using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>実行と表示 ID の組（一覧のクエリの行）。</summary>
internal sealed class ExecutionWithDisplay
{
    /// <summary>実行の投影。</summary>
    public required ExecutionRow Execution { get; init; }

    /// <summary>実行の表示 ID。採番されていなければ <see langword="null"/>。</summary>
    public string? DisplayId { get; init; }
}

/// <summary>実行の一覧と件数で共通の絞り込み（状態・待機・要対応を除く）。</summary>
/// <param name="StatusFilter">投影の値。未指定は絞らない。</param>
/// <param name="DefinitionIdFilter">定義 UUID。未指定は絞らない。</param>
/// <param name="NameContains">表示 ID の部分一致、または execution UUID の完全一致。</param>
/// <param name="ProjectId">指定時はその Project の定義に紐づく実行だけ。</param>
/// <param name="StartedFrom">開始日時の下限（UTC、含む）。</param>
/// <param name="StartedTo">開始日時の上限（UTC、含まない）。</param>
internal sealed record ExecutionScopeFilter(
    string? StatusFilter,
    Guid? DefinitionIdFilter,
    string? NameContains,
    Guid? ProjectId,
    DateTime? StartedFrom,
    DateTime? StartedTo);

/// <summary>
/// 実行の一覧と件数が使う、範囲と絞り込みを済ませたクエリを組み立てる。
/// </summary>
/// <remarks>
/// 範囲はテナント、呼び出し元が Reader 以上の Project、実行リソース許可（<see cref="ResourceGrantListScope"/>）。
/// 物理子 Execution（<c>execution_branches.execution_id</c> にある実行）は除き、ルートの実行だけを返す。
/// 一覧と件数が同じ部品を通るため、件数に範囲の外が混ざらない。
/// </remarks>
internal static class ExecutionListScope
{
    /// <summary>範囲と絞り込みを済ませたルートの実行のクエリを返す。</summary>
    /// <param name="db">読み取りコンテキスト。</param>
    /// <param name="tenantId">対象テナント。</param>
    /// <param name="principalId">呼び出し元 Principal。</param>
    /// <param name="filter">絞り込み。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>並び順とページを付けていないクエリ。</returns>
    public static async Task<IQueryable<ExecutionWithDisplay>> BuildAsync(
        CoreDbContext db,
        Guid tenantId,
        Guid principalId,
        ExecutionScopeFilter filter,
        CancellationToken ct)
    {
        var query = QueryExecutionsWithDisplayIds(db, tenantId, principalId);
        var grantScope = await ResourceGrantListScope.LoadAsync(db, principalId, ct).ConfigureAwait(false);
        if (grantScope.ProjectIds is { } grantedProjectIds)
        {
            var grantedDefinitionIds = db.Definitions.AsNoTracking()
                .Where(definition => grantedProjectIds.Contains(definition.ProjectId))
                .Select(definition => definition.DefinitionId);
            query = query.Where(x => grantedDefinitionIds.Contains(x.Execution.DefinitionId));
        }

        if (grantScope.DefinitionIds is { } grantedDefinitionIdList)
            query = query.Where(x => grantedDefinitionIdList.Contains(x.Execution.DefinitionId));

        if (!string.IsNullOrWhiteSpace(filter.StatusFilter))
            query = query.Where(x => x.Execution.Status == filter.StatusFilter);

        if (filter.DefinitionIdFilter is Guid definitionId)
            query = query.Where(x => x.Execution.DefinitionId == definitionId);

        if (filter.ProjectId is Guid projectId)
        {
            var projectDefinitionIds = db.Definitions.AsNoTracking()
                .Where(definition => definition.ProjectId == projectId)
                .Select(definition => definition.DefinitionId);
            query = query.Where(x => projectDefinitionIds.Contains(x.Execution.DefinitionId));
        }

        if (!string.IsNullOrWhiteSpace(filter.NameContains))
            query = ApplyNameFilter(query, filter.NameContains.Trim());

        if (filter.StartedFrom is DateTime startedFrom)
            query = query.Where(x => x.Execution.StartedAt >= startedFrom);

        if (filter.StartedTo is DateTime startedTo)
            query = query.Where(x => x.Execution.StartedAt < startedTo);

        return query.Where(x => !db.ExecutionBranches.Any(branch => branch.ExecutionId == x.Execution.ExecutionId));
    }

    private static IQueryable<ExecutionWithDisplay> ApplyNameFilter(IQueryable<ExecutionWithDisplay> query, string needle) =>
        Guid.TryParse(needle, out var executionGuid)
            ? query.Where(x => (x.DisplayId != null && x.DisplayId.Contains(needle)) || x.Execution.ExecutionId == executionGuid)
            : query.Where(x => x.DisplayId != null && x.DisplayId.Contains(needle));

    private static IQueryable<ExecutionWithDisplay> QueryExecutionsWithDisplayIds(
        CoreDbContext db,
        Guid tenantId,
        Guid principalId)
    {
        var accessibleProjectIds = ProjectAccessQueries.AccessibleProjectIds(db, principalId, ProjectAccessRole.Reader);
        var accessibleDefinitionIds = db.Definitions.AsNoTracking()
            .Where(definition => accessibleProjectIds.Contains(definition.ProjectId))
            .Select(definition => definition.DefinitionId);
        var displayIdsForExecution = db.DisplayIds.Where(x => x.Kind == "execution");
        return from w in db.Executions.AsNoTracking()
                   .Where(x => x.TenantId == tenantId && accessibleDefinitionIds.Contains(x.DefinitionId))
               join d in displayIdsForExecution on w.ExecutionId equals d.ResourceId into dGroup
               from d in dGroup.DefaultIfEmpty()
               select new ExecutionWithDisplay { Execution = w, DisplayId = d != null ? d.DisplayId : null };
    }
}
