using Statevia.Runtime.Observability;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>想定外例外レポートを記録するテスト用実装。</summary>
internal sealed class RecordingUnexpectedExceptionReporter : IUnexpectedExceptionReporter
{
    /// <summary>Report 呼び出し。</summary>
    public List<(Exception Exception, UnexpectedExceptionTags Tags)> Reports { get; } = [];

    /// <inheritdoc />
    public void Report(Exception exception, UnexpectedExceptionTags tags)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Reports.Add((exception, tags));
    }
}
