using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>テナント文脈の <c>principal_resource_grants</c> を読み書きする。</summary>
/// <remarks>
/// <para>一覧と置換は解決済みテナントの行だけを対象にする。QueryFilter に加え <c>tenant_id</c> を明示する。</para>
/// <para>主キー (principal_id, resource_kind, resource_id) が種別内の二重行を拒む。先頭 2 列で Principal と種別の参照に使える。</para>
/// <para>物理外部キーは principal と tenant。resource の実在は呼び出し側が検証する。</para>
/// </remarks>
/// <param name="dbFactory">テナント文脈付き DbContext 工場。</param>
/// <param name="tenantContext">呼び出しスコープのテナント文脈。登録済みのアクセサを使う。</param>
internal sealed class PrincipalResourceGrantStore(
    IDbContextFactory<CoreDbContext> dbFactory,
    ITenantContextAccessor tenantContext) : IPrincipalResourceGrantStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.PrincipalResourceGrants
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.PrincipalId == principalId)
            .OrderBy(row => row.ResourceKind)
            .ThenBy(row => row.ResourceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(
        Guid principalId,
        IReadOnlyCollection<Guid> projectIds,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        ArgumentNullException.ThrowIfNull(definitionIds);
        if (principalId == Guid.Empty)
            throw new ArgumentException("Principal ID is required.", nameof(principalId));

        var tenantId = RequireTenantId();
        var projects = DistinctIds(projectIds, nameof(projectIds));
        var definitions = DistinctIds(definitionIds, nameof(definitionIds));
        var now = DateTime.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await db.PrincipalResourceGrants
            .Where(row => row.TenantId == tenantId && row.PrincipalId == principalId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = projects
            .Select(resourceId => CreateRow(tenantId, principalId, PrincipalResourceGrantKinds.Project, resourceId, now))
            .Concat(definitions.Select(resourceId =>
                CreateRow(tenantId, principalId, PrincipalResourceGrantKinds.Definition, resourceId, now)));
        db.PrincipalResourceGrants.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>未解決テナントでは書き込まない。</summary>
    private Guid RequireTenantId() =>
        tenantContext.TenantId ?? throw new InvalidOperationException(
            "Tenant context is required to replace principal resource grants.");

    /// <summary>空 GUID を拒み、同一 ID は 1 件にする。</summary>
    private static Guid[] DistinctIds(IReadOnlyCollection<Guid> resourceIds, string paramName)
    {
        if (resourceIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Resource ID is required.", paramName);

        return resourceIds.Distinct().ToArray();
    }

    private static PrincipalResourceGrantRow CreateRow(
        Guid tenantId,
        Guid principalId,
        string resourceKind,
        Guid resourceId,
        DateTime createdAt) =>
        new()
        {
            TenantId = tenantId,
            PrincipalId = principalId,
            ResourceKind = resourceKind,
            ResourceId = resourceId,
            CreatedAt = createdAt
        };
}
