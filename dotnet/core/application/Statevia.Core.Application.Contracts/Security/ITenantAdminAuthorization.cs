namespace Statevia.Core.Application.Contracts.Security;

/// <summary>テナント管理者判定（<c>users.is_tenant_admin</c>。グループのみに依存しない）。</summary>
public interface ITenantAdminAuthorization
{
    /// <summary>Principal がテナント管理者か。</summary>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>テナント管理者なら <see langword="true"/>。</returns>
    Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken);
}
