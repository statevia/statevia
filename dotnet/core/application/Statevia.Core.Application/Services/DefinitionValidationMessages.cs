namespace Statevia.Core.Application.Services;

/// <summary>
/// <see cref="DefinitionService"/> の検証・NotFound メッセージ。
/// </summary>
internal static class DefinitionValidationMessages
{
    /// <summary>定義名が空のときのメッセージ。</summary>
    public const string NameRequired = "Definition name is required.";

    /// <summary>定義の YAML が空のときのメッセージ。</summary>
    public const string YamlRequired = "Definition YAML is required.";

    /// <summary>コンパイル／検証失敗時の API エンベロープ要約メッセージ。</summary>
    public const string ValidationFailed = "Definition validation failed.";

    /// <summary>定義が見つからないときのメッセージ。</summary>
    public const string NotFound = "Definition not found";

    /// <summary>論理削除されていない定義を復元しようとしたときのメッセージ。</summary>
    public const string NotDeleted = "Definition is not deleted.";

    /// <summary>同じプロジェクト内で slug が既に使われているときのメッセージ。</summary>
    public const string SlugConflict = "Definition slug is already in use in this project.";
}
