namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>Principal リソース許可の件数上限。</summary>
/// <remarks>
/// design が例示した project 32 / definition 128。
/// Start は許可集合を都度メモリへ載せるため、管理 PUT でこの件数を超えないようにする。
/// </remarks>
public static class PrincipalResourceGrantLimits
{
    /// <summary>1 Principal に付けられる project 許可の上限。</summary>
    public const int MaxProjects = 32;

    /// <summary>1 Principal に付けられる definition 許可の上限。</summary>
    public const int MaxDefinitions = 128;
}
