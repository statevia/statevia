namespace Statevia.Service.Api.Hosting;

/// <summary>ホスト停止時に Sentry へ未送信イベントを flush する。</summary>
internal sealed class SentryFlushHostedService : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        SentrySdk.Close();
    }
}
