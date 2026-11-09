namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_graph_snapshots テーブル（projection）。</summary>
public class ExecutionGraphSnapshotRow
{
    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; set; }
    /// <summary>実行グラフの JSON。</summary>
    public required string GraphJson { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}
