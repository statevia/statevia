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

    /// <summary>
    /// 定義 YAML を受け取るエンドポイントの HTTP リクエストボディ上限（2MiB）。
    /// </summary>
    /// <remarks>
    /// <para>上限を超えるボディはモデルバインド前に読み込みを打ち切り、メモリ確保を抑える。</para>
    /// <para>
    /// JSON 文字列では制御文字が <c>\u00XX</c>（1 バイト → 6 バイト）にエスケープされ得るため、
    /// <see cref="MaxUtf8Bytes"/> の YAML が必ず収まるよう 8 倍（6 倍 + name / projectId 等の余裕）とする。
    /// YAML 自体の上限判定は <see cref="MaxUtf8Bytes"/> の属性検証（422）が担う。
    /// </para>
    /// </remarks>
    public const long MaxRequestBodyBytes = MaxUtf8Bytes * 8L;

    /// <summary>上限超過時のメッセージ。</summary>
    public const string TooLargeErrorMessage = "Definition YAML must be within 262144 bytes (UTF-8).";
}
