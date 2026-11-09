namespace Statevia.Core.Application.Contracts.Security;

/// <summary>Runtime API の global permission 評価。</summary>
public interface IRuntimePermissionAuthorization
{
    /// <summary>指定 semantic key を Principal が保持していることを要求する。</summary>
    /// <param name="permissionKey">要求する権限キー。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken);
}
