namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// execution_id 単位で seq を採番し <c>event_store</c> に追記する。
/// </summary>
public interface IEventStoreRepository
{
    /// <summary>イベントを追記する。主体は未設定のままにする。</summary>
    /// <param name="uow">書き込み単位。</param>
    /// <param name="executionId">対象実行。</param>
    /// <param name="eventType">イベント種別。</param>
    /// <param name="payloadJson">ペイロード JSON。無いときは null。</param>
    /// <param name="ct">キャンセル。</param>
    Task AppendAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken ct = default);

    /// <summary>主体付きでイベントを追記する。</summary>
    /// <param name="uow">書き込み単位。</param>
    /// <param name="executionId">対象実行。</param>
    /// <param name="eventType">イベント種別。</param>
    /// <param name="payloadJson">ペイロード JSON。無いときは null。</param>
    /// <param name="actorKind">主体種別。スケジュール発火の開始では <c>scheduler</c>。未設定時は null。</param>
    /// <param name="actorId">主体 ID。スケジュール発火ではスケジュール ID。未設定時は null。</param>
    /// <param name="ct">キャンセル。</param>
    Task AppendAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        EventStoreEventType eventType,
        string? payloadJson,
        string? actorKind,
        string? actorId,
        CancellationToken ct = default);

    Task<bool> TryAppendIfAbsentByClientEventAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long afterSeq,
        int limit,
        CancellationToken ct = default);

    Task<long> GetMaxSeqAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct = default);
}
