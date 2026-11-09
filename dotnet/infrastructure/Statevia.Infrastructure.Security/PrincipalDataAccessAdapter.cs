namespace Statevia.Infrastructure.Security;

/// <summary>
/// <see cref="IPrincipalDataAccess"/> の実装。内部の <see cref="IPlatformDataAccess"/> に委譲する。
/// </summary>
internal sealed class PrincipalDataAccessAdapter : IPrincipalDataAccess
{
    private readonly IPlatformDataAccess _platform;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="platform">委譲先の Platform データアクセス。</param>
    public PrincipalDataAccessAdapter(IPlatformDataAccess platform)
    {
        _platform = platform;
    }

    /// <inheritdoc />
    public async Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken)
    {
        var row = await _platform.FindPrincipalAsync(principalId, cancellationToken).ConfigureAwait(false);
        if (row is null) return null;

        return new PrincipalInfo(
            row.PrincipalId,
            row.TenantId,
            row.PrincipalType,
            row.IsActive,
            row.DisabledAt,
            row.DeletedAt);
    }

    /// <inheritdoc />
    public async Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await _platform.FindTenantByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;

        return new TenantInfo(row.TenantId, row.TenantKey, row.Lifecycle);
    }

    /// <inheritdoc />
    public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
        _platform.IsTenantAdminAsync(principalId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
        Guid principalId, CancellationToken cancellationToken) =>
        _platform.ExpandPrincipalPermissionKeysAsync(principalId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
        Guid principalId, CancellationToken cancellationToken) =>
        _platform.GetGroupSnapshotsForPrincipalAsync(principalId, cancellationToken);
}
