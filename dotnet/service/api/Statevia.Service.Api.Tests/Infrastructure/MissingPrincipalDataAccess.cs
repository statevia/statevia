namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用。Principal を見つけず、リソース許可の評価をスキップさせる。</summary>
internal sealed class MissingPrincipalDataAccess : IPrincipalDataAccess
{
    /// <inheritdoc />
    public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken)
    {
        _ = principalId;
        _ = cancellationToken;
        return Task.FromResult<PrincipalInfo?>(null);
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
