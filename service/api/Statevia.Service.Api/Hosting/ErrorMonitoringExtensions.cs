using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sentry;
using Statevia.Runtime.Observability;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// 任意のエラー監視（Sentry）DSN 解決とホスト初期化。
/// </summary>
/// <remarks>
/// <para>製品必須ではない。DSN が空なら SDK を初期化せず送信しない。</para>
/// <para>ログ基盤クライアントではなく、想定外例外のチャネルだけを足す。</para>
/// </remarks>
#pragma warning disable CA1515 // Worker ホストが参照するため public を維持する
public static class ErrorMonitoringExtensions
#pragma warning restore CA1515
{
    /// <summary>環境変数 <c>SENTRY_DSN</c>。</summary>
    public const string DsnEnvironmentVariable = "SENTRY_DSN";

    /// <summary>構成キー <c>Sentry:Dsn</c>（<c>Sentry__Dsn</c>）。</summary>
    public const string DsnConfigurationKey = "Sentry:Dsn";

    /// <summary>
    /// DSN があるときだけ Sentry を初期化し、想定外例外の送信先を登録する。
    /// </summary>
    /// <param name="builder">API / Worker のホストビルダー。</param>
    /// <returns>同じビルダー。</returns>
    public static TBuilder UseStateviaErrorMonitoring<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!TryGetDsn(builder.Configuration, out var dsn))
        {
            builder.Services.TryAddSingleton<IUnexpectedExceptionReporter>(
                NullUnexpectedExceptionReporter.Instance);
            return builder;
        }

        SentrySdk.Init(options => ConfigureSentry(options, dsn, builder.Environment.EnvironmentName));
        ReplaceReporter(builder.Services, new SentryUnexpectedExceptionReporter());
        builder.Services.AddHostedService<SentryFlushHostedService>();
        return builder;
    }

    /// <summary>構成から DSN を取る。空・空白は未設定。</summary>
    /// <param name="configuration">ホスト構成。</param>
    /// <param name="dsn">トリム済み DSN。未設定時は空文字。</param>
    /// <returns>送信してよい DSN があるとき <see langword="true"/>。</returns>
    public static bool TryGetDsn(IConfiguration configuration, out string dsn)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var raw = configuration[DsnEnvironmentVariable] ?? configuration[DsnConfigurationKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            dsn = string.Empty;
            return false;
        }

        dsn = raw.Trim();
        return true;
    }

    /// <summary>PII・トレース・リクエスト本文を切った Sentry オプションを適用する。</summary>
    internal static void ConfigureSentry(SentryOptions options, string dsn, string? environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(dsn);

        options.Dsn = dsn;
        options.SendDefaultPii = false;
        options.TracesSampleRate = 0;
        options.ProfilesSampleRate = 0;
        options.AutoSessionTracking = false;
        options.IsEnvironmentUser = false;
        options.CaptureFailedRequests = false;
        options.DisableDiagnosticSourceIntegration();
        if (!string.IsNullOrWhiteSpace(environment))
            options.Environment = environment;

        options.SetBeforeSend(static (sentryEvent, _) =>
        {
            sentryEvent.Request = new SentryRequest();
            sentryEvent.User = new SentryUser();
            return sentryEvent;
        });
    }

    private static void ReplaceReporter(IServiceCollection services, IUnexpectedExceptionReporter reporter)
    {
        var existing = services.Where(descriptor => descriptor.ServiceType == typeof(IUnexpectedExceptionReporter))
            .ToList();
        foreach (var descriptor in existing)
            services.Remove(descriptor);

        services.AddSingleton(reporter);
    }
}
