namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>Principal の実行リソース許可 1 行。</summary>
/// <remarks>
/// 一意キーは (principal, kind, resource)。種別の行が無いことは、その種別の追加制限なしを意味する。
/// </remarks>
public sealed class PrincipalResourceGrantRow
{
    /// <summary>所属テナント。</summary>
    public Guid TenantId { get; set; }

    /// <summary>許可を持つ Principal（User または ServiceAccount）。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary><see cref="PrincipalResourceGrantKinds"/> のいずれか。</summary>
    public string ResourceKind { get; set; } = "";

    /// <summary>project なら project ID、definition なら論理定義 ID。</summary>
    public Guid ResourceId { get; set; }

    /// <summary>作成日時（UTC）。置換のたびに新しい行として記録する。</summary>
    public DateTime CreatedAt { get; set; }
}
