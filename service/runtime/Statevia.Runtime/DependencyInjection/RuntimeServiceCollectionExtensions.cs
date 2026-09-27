using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Infrastructure.Common;
using Statevia.Infrastructure.Common.DependencyInjection;
using Statevia.Infrastructure.Persistence.DependencyInjection;
using Statevia.Infrastructure.Security.DependencyInjection;
using Statevia.Runtime.Configuration;
using Statevia.Runtime.Services;

namespace Statevia.Runtime.DependencyInjection;

/// <summary>ランタイム用 hosted service の登録拡張。</summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Scheduler 専用ホスト向けに Common（IIdGenerator）・Persistence・書き込み可能なテナント文脈・
    /// DelayWait / OwnershipRecovery / スケジュール Dispatcher を登録する。
    /// </summary>
    /// <remarks>
    /// <para>システム全体の wait / ownership / due スケジュールをスキャンするためテナントフィルタは無効化する。</para>
    /// <para>Start 受理が <see cref="ITenantContextAccessor.SetContext"/> できるよう Security の文脈を使う。JWT 発行は登録しない。</para>
    /// <para>製品の実行 Engine と Action Host は登録しない。Start 受理に必要な拒否実装は API の scheduler プロセス登録が足す。</para>
    /// </remarks>
    public static IServiceCollection AddStateviaSchedulerHost(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = DatabaseConnection.Resolve(configuration);
        services.AddSingleton<ITenantQueryFilterOptions>(DisabledTenantQueryFilterOptions.Instance);
        services.AddStateviaInfrastructureSecurity(configuration);
        services.AddStateviaInfrastructureCommon();
        services.AddStateviaInfrastructurePersistence(connectionString);
        services.AddStateviaRuntimeOptions(configuration);
        services.AddStateviaRuntimeSchedulers();
        return services;
    }

    /// <summary>DelayWait、OwnershipRecovery、スケジュール Dispatcher を登録する。</summary>
    /// <remarks>
    /// 専用 scheduler ホストが常に使う。API プロセス内の Dispatcher は
    /// <c>EnableInProcessScheduleDispatcher</c> で別途登録し、本メソッドは呼ばない。
    /// </remarks>
    public static IServiceCollection AddStateviaRuntimeSchedulers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHostedService<DelayWaitSchedulerHostedService>();
        services.AddHostedService<ExecutionOwnershipRecoveryHostedService>();
        services.AddHostedService<ExecutionScheduleDispatcherHostedService>();
        return services;
    }

    /// <summary>worker hosted service を登録する。</summary>
    public static IServiceCollection AddStateviaRuntimeWorkerHostedService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHostedService<ExecutionWorkItemWorkerHostedService>();
        return services;
    }

    /// <summary>ランタイム設定をバインドし、起動時に検証する。</summary>
    public static IServiceCollection AddStateviaRuntimeOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<RuntimeOptions>()
            .Bind(configuration.GetSection(RuntimeOptions.SectionName))
            .Validate(
                static _ => true,
                "Statevia:Runtime must be bindable.")
            .ValidateOnStart();
        services.AddOptions<WorkerRuntimeOptions>()
            .Bind(configuration.GetSection(WorkerRuntimeOptions.SectionName))
            .Validate(
                static options => options.MaxConcurrency is >= WorkerRuntimeOptions.MinMaxConcurrency
                    and <= WorkerRuntimeOptions.MaxMaxConcurrency,
                "Statevia:Runtime:Worker:MaxConcurrency must be between 1 and 64.")
            .Validate(
                static options => options.CancelConcurrency is >= WorkerRuntimeOptions.MinCancelConcurrency
                    and <= WorkerRuntimeOptions.MaxCancelConcurrency,
                "Statevia:Runtime:Worker:CancelConcurrency must be between 1 and 8.")
            .Validate(
                static options => options.NoProgressTimeout >= WorkerRuntimeOptions.MinNoProgressTimeout
                    && options.NoProgressTimeout <= WorkerRuntimeOptions.MaxNoProgressTimeout,
                "Statevia:Runtime:Worker:NoProgressTimeout must be between 00:00:30 and 1.00:00:00.")
            .Validate(
                static options => options.MaxAttempts is >= WorkerRuntimeOptions.MinMaxAttempts
                    and <= WorkerRuntimeOptions.MaxMaxAttempts,
                "Statevia:Runtime:Worker:MaxAttempts must be between 1 and 1000.")
            .ValidateOnStart();
        return services;
    }
}
