using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>
/// 一覧用の実行リソース許可。User と ServiceAccount だけを見る。
/// 種別の行が無ければその種別は制限しない。System は制限しない。
/// </summary>
internal static class ResourceGrantListScope
{
    /// <summary>制限する ID。null の種別は追加制限なし。</summary>
    /// <param name="db">読み取りコンテキスト。</param>
    /// <param name="principalId">呼び出し元 Principal。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>project 許可と definition 許可。評価しないときは両方 null。</returns>
    public static async Task<(List<Guid>? ProjectIds, List<Guid>? DefinitionIds)> LoadAsync(
        CoreDbContext db,
        Guid principalId,
        CancellationToken ct)
    {
        var principalType = await db.Principals.AsNoTracking()
            .Where(principal => principal.PrincipalId == principalId)
            .Select(principal => (PrincipalType?)principal.PrincipalType)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (principalType is not PrincipalType.User and not PrincipalType.ServiceAccount)
            return (null, null);

        var grants = await db.PrincipalResourceGrants.AsNoTracking()
            .Where(grant => grant.PrincipalId == principalId)
            .Select(grant => new { grant.ResourceKind, grant.ResourceId })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var projectIds = grants
            .Where(grant => grant.ResourceKind == PrincipalResourceGrantKinds.Project)
            .Select(grant => grant.ResourceId)
            .Distinct()
            .ToList();
        var definitionIds = grants
            .Where(grant => grant.ResourceKind == PrincipalResourceGrantKinds.Definition)
            .Select(grant => grant.ResourceId)
            .Distinct()
            .ToList();
        return (
            projectIds.Count == 0 ? null : projectIds,
            definitionIds.Count == 0 ? null : definitionIds);
    }
}
