namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>event_store テーブル（イベントソース専用）。</summary>
public class EventStoreRow
{
    /// <summary>監査イベントの一意 ID。</summary>
    public Guid EventId { get; set; }
    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>同じ Execution 内の連番。</summary>
    public long Seq { get; set; }
    /// <summary>監査イベントの種別（<see cref="EventStoreEventType"/> の名前）。</summary>
    public required string Type { get; set; }
    /// <summary>発生日時（UTC）。</summary>
    public DateTime OccurredAt { get; set; }
    /// <summary>主体の種別（system / user / scheduler / external）。無ければ null。</summary>
    public string? ActorKind { get; set; }
    /// <summary>主体の ID。無ければ null。</summary>
    public string? ActorId { get; set; }
    /// <summary>相関 ID。無ければ null。</summary>
    public string? CorrelationId { get; set; }
    /// <summary>原因になったイベントの ID。無ければ null。</summary>
    public Guid? CausationId { get; set; }
    /// <summary>ペイロードのスキーマの版。</summary>
    public int SchemaVersion { get; set; }
    /// <summary>ペイロードの JSON。無ければ null。</summary>
    public string? PayloadJson { get; set; }
    /// <summary>登録日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
