namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// イベント配送冪等テーブル <c>event_delivery_dedup</c> の永続化。
/// </summary>
public interface IEventDeliveryDedupRepository
{
    /// <summary>テナント・Execution・クライアントイベント ID で配送の記録を探す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="clientEventId">クライアントイベント ID。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>配送の記録。無ければ <see langword="null"/>。</returns>
    Task<EventDeliveryDedupRow?> FindAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        CancellationToken cancellationToken);

    /// <summary>受付の記録をユニットオブワークに追加する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="row">追加する受付の記録（RECEIVED）。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>追加が終わると完了するタスク。</returns>
    Task AddReceivedAsync(ICoreUnitOfWork uow, EventDeliveryDedupRow row, CancellationToken cancellationToken);

    /// <summary>配送の記録のステータスをすぐに更新する（ExecuteUpdate）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="clientEventId">クライアントイベント ID。</param>
    /// <param name="update">書き込むステータス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>更新した記録があれば <see langword="true"/>。</returns>
    Task<bool> TryUpdateStatusAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        EventDeliveryDedupStatusUpdate update,
        CancellationToken cancellationToken);
}
