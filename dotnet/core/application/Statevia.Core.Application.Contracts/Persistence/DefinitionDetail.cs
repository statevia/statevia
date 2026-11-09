namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>definitions と特定版を束ねた読み取りモデル。</summary>
public sealed class DefinitionDetail
{
    /// <summary>定義の catalog。</summary>
    public required DefinitionRow Definition { get; init; }

    /// <summary>定義の版。</summary>
    public required DefinitionVersionRow Version { get; init; }
}
