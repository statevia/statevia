using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statevia.Runtime.Observability;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>エラー監視の DSN オプトイン。</summary>
public sealed class ErrorMonitoringExtensionsTests
{
    /// <summary>DSN が空なら未設定とみなす。</summary>
    [Fact]
    public void TryGetDsn_WhenMissing_ReturnsFalse()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        // Act
        var found = ErrorMonitoringExtensions.TryGetDsn(configuration, out var dsn);

        // Assert
        Assert.False(found);
        Assert.Equal(string.Empty, dsn);
    }

    /// <summary>空白のみの DSN は未設定とみなす。</summary>
    [Fact]
    public void TryGetDsn_WhenWhitespace_ReturnsFalse()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ErrorMonitoringExtensions.DsnEnvironmentVariable] = "  "
            })
            .Build();

        // Act
        var found = ErrorMonitoringExtensions.TryGetDsn(configuration, out var dsn);

        // Assert
        Assert.False(found);
        Assert.Equal(string.Empty, dsn);
    }

    /// <summary>SENTRY_DSN をトリムして採用する。</summary>
    [Fact]
    public void TryGetDsn_WhenEnvironmentVariable_ReturnsTrimmed()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ErrorMonitoringExtensions.DsnEnvironmentVariable] = " http://key@localhost:9090/1 "
            })
            .Build();

        // Act
        var found = ErrorMonitoringExtensions.TryGetDsn(configuration, out var dsn);

        // Assert
        Assert.True(found);
        Assert.Equal("http://key@localhost:9090/1", dsn);
    }

    /// <summary>Sentry:Dsn 構成キーも読む。</summary>
    [Fact]
    public void TryGetDsn_WhenConfigurationKey_ReturnsValue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ErrorMonitoringExtensions.DsnConfigurationKey] = "http://key@localhost:9090/2"
            })
            .Build();

        // Act
        var found = ErrorMonitoringExtensions.TryGetDsn(configuration, out var dsn);

        // Assert
        Assert.True(found);
        Assert.Equal("http://key@localhost:9090/2", dsn);
    }

    /// <summary>DSN 未設定なら SDK を初期化せず no-op reporter を登録する。</summary>
    [Fact]
    public void UseStateviaErrorMonitoring_WhenDsnMissing_RegistersNullReporter()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ErrorMonitoringExtensions.DsnEnvironmentVariable] = string.Empty
        });

        // Act
        builder.UseStateviaErrorMonitoring();
        using var host = builder.Build();

        // Assert
        var reporter = host.Services.GetRequiredService<IUnexpectedExceptionReporter>();
        Assert.Same(NullUnexpectedExceptionReporter.Instance, reporter);
        Assert.DoesNotContain(
            host.Services.GetServices<IHostedService>(),
            service => service is SentryFlushHostedService);
    }

    /// <summary>構成が null なら ArgumentNullException になる。</summary>
    [Fact]
    public void TryGetDsn_WhenConfigurationNull_Throws()
    {
        // Act
        Action act = () =>
        {
            ErrorMonitoringExtensions.TryGetDsn(null!, out _);
        };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>ビルダーが null なら ArgumentNullException になる。</summary>
    [Fact]
    public void UseStateviaErrorMonitoring_WhenBuilderNull_Throws()
    {
        // Act
        IHostApplicationBuilder builder = null!;
        Action act = () =>
        {
            _ = builder.UseStateviaErrorMonitoring();
        };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>PII・トレースを切ったオプションを適用する。</summary>
    [Fact]
    public void ConfigureSentry_AppliesHardenedOptionsAndClearsRequestUser()
    {
        // Arrange
        var options = new Sentry.SentryOptions();

        // Act
        ErrorMonitoringExtensions.ConfigureSentry(options, "http://key@127.0.0.1:9/1", "Development");
        var sentryEvent = new Sentry.SentryEvent();
        sentryEvent.Request = new Sentry.SentryRequest { Url = "http://example.invalid/path" };
        sentryEvent.User = new Sentry.SentryUser { Id = "user-1" };
        var sent = InvokeBeforeSend(options, sentryEvent);

        // Assert
        Assert.Equal("http://key@127.0.0.1:9/1", options.Dsn);
        Assert.False(options.SendDefaultPii);
        Assert.Equal(0, options.TracesSampleRate);
        Assert.Equal(0, options.ProfilesSampleRate);
        Assert.False(options.AutoSessionTracking);
        Assert.False(options.IsEnvironmentUser);
        Assert.False(options.CaptureFailedRequests);
        Assert.Equal("Development", options.Environment);
        Assert.NotNull(sent);
        Assert.True(string.IsNullOrEmpty(sent!.Request.Url));
        Assert.True(string.IsNullOrEmpty(sent.User.Id));
    }

    /// <summary>環境名が空白なら Environment を書かない。</summary>
    [Fact]
    public void ConfigureSentry_WhenEnvironmentBlank_DoesNotSetEnvironment()
    {
        // Arrange
        var options = new Sentry.SentryOptions();

        // Act
        ErrorMonitoringExtensions.ConfigureSentry(options, "http://key@127.0.0.1:9/1", "  ");

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(options.Environment));
    }

    /// <summary>options が null なら ArgumentNullException になる。</summary>
    [Fact]
    public void ConfigureSentry_WhenOptionsNull_Throws()
    {
        // Act
        Action act = () => ErrorMonitoringExtensions.ConfigureSentry(null!, "http://key@127.0.0.1:9/1", "Development");

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>DSN が空白なら ArgumentException になる。</summary>
    [Fact]
    public void ConfigureSentry_WhenDsnBlank_Throws()
    {
        // Act
        Action act = () => ErrorMonitoringExtensions.ConfigureSentry(new Sentry.SentryOptions(), "  ", "Development");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    private static Sentry.SentryEvent? InvokeBeforeSend(Sentry.SentryOptions options, Sentry.SentryEvent sentryEvent)
    {
        var beforeSend = typeof(Sentry.SentryOptions)
            .GetProperty("BeforeSendInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(options) as Func<Sentry.SentryEvent, Sentry.SentryHint, Sentry.SentryEvent?>;
        Assert.NotNull(beforeSend);
        return beforeSend(sentryEvent, new Sentry.SentryHint());
    }
}
