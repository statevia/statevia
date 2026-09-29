namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>枠または手動発火の結果（<c>schedule_runs</c>）行。</summary>
public sealed class ExecutionScheduleRunRow
{
    /// <summary>枠結果 ID。</summary>
    public Guid ScheduleRunId { get; set; }

    /// <summary>スケジュール ID。</summary>
    public Guid ScheduleId { get; set; }

    /// <summary>テナント ID。</summary>
    public Guid TenantId { get; set; }

    /// <summary>cron 枠の UTC。手動は null。</summary>
    public DateTime? ScheduledFireAt { get; set; }

    /// <summary>手動実行か。</summary>
    public bool Manual { get; set; }

    /// <summary><c>started</c> / <c>skipped_overlap</c> / <c>failed</c>。</summary>
    public string Outcome { get; set; } = "";

    /// <summary>開始できた実行。</summary>
    public Guid? ExecutionId { get; set; }

    /// <summary>公開 error.code があれば。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>システム点検の件数 JSON。テナントスケジュールは null。</summary>
    public string? SummaryJson { get; set; }

    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
