using Statevia.Service.ActionHost.Execution;

namespace Statevia.Service.ActionHost;

/// <summary>待受開始時にプロセス環境の基準を固定する。</summary>
/// <remarks>Module ロードより後、待受が始まった時点の環境を基準にする。待受後の書き換えは基準に入らない。</remarks>
internal sealed class ActionHostEnvironmentBaselineService(
    ProcessEnvironmentBaseline baseline,
    IHostApplicationLifetime lifetime) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(baseline.Capture);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
