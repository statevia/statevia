namespace Statevia.Runtime.Observability;

/// <summary>エラー監視が無効なときの no-op 実装。</summary>
public sealed class NullUnexpectedExceptionReporter : IUnexpectedExceptionReporter
{
    /// <summary>共有インスタンス。</summary>
    public static NullUnexpectedExceptionReporter Instance { get; } = new();

    /// <inheritdoc />
    public void Report(Exception exception, UnexpectedExceptionTags tags)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _ = tags;
    }
}
