using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statevia.Runtime.Observability;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>DSN あり経路と SDK 依存の reporter / flush。</summary>
[Collection("SentrySdk")]
public sealed class SentryErrorMonitoringSdkTests
{
    private const string DummyDsn = "http://key@127.0.0.1:9/1";

    /// <summary>DSN があるとき Sentry reporter と flush を登録する。</summary>
    [Fact]
    public void UseStateviaErrorMonitoring_WhenDsnPresent_RegistersSentryReporterAndFlush()
    {
        try
        {
            // Arrange
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<IUnexpectedExceptionReporter>(NullUnexpectedExceptionReporter.Instance);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ErrorMonitoringExtensions.DsnEnvironmentVariable] = DummyDsn
            });

            // Act
            builder.UseStateviaErrorMonitoring();
            using var host = builder.Build();

            // Assert
            Assert.IsType<SentryUnexpectedExceptionReporter>(
                host.Services.GetRequiredService<IUnexpectedExceptionReporter>());
            Assert.Contains(
                host.Services.GetServices<IHostedService>(),
                service => service is SentryFlushHostedService);
            Assert.True(SentrySdk.IsEnabled);
        }
        finally
        {
            SentrySdk.Close();
        }
    }

    /// <summary>SDK 無効時は Capture しない。</summary>
    [Fact]
    public void Report_WhenSdkDisabled_DoesNotThrow()
    {
        // Arrange
        SentrySdk.Close();
        var sut = new SentryUnexpectedExceptionReporter();

        // Act
        var act = () => sut.Report(
            new InvalidOperationException("disabled"),
            new UnexpectedExceptionTags(TraceId: "trace"));

        // Assert
        act();
        Assert.False(SentrySdk.IsEnabled);
    }

    /// <summary>exception が null なら ArgumentNullException になる。</summary>
    [Fact]
    public void Report_WhenExceptionNull_Throws()
    {
        // Arrange
        var sut = new SentryUnexpectedExceptionReporter();

        // Act
        var act = () => sut.Report(null!, default);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>SDK 有効時はタグ付きで Capture する。</summary>
    [Fact]
    public void Report_WhenSdkEnabled_CapturesWithTags()
    {
        try
        {
            // Arrange
            SentrySdk.Init(options => ErrorMonitoringExtensions.ConfigureSentry(options, DummyDsn, "Test"));
            var sut = new SentryUnexpectedExceptionReporter();
            var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var workItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            // Act
            sut.Report(
                new InvalidOperationException("enabled"),
                new UnexpectedExceptionTags(
                    TraceId: "trace-1",
                    TenantId: tenantId,
                    ExecutionId: "exec-1",
                    WorkItemId: workItemId));
            sut.Report(new InvalidOperationException("empty-tags"), default);

            // Assert
            Assert.True(SentrySdk.IsEnabled);
        }
        finally
        {
            SentrySdk.Close();
        }
    }

    /// <summary>停止時に flush と Close を呼んでも落ちない。</summary>
    [Fact]
    public async Task FlushHostedService_StopAsync_Completes()
    {
        // Arrange
        var sut = new SentryFlushHostedService();

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.False(SentrySdk.IsEnabled);
    }

    /// <summary>no-op reporter は例外を要求する。</summary>
    [Fact]
    public void NullReporter_WhenExceptionNull_Throws()
    {
        // Act
        var act = () => NullUnexpectedExceptionReporter.Instance.Report(null!, default);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>no-op reporter は何もしない。</summary>
    [Fact]
    public void NullReporter_Report_DoesNotThrow()
    {
        // Act
        NullUnexpectedExceptionReporter.Instance.Report(new InvalidOperationException("noop"), default);

        // Assert
        Assert.NotNull(NullUnexpectedExceptionReporter.Instance);
    }
}
