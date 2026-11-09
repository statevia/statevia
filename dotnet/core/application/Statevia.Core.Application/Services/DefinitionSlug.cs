namespace Statevia.Core.Application.Services;

/// <summary>definitions.slug 生成ヘルパー。</summary>
internal static class DefinitionSlug
{
    /// <summary>表示名と definition ID から tenant 内一意の slug を生成する。</summary>
    /// <param name="definitionId">definition ID。先頭 8 桁を接尾辞に使う。</param>
    /// <param name="name">定義の表示名。</param>
    /// <returns>128 文字以下の slug。</returns>
    public static string FromName(Guid definitionId, string name)
    {
        var trimmed = name.Trim();
        var baseName = string.IsNullOrEmpty(trimmed) ? "definition" : trimmed;
        var suffix = definitionId.ToString("N")[..8];
        var combined = $"{baseName}-{suffix}";
        return combined.Length <= 128 ? combined : combined[..128];
    }
}
