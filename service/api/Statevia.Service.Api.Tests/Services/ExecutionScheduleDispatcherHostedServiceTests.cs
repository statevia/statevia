using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Runtime.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>Schedule Dispatcher が発火ユースケースだけを呼ぶことの単体テスト。</summary>
public sealed class ExecutionScheduleDispatcherHostedServiceTests
{
    /// <summary>イテレーションごとに DispatchDueAsync を呼ぶ。</summary>
    [Fact]
    public async Task ExecuteAsync_CallsDispatchDue()
    {
        // Arrange
        var dispatcher = new RecordingDispatchService();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionScheduleDispatchService>(dispatcher);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var sut = new ExecutionScheduleDispatcherHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionScheduleDispatcherHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(dispatcher.CallCount >= 1);
        Assert.Equal(64, dispatcher.LastLimit);
    }

    /// <summary>例外でもポーリングを継続する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDispatchThrows_ContinuesPolling()
    {
        // Arrange
        var dispatcher = new RecordingDispatchService { ThrowOnDispatch = true };
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionScheduleDispatchService>(dispatcher);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sut = new ExecutionScheduleDispatcherHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionScheduleDispatcherHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(dispatcher.CallCount >= 1);
    }

    private sealed class RecordingDispatchService : IExecutionScheduleDispatchService
    {
        public int CallCount { get; private set; }
        public int LastLimit { get; private set; }
        public bool ThrowOnDispatch { get; set; }

        public Task<int> DispatchDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
        {
            _ = utcNow;
            _ = cancellationToken;
            CallCount++;
            LastLimit = limit;
            if (ThrowOnDispatch)
                throw new InvalidOperationException("dispatch failed");
            return Task.FromResult(0);
        }
    }
}
