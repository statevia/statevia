using Statevia.Core.Application.Contracts;

namespace Statevia.Infrastructure.Security;

/// <summary>Principal の Identity（Live）検証。</summary>
internal static class PrincipalIdentityAuthorization
{
    /// <summary>
    /// Principal が解決・有効であることを検証する。無効・論理削除は fail-closed。
    /// </summary>
    /// <param name="principal">Principal 行。</param>
    public static void EnsureLive(PrincipalRow principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!principal.IsActive || principal.DisabledAt is not null || principal.DeletedAt is not null)
            throw new ForbiddenException("Principal is not active.", ApiErrorCodes.PrincipalInactive);
    }
}
