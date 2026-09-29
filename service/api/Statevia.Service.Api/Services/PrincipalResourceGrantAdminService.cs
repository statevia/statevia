using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Services;

/// <summary>Principal リソース許可の読取と、昇格を拒んだ一括置換。</summary>
/// <remarks>
/// <para><see cref="TenantAdministrationService"/> がテナント管理者と対象 Principal を確認したあとで呼ぶ。</para>
/// <para>他テナントの定義は 404。テナントが executor 未満の project とその定義は 422 で保存しない。</para>
/// </remarks>
/// <param name="dbFactory">定義のテナント一致を見るためのコンテキスト。</param>
/// <param name="grants">許可の正本。</param>
/// <param name="projectAuth">project の executor 判定。</param>
/// <param name="executor">認可読み取りのトランザクション。</param>
internal sealed class PrincipalResourceGrantAdminService(
    IDbContextFactory<CoreDbContext> dbFactory,
    IPrincipalResourceGrantStore grants,
    IProjectAuthorizationService projectAuth,
    ICoreTransactionExecutor executor)
{
    /// <summary>当該 Principal の許可を種別ごとに返す。</summary>
    /// <param name="principalId">対象 Principal。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>行が無い種別は空配列。</returns>
    public async Task<PrincipalResourceGrantsDto> GetAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        var rows = await grants.ListAsync(principalId, cancellationToken).ConfigureAwait(false);
        return ToDto(rows);
    }

    /// <summary>両種別を検証してから一括置換し、置換後を返す。</summary>
    /// <param name="tenantId">呼び出しテナント。</param>
    /// <param name="principalId">対象 Principal。</param>
    /// <param name="request">残す ID。空配列はその種別を消す。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>置換後の許可。</returns>
    /// <exception cref="ApiValidationException">配列の省略、空 GUID、件数超過、executor 未満。</exception>
    /// <exception cref="NotFoundException">定義が当該テナントに無いとき。</exception>
    public async Task<PrincipalResourceGrantsDto> ReplaceAsync(
        Guid tenantId,
        Guid principalId,
        ReplacePrincipalResourceGrantsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectIds = Normalize(request.ProjectIds, "projectIds", PrincipalResourceGrantLimits.MaxProjects);
        var definitionIds = Normalize(request.DefinitionIds, "definitionIds", PrincipalResourceGrantLimits.MaxDefinitions);

        foreach (var projectId in projectIds)
            await EnsureProjectExecutableAsync(tenantId, projectId, cancellationToken).ConfigureAwait(false);

        await EnsureDefinitionsExecutableAsync(tenantId, definitionIds, cancellationToken).ConfigureAwait(false);
        await grants.ReplaceAsync(principalId, projectIds, definitionIds, cancellationToken).ConfigureAwait(false);
        return await GetAsync(principalId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>空 GUID を拒み、重複を除いた件数が上限以内であることを要求する。</summary>
    private static Guid[] Normalize(IReadOnlyList<Guid>? ids, string fieldName, int maxCount)
    {
        if (ids is null)
            throw new ApiValidationException($"{fieldName} is required.");
        if (ids.Contains(Guid.Empty))
            throw new ApiValidationException($"{fieldName} must not contain an empty id.");

        var distinct = ids.Distinct().ToArray();
        if (distinct.Length > maxCount)
            throw new ApiValidationException($"{fieldName} exceeds the limit of {maxCount}.");
        return distinct;
    }

    /// <summary>当該テナントの未削除定義だけを受け、各 project が executor 以上であることを要求する。</summary>
    private async Task EnsureDefinitionsExecutableAsync(
        Guid tenantId,
        Guid[] definitionIds,
        CancellationToken cancellationToken)
    {
        if (definitionIds.Length == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Definitions
            .AsNoTracking()
            .Where(row => definitionIds.Contains(row.DefinitionId) && row.TenantId == tenantId && row.DeletedAt == null)
            .Select(row => new { row.DefinitionId, row.ProjectId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var foundIds = rows.Select(row => row.DefinitionId).ToHashSet();
        if (definitionIds.Any(id => !foundIds.Contains(id)))
            throw new NotFoundException("Definition not found.");

        foreach (var projectId in rows.Select(row => row.ProjectId).Distinct())
            await EnsureProjectExecutableAsync(tenantId, projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>executor 未満は 422。未登録と他テナントは 404 のまま返す。</summary>
    private async Task EnsureProjectExecutableAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            await executor.ExecuteReadOnlyAsync(
                    (uow, innerCt) => projectAuth.EnsureCanExecuteAsync(uow, tenantId, projectId, innerCt),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ForbiddenException ex) when (ex.Code == "PROJECT_ACCESS_DENIED")
        {
            throw new ApiValidationException($"Project {projectId:D} is not executable for this tenant.");
        }
    }

    private static PrincipalResourceGrantsDto ToDto(IReadOnlyList<PrincipalResourceGrantRow> rows) =>
        new()
        {
            ProjectIds = rows
                .Where(row => row.ResourceKind == PrincipalResourceGrantKinds.Project)
                .Select(row => row.ResourceId)
                .ToArray(),
            DefinitionIds = rows
                .Where(row => row.ResourceKind == PrincipalResourceGrantKinds.Definition)
                .Select(row => row.ResourceId)
                .ToArray()
        };
}
