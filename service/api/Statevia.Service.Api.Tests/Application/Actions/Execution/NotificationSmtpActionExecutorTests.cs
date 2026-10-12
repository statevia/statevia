using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="NotificationSmtpActionExecutor"/> が接続を載せる範囲。</summary>
public sealed class NotificationSmtpActionExecutorTests
{
    /// <summary>notification.send 以外はローダーを呼ばず、内側へ元のリクエストを渡す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOtherAction_DoesNotLoadSmtp()
    {
        // Arrange
        var inner = new RecordingExecutor();
        var loader = new RecordingLoader();
        var sut = new NotificationSmtpActionExecutor(inner, loader);
        var request = Request("statevia.action.builtin.execution.noop");

        // Act
        var result = await sut.ExecuteAsync(request, CreateContext(), null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, loader.CallCount);
        Assert.Null(inner.ExecutionRequest?.NotificationSmtp);
    }

    /// <summary>notification.send は実行テナントの接続を載せてから内側へ渡す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenNotificationSend_AttachesConnection()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var connection = new NotificationSmtpDispatch("smtp.example.com", 2525, "mailer", "secret", null);
        var inner = new RecordingExecutor();
        var loader = new RecordingLoader(connection);
        var sut = new NotificationSmtpActionExecutor(inner, loader);
        var request = Request(NotificationSmtpDispatch.ActionId, tenantId.ToString("D"));

        // Act
        var result = await sut.ExecuteAsync(request, CreateContext(), null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(tenantId, loader.TenantId);
        Assert.Equal("smtp.example.com", inner.ExecutionRequest?.NotificationSmtp?.Host);
        Assert.Null(request.NotificationSmtp);
    }

    private static ActionExecutionRequest Request(string actionId, string? tenantId = null) =>
        new()
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = actionId,
            TenantId = tenantId ?? ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

    private static StateContext CreateContext() =>
        new()
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };

    private sealed class RecordingExecutor : IActionExecutor
    {
        public ActionExecutionRequest? ExecutionRequest { get; private set; }

        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            StateContext stateContext,
            object? runtimeInput,
            CancellationToken cancellationToken)
        {
            ExecutionRequest = request;
            return Task.FromResult(new ActionExecutionResult { Success = true });
        }
    }

    private sealed class RecordingLoader : INotificationSmtpConnectionLoader
    {
        private readonly NotificationSmtpDispatch _connection;

        public RecordingLoader(NotificationSmtpDispatch? connection = null) =>
            _connection = connection ?? new NotificationSmtpDispatch("smtp.example.com", 25, null, null, null);

        public int CallCount { get; private set; }

        public Guid TenantId { get; private set; }

        public Task<NotificationSmtpDispatch> LoadAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            CallCount++;
            TenantId = tenantId;
            return Task.FromResult(_connection);
        }
    }
}
