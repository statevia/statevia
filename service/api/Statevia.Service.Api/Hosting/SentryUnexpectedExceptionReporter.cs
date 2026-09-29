using Statevia.Runtime.Observability;

namespace Statevia.Service.Api.Hosting;

/// <summary>Sentry SDK 経由の想定外例外送信。</summary>
internal sealed class SentryUnexpectedExceptionReporter : IUnexpectedExceptionReporter
{
    /// <inheritdoc />
    public void Report(Exception exception, UnexpectedExceptionTags tags)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!SentrySdk.IsEnabled)
            return;

        SentrySdk.CaptureException(exception, scope => ApplyTags(scope, tags));
    }

    private static void ApplyTags(Scope scope, UnexpectedExceptionTags tags)
    {
        if (!string.IsNullOrEmpty(tags.TraceId))
            scope.SetTag("TraceId", tags.TraceId);

        if (tags.TenantId is { } tenantId)
            scope.SetTag("TenantId", tenantId.ToString("D"));

        if (!string.IsNullOrEmpty(tags.ExecutionId))
            scope.SetTag("ExecutionId", tags.ExecutionId);

        if (tags.WorkItemId is { } workItemId)
            scope.SetTag("WorkItemId", workItemId.ToString("D"));
    }
}
