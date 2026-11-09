namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>executions テーブル（projection）。</summary>
public class ExecutionRow
{
    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>テナントの内部 ID。</summary>
    public Guid TenantId { get; set; }
    /// <summary>開始元の定義 ID。</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>開始時に固定した定義版（definition_versions FK）。</summary>
    public Guid DefinitionVersionId { get; set; }

    /// <summary>投影 status（Running / Completed / Cancelled / Failed など）。</summary>
    public required string Status { get; set; }
    /// <summary>開始日時（UTC）。</summary>
    public DateTime StartedAt { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
    /// <summary>キャンセルを要求されたか。</summary>
    public bool CancelRequested { get; set; }
    /// <summary>再起動で失われたか（載荷済みのまま試行上限に達した印）。</summary>
    public bool RestartLost { get; set; }

    /// <summary>Start 時点の <see cref="Security.ExecutionSecuritySnapshot"/> JSON。</summary>
    public string? SecuritySnapshotJson { get; set; }
}
