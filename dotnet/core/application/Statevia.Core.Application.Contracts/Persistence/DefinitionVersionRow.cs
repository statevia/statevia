namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>definition_versions テーブル（immutable 定義版の truth）。</summary>
public class DefinitionVersionRow
{
    /// <summary>版 ID。</summary>
    public Guid DefinitionVersionId { get; set; }

    /// <summary>親の定義 ID。</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>版番号（定義内で 1 始まり）。</summary>
    public int Version { get; set; }

    /// <summary>この版の YAML。作成後は変えない。</summary>
    public required string SourceYaml { get; set; }

    /// <summary>この版のコンパイル済み JSON。Engine に渡す定義の正。</summary>
    public required string CompiledJson { get; set; }

    /// <summary>版の作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
