namespace Statevia.Infrastructure.Persistence;

/// <summary>execution_events テーブル（監査専用）。行の PK は EventStore の event_id とは別（監査レコードの一意 ID）。</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class ExecutionEventRow
{
    /// <summary>監査レコードの一意 ID。EventStore の event_id とは別。</summary>
    public Guid ExecutionEventId { get; set; }
    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>event_store の同じイベントと同じ seq。</summary>
    public long Seq { get; set; }
    /// <summary>監査イベントの種別。</summary>
    public required string Type { get; set; }
    /// <summary>ペイロードの JSON。無ければ null。</summary>
    public string? PayloadJson { get; set; }
    /// <summary>登録日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
