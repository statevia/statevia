namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// Cancel / Publish 等の Serializable 永続化と競合時の再試行。
/// </summary>
public interface IExecutionMutationPersistence
{
    /// <summary>
    /// Serializable で永続化を試み、PostgreSQL の直列化失敗・デッドロック時は設定に従い再試行する。
    /// </summary>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="clientEventId">クライアントイベント ID。</param>
    /// <param name="applyAsync">永続化する処理。再試行のたびに新しいユニットオブワークで呼ぶ。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>コミットが終わると完了するタスク。再試行の上限を超えると例外で失敗する。</returns>
    Task ExecuteSerializableWithRetryAsync(
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        Func<ICoreUnitOfWork, CancellationToken, Task> applyAsync,
        CancellationToken cancellationToken = default);
}
