namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>定義ソース YAML の入力制約。</summary>
/// <remarks>
/// <para>Studio の <c>DEFINITION_YAML_MAX_BYTES</c>（<c>nodejs/studio/shared/lib/validation/formRules.ts</c>）と同値であること。</para>
/// <para>Studio を経由しない API 直呼びでも同じ上限を強制し、巨大 YAML によるパース・検証・保存の負荷を防ぐ。</para>
/// </remarks>
public static class DefinitionYamlConstraints
{
    /// <summary>
    /// 定義 YAML の最大 UTF-8 バイト数（256KB）。
    /// </summary>
    /// <remarks>Studio の上限と一致させるため 256 * 1024。文字数ではなく UTF-8 バイト数で数える。</remarks>
    public const int MaxUtf8Bytes = 256 * 1024;

    /// <summary>上限超過時のメッセージ。</summary>
    public const string TooLargeErrorMessage = "Definition YAML must be within 262144 bytes (UTF-8).";
}
