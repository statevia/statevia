namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>workflow_definitions テーブル。</summary>
public class WorkflowDefinitionRow
{
    /// <summary>定義 ID。</summary>
    public Guid DefinitionId { get; set; }
    /// <summary>テナントの内部 ID。</summary>
    public Guid TenantId { get; set; }
    /// <summary>定義名。</summary>
    public required string Name { get; set; }
    /// <summary>元の YAML。</summary>
    public required string SourceYaml { get; set; }
    /// <summary>コンパイル済み JSON。</summary>
    public required string CompiledJson { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>更新日時（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}
