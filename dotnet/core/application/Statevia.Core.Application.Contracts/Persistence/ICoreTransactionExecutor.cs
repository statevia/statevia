namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// ReadCommitted / ReadOnly の定型トランザクション実行。
/// </summary>
public interface ICoreTransactionExecutor
{
    /// <summary>ReadCommitted で 1 トランザクションを実行し、成功時にコミットする。</summary>
    /// <param name="work">ユニットオブワークとキャンセル トークンを受け取って実行する処理。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>コミットが終わると完了するタスク。</returns>
    Task ExecuteReadCommittedAsync(
        Func<ICoreUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>ReadCommitted で 1 トランザクションを実行し、結果を返す。</summary>
    /// <typeparam name="T">処理の結果の型。</typeparam>
    /// <param name="work">ユニットオブワークとキャンセル トークンを受け取って実行する処理。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns><paramref name="work"/> の結果を返すタスク。コミットしてから完了する。</returns>
    Task<T> ExecuteReadCommittedAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);

    /// <summary>トランザクションなしの短命 UoW で読み取りのみ実行する。</summary>
    /// <param name="work">ユニットオブワークとキャンセル トークンを受け取って実行する処理。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns><paramref name="work"/> が終わると完了するタスク。</returns>
    Task ExecuteReadOnlyAsync(
        Func<ICoreUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>トランザクションなしの短命 UoW で読み取りのみ実行し、結果を返す。</summary>
    /// <typeparam name="T">処理の結果の型。</typeparam>
    /// <param name="work">ユニットオブワークとキャンセル トークンを受け取って実行する処理。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns><paramref name="work"/> の結果を返すタスク。</returns>
    Task<T> ExecuteReadOnlyAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);
}
