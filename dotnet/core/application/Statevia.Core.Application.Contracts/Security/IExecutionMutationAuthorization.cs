namespace Statevia.Core.Application.Contracts.Security;

/// <summary>Resume / Cancel 等の実行変異操作の Owner / Operator 認可。</summary>
public interface IExecutionMutationAuthorization
{
    /// <summary>
    /// Identity（Live）を検証し、Owner / Operator 経路に応じて Authorization を評価する。
    /// </summary>
    /// <param name="snapshot">Start 時スナップショット。未保存 execution は Live にフォールバック。</param>
    /// <param name="permissionKey">要求 semantic permission key。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureMutationPermissionAsync(
        ExecutionSecuritySnapshot? snapshot,
        string permissionKey,
        CancellationToken cancellationToken);
}
