namespace Statevia.Infrastructure.Persistence;

/// <summary>definitions.slug 生成ヘルパー。</summary>
internal static class DefinitionSlug
{
    /// <summary>表示名と definition ID から tenant 内一意の slug を生成する。</summary>
    /// <remarks>前後の空白を除いた表示名に、definition ID の先頭 8 桁（16 進数）をハイフンでつなぐ。表示名が空なら <c>definition</c> を使う。128 文字を超える分は切り捨てる（<c>definitions.slug</c> の長さ）。</remarks>
    /// <param name="definitionId">definition ID。</param>
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
