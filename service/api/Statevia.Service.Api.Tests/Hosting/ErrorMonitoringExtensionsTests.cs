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
}
