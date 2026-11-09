namespace Statevia.Core.Application.Contracts.Services;

/// <summary>nodes スキーマ文書の供給ポート。</summary>
public interface INodesSchemaProvider
{
    /// <summary>nodes 形式の JSON Schema 相当のドキュメントを返す。</summary>
    /// <returns>スキーマ文書。</returns>
    object GetSchemaDocument();

    /// <summary>スキーマ文書のバージョン文字列を返す。</summary>
    /// <returns>スキーマ文書のバージョン文字列。</returns>
    string GetSchemaVersion();

    /// <summary>nodes スキーマの整数バージョンを返す。</summary>
    /// <returns>nodes スキーマのバージョン。</returns>
    int GetNodesVersion();
}
