using System.Data;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 1 つのデータベースセッションのライフタイムとトランザクション制御。
/// </summary>
public interface ICoreUnitOfWork : IAsyncDisposable
{
    /// <summary>この UoW が保持するデータベースセッション（実装は EF Core DbContext）。</summary>
    ICoreDatabase Db { get; }

    /// <summary>トランザクションを開始する（二重開始は不可）。</summary>
    /// <param name="level">トランザクションの分離レベル。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>トランザクションを開始すると完了するタスク。</returns>
    Task BeginTransactionAsync(IsolationLevel level, CancellationToken cancellationToken);

    /// <summary>変更をセッションに反映する（コミットは <see cref="CommitAsync"/>）。</summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>書き込んだエンティティの数を返すタスク。</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>開始済みトランザクションをコミットする。</summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>コミットが終わると完了するタスク。</returns>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>開始済みトランザクションをロールバックする。</summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>ロールバックが終わると完了するタスク。</returns>
    Task RollbackAsync(CancellationToken cancellationToken);
}
