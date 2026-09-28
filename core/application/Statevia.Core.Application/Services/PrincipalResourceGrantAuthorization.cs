namespace Statevia.Core.Application.Services;

/// <summary>User / ServiceAccount の Start に対する Principal リソース許可を評価する。</summary>
/// <remarks>
/// <para><see cref="ExecutionAuthorizationGuard"/> から呼ぶ。継承子 Start かどうかの判断は呼び出し側が行う。</para>
/// <para>種別の行が無いとその種別は追加制限なし。両方あるときは AND。</para>
/// </remarks>
/// <param name="resourceGrants">Principal の実行リソース許可。</param>
/// <param name="tenantContext">呼び出しスコープのテナント文脈。DI に登録されているのは <see cref="ITenantContextAccessor"/> なので、基底の <see cref="ITenantContext"/> では解決しない。</param>
/// <param name="principals">Principal 種別の参照。</param>
internal sealed class PrincipalResourceGrantAuthorization(
    IPrincipalResourceGrantStore resourceGrants,
    ITenantContextAccessor tenantContext,
    IPrincipalDataAccess principals)
{
    /// <summary>許可集合を評価し、含まれなければ拒否する。</summary>
    /// <param name="projectId">定義が属するプロジェクト。</param>
    /// <param name="definitionId">論理定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public Task EnsureAsync(Guid projectId, Guid definitionId, CancellationToken ct)
    {
        if (tenantContext.PrincipalId is not Guid principalId)
            return Task.CompletedTask;

        return EnsureForPrincipalAsync(principalId, projectId, definitionId, ct);
    }

    /// <summary>指定 Principal の許可集合を評価し、含まれなければ拒否する。</summary>
    /// <param name="principalId">評価する User または ServiceAccount。</param>
    /// <param name="projectId">定義が属するプロジェクト。</param>
    /// <param name="definitionId">論理定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="ForbiddenException">許可集合に含まれないとき。コードは <c>RESOURCE_GRANT_DENIED</c>。</exception>
    public async Task EnsureForPrincipalAsync(
        Guid principalId,
        Guid projectId,
        Guid definitionId,
        CancellationToken ct)
    {
        var principal = await principals.FindPrincipalAsync(principalId, ct).ConfigureAwait(false);
        if (principal is not { PrincipalType: PrincipalType.User or PrincipalType.ServiceAccount })
            return;

        var grants = await resourceGrants.ListAsync(principalId, ct).ConfigureAwait(false);
        if (!Allows(grants, projectId, definitionId))
            throw new ForbiddenException("Resource grant denied.", "RESOURCE_GRANT_DENIED");
    }

    /// <summary>行が無い種別は制限せず、両方あるときは AND にする。</summary>
    private static bool Allows(
        IReadOnlyList<PrincipalResourceGrantRow> grants,
        Guid projectId,
        Guid definitionId)
    {
        var projectIds = grants
            .Where(row => row.ResourceKind == PrincipalResourceGrantKinds.Project)
            .Select(row => row.ResourceId)
            .ToHashSet();
        var definitionIds = grants
            .Where(row => row.ResourceKind == PrincipalResourceGrantKinds.Definition)
            .Select(row => row.ResourceId)
            .ToHashSet();
        var projectAllowed = projectIds.Count == 0 || projectIds.Contains(projectId);
        var definitionAllowed = definitionIds.Count == 0 || definitionIds.Contains(definitionId);
        return projectAllowed && definitionAllowed;
    }
}
