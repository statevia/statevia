namespace Statevia.Core.Application.Services;

/// <summary>
/// 現行の nodes スキーマ供給実装。
/// 将来は DTO からの自動生成実装へ差し替える想定。
/// </summary>
internal sealed class DefinitionSchemaService : IDefinitionSchemaService
{
    private readonly INodesSchemaProvider _schemaProvider;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="schemaProvider">nodes スキーマを返すプロバイダー。</param>
    public DefinitionSchemaService(INodesSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    /// <inheritdoc />
    public object GetNodesSchemaDocument() => _schemaProvider.GetSchemaDocument();

    /// <inheritdoc />
    public string GetNodesSchemaVersion() => _schemaProvider.GetSchemaVersion();

    /// <inheritdoc />
    public int GetNodesVersion() => _schemaProvider.GetNodesVersion();
}
