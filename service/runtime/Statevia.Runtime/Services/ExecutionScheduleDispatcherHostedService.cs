using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Contracts.Services;

namespace Statevia.Runtime.Services;

/// <summary>期限到来スケジュールをポーリングし、Application の発火ユースケースへ渡す。</summary>
/// <remarks>
/// <para>DelayWait とは別 HostedService。idle 5 秒、batch 64。</para>
/// <para>Start 受理は <see cref="IExecutionScheduleDispatchService"/>。Engine は呼ばない。</para>
/// </remarks>
/// <param name="scopeFactory">イテレーションごとの scope。</param>
/// <param name="logger">構造化ログ。</param>
public sealed class ExecutionScheduleDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExecutionScheduleDispatcherHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(5);
    private const int BatchLimit = 64;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var waitBeforeNextPoll = true;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IExecutionScheduleDispatchService>();
                var count = await dispatcher.DispatchDueAsync(DateTime.UtcNow, BatchLimit, stoppingToken)
                    .ConfigureAwait(false);
                if (count > 0)
                    logger.DueSchedulesDispatched(count);
                waitBeforeNextPoll = count < BatchLimit;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031
            catch (Exception exception)
            {
                logger.DispatchIterationFailed(exception);
                waitBeforeNextPoll = true;
            }
#pragma warning restore CA1031
            if (waitBeforeNextPoll)
                await Task.Delay(IdlePollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
