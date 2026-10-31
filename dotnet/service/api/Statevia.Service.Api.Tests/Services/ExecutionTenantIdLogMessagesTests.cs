using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Services;
using Statevia.Runtime.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>手元の <c>TenantId</c> を載せる実行系 LoggerMessage のテスト。</summary>
public sealed class ExecutionTenantIdLogMessagesTests
{
    /// <summary>Worker の恒久失敗ログにテナント UUID が載る。</summary>
    [Fact]
    public void WorkItemPermanentFailure_IncludesTenantId()
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger("worker");
        var tenantId = TestTenantIds.DefaultTenantId;
        var executionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workItemId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        logger.WorkItemPermanentFailure(
            new InvalidOperationException("restore_invalid"),
            workItemId,
            executionId,
            "Start",
            "restore_invalid",
            tenantId);

        // Assert
        var line = Assert.Single(collector.Entries);
        Assert.Contains(tenantId.ToString("D"), line, StringComparison.Ordinal);
        Assert.Contains("TenantId=", line, StringComparison.Ordinal);
    }

    /// <summary>Worker の work item 失敗ログにテナント UUID が載る。</summary>
    [Fact]
    public void WorkItemFailed_IncludesTenantId()
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger("worker");
        var tenantId = TestTenantIds.T1TenantId;

        // Act
        logger.WorkItemFailed(new InvalidOperationException("transient"), Guid.NewGuid(), tenantId);

        // Assert
        var line = Assert.Single(collector.Entries);
        Assert.Contains(tenantId.ToString("D"), line, StringComparison.Ordinal);
    }

    /// <summary>プロセス全体の反復失敗ログには TenantId を埋め込まない。</summary>
    [Fact]
    public void WorkerIterationFailed_DoesNotIncludeTenantIdPlaceholder()
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger("worker");

        // Act
        logger.WorkerIterationFailed(new InvalidOperationException("loop"));

        // Assert
        var line = Assert.Single(collector.Entries);
        Assert.DoesNotContain("TenantId=", line, StringComparison.Ordinal);
    }

    /// <summary>Application の未開始 Cancel 終端ログにテナント UUID が載る。</summary>
    [Fact]
    public void UnstartedCancelTerminated_IncludesTenantId()
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<ExecutionService>();
        var tenantId = TestTenantIds.DefaultTenantId;
        var executionId = Guid.NewGuid();

        // Act
        logger.UnstartedCancelTerminated(executionId, tenantId);

        // Assert
        var line = Assert.Single(collector.Entries);
        Assert.Contains(tenantId.ToString("D"), line, StringComparison.Ordinal);
        Assert.Contains(executionId.ToString("D"), line, StringComparison.Ordinal);
    }

    /// <summary>Fork 展開失敗ログにテナント UUID が載る。</summary>
    [Fact]
    public void ForkExpansionAttemptFailed_IncludesTenantId()
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger("fork");
        var tenantId = TestTenantIds.T1TenantId;

        // Act
        logger.ForkExpansionAttemptFailed(
            new InvalidOperationException("expand"),
            attempt: 1,
            maxAttempts: 3,
            parentExecutionId: Guid.NewGuid(),
            forkNodeId: "fork-1",
            tenantId);

        // Assert
        var line = Assert.Single(collector.Entries);
        Assert.Contains(tenantId.ToString("D"), line, StringComparison.Ordinal);
    }

    private sealed class LogCollector : ILoggerProvider, IDisposable
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CollLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CollLogger : ILogger
        {
            private readonly List<string> _entries;

            public CollLogger(List<string> entries) => _entries = entries;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _entries.Add(formatter(state, exception));
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
