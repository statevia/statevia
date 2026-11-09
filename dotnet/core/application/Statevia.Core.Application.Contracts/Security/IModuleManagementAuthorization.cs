namespace Statevia.Core.Application.Contracts.Security;

/// <summary>Module 管理 API（reload / 一覧）の認可。</summary>
/// <remarks>
/// <para>テナント管理者 JWT は従来どおり許可する。</para>
/// <para>非管理者は semantic permission（JWT Live 展開 / API キーの交差済み <c>EffectivePermissionKeys</c>）で評価する。</para>
/// </remarks>
public interface IModuleManagementAuthorization
{
    /// <summary><c>POST /internal/modules/reload</c> を許可する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <exception cref="UnauthorizedException">Principal 未解決。</exception>
    /// <exception cref="ForbiddenException">管理者でも <c>modules.reload</c> でもない。</exception>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureReloadAllowedAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /v1/admin/modules</c> を許可する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <exception cref="UnauthorizedException">Principal 未解決。</exception>
    /// <exception cref="ForbiddenException">管理者でも <c>modules.read</c> でもない。</exception>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureReadAllowedAsync(CancellationToken cancellationToken);
}
