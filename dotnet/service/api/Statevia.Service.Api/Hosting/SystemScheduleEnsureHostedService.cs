using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Hosting;

/// <summary>起動時に Active テナントへシステムスケジュールを補完する。</summary>
/// <remarks>一意制約の衝突は成功。それ以外の失敗でもホストは落とさない。既存行の cron は変えない。</remarks>
/// <param name="scopeFactory">補完ごとの scope。</param>
/// <param name="logger">構造化ログ。</param>
internal sealed class SystemScheduleEnsureHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SystemScheduleEnsureHostedService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
            var provisioner = scope.ServiceProvider.GetRequiredService<ISystemScheduleProvisioner>();
            var tenants = await platform.ListActiveTenantsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var tenantId in tenants.Select(static tenant => tenant.TenantId))
            {
                try
                {
                    await provisioner.EnsureAsync(tenantId, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
#pragma warning disable CA1031
                catch (Exception exception)
                {
                    logger.EnsureTenantFailed(exception, tenantId);
                }
#pragma warning restore CA1031
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            logger.EnsureListFailed(exception);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
