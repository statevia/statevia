
namespace Statevia.Infrastructure.Security;

/// <inheritdoc />
internal sealed class TenantAdminAuthorization : ITenantAdminAuthorization
{
    private readonly IPlatformDataAccess _platformDataAccess;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="platformDataAccess">管理者フラグを検索する Platform データアクセス。</param>
    public TenantAdminAuthorization(IPlatformDataAccess platformDataAccess) =>
        _platformDataAccess = platformDataAccess;

    /// <inheritdoc />
    public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
        _platformDataAccess.IsTenantAdminAsync(principalId, cancellationToken);
}
