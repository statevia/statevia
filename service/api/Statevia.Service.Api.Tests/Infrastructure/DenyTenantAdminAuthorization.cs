namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>テスト用。テナント管理者ではない Principal を返す。</summary>
internal sealed class DenyTenantAdminAuthorization : ITenantAdminAuthorization
{
    /// <inheritdoc />
    public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
    {
        _ = principalId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}
