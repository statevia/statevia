namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>event_delivery_dedup テーブル（イベント配送の冪等制御）。</summary>
public class EventDeliveryDedupRow
{
    /// <summary>テナントの内部 ID。</summary>
    public required Guid TenantId { get; set; }
    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>クライアントが発行したイベントの ID。</summary>
    public Guid ClientEventId { get; set; }
    /// <summary>バッチ ID。無ければ null。</summary>
    public Guid? BatchId { get; set; }
    /// <summary>配送の状態（RECEIVED / APPLIED など）。</summary>
    public required string Status { get; set; }
    /// <summary>受け付けた日時（UTC）。</summary>
    public DateTime AcceptedAt { get; set; }
    /// <summary>適用した日時（UTC）。未適用なら null。</summary>
    public DateTime? AppliedAt { get; set; }
    /// <summary>失敗したときのエラーコード。無ければ null。</summary>
    public string? ErrorCode { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>event_delivery_dedup ステータス更新用 DTO。</summary>
public sealed record EventDeliveryDedupStatusUpdate(
    string Status,
    DateTime UtcNow,
    DateTime? AppliedAt,
    string? ErrorCode);
