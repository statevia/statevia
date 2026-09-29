namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary><c>principal_resource_grants.resource_kind</c> の許可値。</summary>
public static class PrincipalResourceGrantKinds
{
    /// <summary>project 許可。長さは列 <c>varchar(32)</c> に収まる。</summary>
    public const string Project = "project";

    /// <summary>definition 許可。長さは列 <c>varchar(32)</c> に収まる。</summary>
    public const string Definition = "definition";

    /// <summary>列の最大文字数。現行値は <c>project</c> / <c>definition</c> で、将来の種別名を足す余白として 32 とする。</summary>
    public const int MaxLength = 32;
}
