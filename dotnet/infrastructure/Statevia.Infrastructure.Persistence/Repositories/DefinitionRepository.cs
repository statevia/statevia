using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>definitions / definition_versions の EF 永続化。project 認可は行わない。</summary>
/// <remarks>
/// <para>呼び出し元（Application / Graph ユースケース）が <see cref="IProjectAuthorizationService"/> でゲートする。</para>
/// <para>一覧は <see cref="ProjectAccessQueries"/> で、呼び出し元 Principal が Reader 以上の Project に絞る。</para>
/// <para>単体取得は現在テナントの QueryFilter（および明示の tenant_id 条件）に閉じる。他テナント定義は返さない。</para>
/// </remarks>
internal sealed class DefinitionRepository : IDefinitionRepository
{
    private sealed class DefinitionWithDisplay
    {
        public required DefinitionRow Definition { get; init; }
        public required DefinitionVersionRow Version { get; init; }
        public string? DisplayId { get; init; }
    }

    /// <inheritdoc />
    public Task<DefinitionDetail?> GetLatestForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        GetLatestDetailAsync(uow, tenantId, definitionId, activeOnly: true, tracked: false, ct);

    /// <inheritdoc />
    public async Task<DefinitionRow?> GetLatestForMutationAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        await DefinitionsInTenant(uow, tenantId, tracked: true)
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.DeletedAt == null, ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<DefinitionVersionRow?> GetVersionForExecutionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct) =>
        GetVersionInternalAsync(uow, tenantId, definitionId, version, activeParentOnly: false, ct);

    /// <inheritdoc />
    public async Task<DefinitionVersionRow?> GetVersionForExecutionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionVersionId,
        CancellationToken ct)
    {
        var version = await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionVersionId == definitionVersionId, ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        var definitionExists = await DefinitionsInTenant(uow, tenantId, tracked: false)
            .AnyAsync(x => x.DefinitionId == version.DefinitionId, ct)
            .ConfigureAwait(false);
        return definitionExists ? version : null;
    }

    /// <inheritdoc />
    public Task<DefinitionVersionRow?> GetVersionForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct) =>
        GetVersionInternalAsync(uow, tenantId, definitionId, version, activeParentOnly: true, ct);

    /// <inheritdoc />
    public Task<DefinitionRow?> GetDeletedCatalogEntryAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        DefinitionsInTenant(uow, tenantId, tracked: false)
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.DeletedAt != null, ct);

    /// <inheritdoc />
    public Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        ResolveProjectIdCoreAsync(uow, tenantId, definitionId, activeOnly: true, ct);

    /// <inheritdoc />
    public Task<Guid?> ResolveProjectIdIncludingDeletedAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        ResolveProjectIdCoreAsync(uow, tenantId, definitionId, activeOnly: false, ct);

    private static async Task<Guid?> ResolveProjectIdCoreAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        bool activeOnly,
        CancellationToken ct)
    {
        var query = DefinitionsInTenant(uow, tenantId, tracked: false)
            .Where(x => x.DefinitionId == definitionId);
        if (activeOnly)
            query = query.Where(x => x.DeletedAt == null);

        return await query
            .Select(x => (Guid?)x.ProjectId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct)
    {
        uow.GetDb().Definitions.Add(definition);
        uow.GetDb().DefinitionVersions.Add(version);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(
                x => x.DefinitionId == command.DefinitionId && x.DeletedAt == null,
                ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        var nextVersion = definition.LatestVersion + 1;
        var now = DateTime.UtcNow;
        var versionRow = new DefinitionVersionRow
        {
            DefinitionVersionId = command.NewVersionId,
            DefinitionId = command.DefinitionId,
            Version = nextVersion,
            SourceYaml = command.SourceYaml,
            CompiledJson = command.CompiledJson,
            CreatedAt = now
        };
        uow.GetDb().DefinitionVersions.Add(versionRow);

        definition.Name = command.Name;
        definition.LatestVersion = nextVersion;
        definition.UpdatedAt = now;

        return new DefinitionDetail { Definition = definition, Version = versionRow };
    }

    /// <inheritdoc />
    public async Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        _ = tenantId;
        var db = uow.GetDb();
        var joinQuery = QueryDefinitionsWithDisplayIds(db, principalId, ProjectAccessRole.Reader);
        var grantScope = await ResourceGrantListScope.LoadAsync(db, principalId, ct).ConfigureAwait(false);
        if (grantScope.ProjectIds is { } projectIds)
            joinQuery = joinQuery.Where(x => projectIds.Contains(x.Definition.ProjectId));
        if (grantScope.DefinitionIds is { } definitionIds)
            joinQuery = joinQuery.Where(x => definitionIds.Contains(x.Definition.DefinitionId));
        if (!query.IncludeDeleted)
            joinQuery = joinQuery.Where(x => x.Definition.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(query.NameContains))
            joinQuery = joinQuery.Where(x => x.Definition.Name.Contains(query.NameContains));

        if (query.ProjectId is { } projectId)
            joinQuery = joinQuery.Where(x => x.Definition.ProjectId == projectId);

        var sortedQuery = ApplyDefinitionsSort(joinQuery, query.Sort.SortBy, query.Sort.SortOrder);

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await sortedQuery
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var list = page.ConvertAll(x => (
            Detail: new DefinitionDetail { Definition = x.Definition, Version = x.Version },
            DisplayId: x.DisplayId));
        return (total, list);
    }

    /// <inheritdoc />
    public async Task<DefinitionSoftDeleteOutcome> SoftDeleteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        DateTime deletedAt,
        CancellationToken ct)
    {
        _ = tenantId;
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return DefinitionSoftDeleteOutcome.NotFound;

        if (definition.DeletedAt is not null)
            return DefinitionSoftDeleteOutcome.AlreadyDeleted;

        definition.DeletedAt = deletedAt;
        definition.UpdatedAt = deletedAt;
        return DefinitionSoftDeleteOutcome.Deleted;
    }

    /// <inheritdoc />
    public async Task<DefinitionDetail?> RestoreAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        _ = tenantId;
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.DeletedAt != null, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        var now = DateTime.UtcNow;
        definition.DeletedAt = null;
        definition.UpdatedAt = now;

        // SaveChanges 前のため AsNoTracking+activeOnly 再取得は不可。追跡行と版から詳細を返す。
        var version = await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == definition.LatestVersion,
                ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        return new DefinitionDetail { Definition = definition, Version = version };
    }

    /// <inheritdoc />
    public Task<bool> ExistsActiveSlugInProjectAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        string slug,
        Guid excludingDefinitionId,
        CancellationToken ct) =>
        uow.GetDb().Definitions.AsNoTracking()
            .AnyAsync(
                x => x.ProjectId == projectId
                     && x.Slug == slug
                     && x.DefinitionId != excludingDefinitionId
                     && x.DeletedAt == null,
                ct);

    private static async Task<DefinitionDetail?> GetLatestDetailAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        bool activeOnly,
        bool tracked,
        CancellationToken ct)
    {
        var query = DefinitionsInTenant(uow, tenantId, tracked);

        if (activeOnly)
            query = WhereActive(query);

        var definition = await query
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        var version = await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == definition.LatestVersion,
                ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        return new DefinitionDetail { Definition = definition, Version = version };
    }

    private static async Task<DefinitionVersionRow?> GetVersionInternalAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        bool activeParentOnly,
        CancellationToken ct)
    {
        var definitionQuery = DefinitionsInTenant(uow, tenantId, tracked: false)
            .Where(x => x.DefinitionId == definitionId);
        if (activeParentOnly)
            definitionQuery = WhereActive(definitionQuery);

        var definition = await definitionQuery
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        return await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == version,
                ct)
            .ConfigureAwait(false);
    }

    private static IQueryable<DefinitionRow> WhereActive(IQueryable<DefinitionRow> query) =>
        query.Where(x => x.DeletedAt == null);

    /// <summary>現在テナントに閉じた定義クエリ。</summary>
    /// <remarks>
    /// QueryFilter が効く環境では冗長だが、テスト（filter 無効）でも越境しないよう tenant_id を明示する。
    /// </remarks>
    private static IQueryable<DefinitionRow> DefinitionsInTenant(ICoreUnitOfWork uow, Guid tenantId, bool tracked)
    {
        var query = uow.GetDb().Definitions.Where(x => x.TenantId == tenantId);
        return tracked ? query : query.AsNoTracking();
    }

    private static IQueryable<DefinitionWithDisplay> QueryDefinitionsWithDisplayIds(
        CoreDbContext db,
        Guid principalId,
        ProjectAccessRole minimumRole)
    {
        var accessibleProjectIds = ProjectAccessQueries.AccessibleProjectIds(db, principalId, minimumRole);
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");

        return from definition in db.Definitions.AsNoTracking()
               where accessibleProjectIds.Contains(definition.ProjectId)
               join version in db.DefinitionVersions.AsNoTracking()
                   on new { definition.DefinitionId, Version = definition.LatestVersion }
                   equals new { version.DefinitionId, version.Version }
               join display in displayIdsForDefinition on definition.DefinitionId equals display.ResourceId into displayGroup
               from display in displayGroup.DefaultIfEmpty()
               select new DefinitionWithDisplay
               {
                   Definition = definition,
                   Version = version,
                   DisplayId = display != null ? display.DisplayId : null
               };
    }

    private static IQueryable<DefinitionWithDisplay> ApplyDefinitionsSort(
        IQueryable<DefinitionWithDisplay> query,
        string? sortBy,
        string? sortOrder)
    {
        var normalizedSortBy = sortBy?.Trim();
        var isAsc = !string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "name" => isAsc
                ? query.OrderBy(x => x.Definition.Name).ThenBy(x => x.Definition.CreatedAt)
                : query.OrderByDescending(x => x.Definition.Name).ThenByDescending(x => x.Definition.CreatedAt),
            _ => isAsc
                ? query.OrderBy(x => x.Definition.CreatedAt)
                : query.OrderByDescending(x => x.Definition.CreatedAt)
        };
    }
}

/// <summary>Principal のメンバーシップで見える project。</summary>
internal static class ProjectAccessQueries
{
    /// <summary>指定ロール以上のメンバーである project_id の集合。</summary>
    /// <param name="db">EF コンテキスト。</param>
    /// <param name="principalId">呼び出し元 Principal。</param>
    /// <param name="minimumRole">一覧に含める最小ロール。</param>
    /// <returns>メンバーである project_id。</returns>
    public static IQueryable<Guid> AccessibleProjectIds(
        CoreDbContext db,
        Guid principalId,
        ProjectAccessRole minimumRole)
    {
        var allowedRoles = ProjectAccessRolePolicy.RolesAtOrAbove(minimumRole);

        return db.ProjectAccesses
            .Where(pa => pa.PrincipalId == principalId && allowedRoles.Contains(pa.Role))
            .Select(pa => pa.ProjectId);
    }
}
