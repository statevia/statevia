namespace Statevia.Core.Application.Contracts.Security;

/// <summary>
/// Principal 検索・権限展開のポート。Infrastructure.Security が実装する。
/// </summary>
public interface IPrincipalDataAccess
{
    /// <summary>Principal ID で Principal 情報を検索する。</summary>
    Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken);

    /// <summary>テナントを内部 UUID で検索する（フィルタ無視）。</summary>
    Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Principal がテナント管理者か。</summary>
    Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken);

    /// <summary>グループ展開と管理者フラグから Principal の許可 semantic key 集合を返す。</summary>
    Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
        Guid principalId,
        CancellationToken cancellationToken);

    /// <summary>Principal の所属グループ（ID と名称）を返す。</summary>
    Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
        Guid principalId,
        CancellationToken cancellationToken);
}

/// <summary>Principal の識別・ライフサイクル情報。</summary>
/// <param name="PrincipalId">Principal UUID。</param>
/// <param name="TenantId">所属テナント。</param>
/// <param name="PrincipalType">Principal 種別。</param>
/// <param name="IsActive">有効か。</param>
/// <param name="DisabledAt">無効化日時（任意）。</param>
/// <param name="DeletedAt">論理削除日時（任意）。</param>
public sealed record PrincipalInfo(
    Guid PrincipalId,
    Guid TenantId,
    PrincipalType PrincipalType,
    bool IsActive,
    DateTime? DisabledAt,
    DateTime? DeletedAt);

/// <summary>テナントの識別とライフサイクル。</summary>
/// <param name="TenantId">内部 UUID。</param>
/// <param name="TenantKey">外部キー。</param>
/// <param name="Lifecycle">ライフサイクル。</param>
public sealed record TenantInfo(Guid TenantId, string TenantKey, TenantLifecycle Lifecycle);
