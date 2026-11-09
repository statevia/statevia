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
    /// <returns>追記が終わると完了するタスク。</returns>
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
    /// <returns>追記が終わると完了するタスク。</returns>
    Task AppendAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        EventStoreEventType eventType,
        string? payloadJson,
        string? actorKind,
        string? actorId,
        CancellationToken ct = default);

    /// <summary>クライアントイベント ID から決まるイベント ID が未登録のときだけ、監査イベントを次の seq で追記する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="clientEventId">クライアントイベント ID。</param>
    /// <param name="eventType">監査イベントの種別。</param>
    /// <param name="payloadJson">ペイロードの JSON。無ければ null。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>追記したら <see langword="true"/>。同じクライアントイベント ID の監査イベントが既にあれば <see langword="false"/>。</returns>
    Task<bool> TryAppendIfAbsentByClientEventAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken);

    /// <summary>指定 seq より後の監査イベントを seq の昇順で返す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="afterSeq">この seq より後のイベントを返す。0 以上。</param>
    /// <param name="limit">返す件数の上限。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>最大 <paramref name="limit"/> 件の監査イベントと、続きがあるか。</returns>
    Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long afterSeq,
        int limit,
        CancellationToken ct = default);

    /// <summary>Execution の監査イベントの最大 seq を返す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">Execution ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>最大 seq。監査イベントが無ければ 0。</returns>
    Task<long> GetMaxSeqAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct = default);
}
