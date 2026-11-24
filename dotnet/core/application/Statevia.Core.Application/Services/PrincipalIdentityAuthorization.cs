namespace Statevia.Core.Application.Services;

/// <summary>Principal の Identity（Live）検証。</summary>
internal static class PrincipalIdentityAuthorization
{
    /// <summary>
    /// Principal が解決・有効であることを検証する。無効・論理削除は fail-closed。
    /// </summary>
    /// <param name="principal">Principal 情報。</param>
    public static void EnsureLive(PrincipalInfo principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!IsLive(principal))
            throw new ForbiddenException("Principal is not active.", ApiErrorCodes.PrincipalInactive);
    }

    /// <summary>Principal が有効（無効化・論理削除されていない）かを返す。</summary>
    /// <remarks>例外にせずに分岐したい呼び出し元（<c>event.publish</c> の開始者の確認など）が使う。</remarks>
    /// <param name="principal">Principal 情報。</param>
    /// <returns>有効なら <see langword="true"/>。</returns>
    public static bool IsLive(PrincipalInfo principal) =>
        principal is { IsActive: true, DisabledAt: null, DeletedAt: null };
}
