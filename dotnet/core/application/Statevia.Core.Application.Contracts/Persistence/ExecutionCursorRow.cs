namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// execution_cursors テーブル（operational projection）。read-model の正本ではない。
/// </summary>
public class ExecutionCursorRow
{
    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>テナントの内部 ID。</summary>
    public Guid TenantId { get; set; }
    /// <summary>今アクティブな実行グラフのノード ID。無ければ null。</summary>
    public string? CurrentNodeId { get; set; }
    /// <summary>ランタイムの ID（将来の RuntimeSpace 連携用）。無ければ null。</summary>
    public string? CurrentRuntimeId { get; set; }
    /// <summary>処理している Worker の ID。無ければ null。</summary>
    public string? CurrentWorkerId { get; set; }
    /// <summary>Execution の状態（Running / Completed など）。</summary>
    public required string State { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}
