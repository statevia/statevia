namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用。指定の 1 Principal だけを有効な User として返し、リソース許可を評価させる。</summary>
/// <param name="userPrincipalId">User として返す Principal。</param>
/// <param name="userTenantId">その Principal の所属テナント。</param>
internal sealed class FixedUserPrincipalDataAccess(Guid userPrincipalId, Guid userTenantId) : IPrincipalDataAccess
{
    /// <inheritdoc />
    public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        PrincipalInfo? info = principalId == userPrincipalId
            ? new PrincipalInfo(userPrincipalId, userTenantId, PrincipalType.User, true, null, null)
            : null;
        return Task.FromResult(info);
    }

    /// <inheritdoc />
    public Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
        Guid principalId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
        Guid principalId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
