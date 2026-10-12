using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using System.Net.Mail;

namespace Statevia.Reference.Notification.Tests;

/// <summary>notification.send が呼び出しスコープの接続だけを使う。</summary>
public sealed class NotificationSmtpDispatchScopeTests
{
    /// <summary>スコープが無い email 送信は失敗し、メッセージにパスワードが無い。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenScopeMissing_ThrowsWithoutSecret()
    {
        // Arrange
        var state = new NotificationSendActionState((_, _, _) => Task.CompletedTask);
        var input = EmailInput(from: "from@example.com");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.ExecuteAsync(CreateContext(), input, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP connection is not available for this execution.", exception.Message);
    }

    /// <summary>input.from が空ならスコープの既定 From を使う。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenFromMissing_UsesDefaultFrom()
    {
        // Arrange
        MailMessage? captured = null;
        var state = new NotificationSendActionState((message, _, _) =>
        {
            captured = message;
            return Task.CompletedTask;
        });
        IDisposable scope = NotificationSmtpDispatchScope.Begin(
            new NotificationSmtpDispatch("smtp.example.com", 2525, "mailer", "secret", "default@example.com"));
        try
        {
            await state.ExecuteAsync(CreateContext(), EmailInput(from: null), CancellationToken.None);
        }
        finally
        {
            scope.Dispose();
        }

        // Assert
        Assert.Equal("default@example.com", captured?.From?.Address);
        Assert.Null(NotificationSmtpDispatchScope.Current);
    }

    /// <summary>from と既定 From が両方空なら送らない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenFromAndDefaultFromMissing_Throws()
    {
        // Arrange
        var state = new NotificationSendActionState((_, _, _) => Task.CompletedTask);
        InvalidOperationException exception;
        using (NotificationSmtpDispatchScope.Begin(
            new NotificationSmtpDispatch("smtp.example.com", 25, null, null, null)))
        {
            exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                state.ExecuteAsync(CreateContext(), EmailInput(from: "  "), CancellationToken.None));
        }

        // Assert
        Assert.Equal("SMTP from address is not configured.", exception.Message);
    }

    /// <summary>スコープを閉じたあと接続は読めない。</summary>
    [Fact]
    public void Begin_WhenDisposed_ClearsCurrent()
    {
        // Arrange
        var connection = new NotificationSmtpDispatch("smtp.example.com", 587, "mailer", "secret", null);

        // Act
        var scope = NotificationSmtpDispatchScope.Begin(connection);
        var during = NotificationSmtpDispatchScope.Current;
        scope.Dispose();

        // Assert
        Assert.Equal("smtp.example.com", during?.Host);
        Assert.Null(NotificationSmtpDispatchScope.Current);
    }

    private static Dictionary<string, object?> EmailInput(string? from) =>
        new()
        {
            ["channel"] = "email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = from,
        };

    private static StateContext CreateContext() =>
        new()
        {
            Events = new StubEventProvider(),
            Store = new StubStateStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "Notify",
        };

    private sealed class StubEventProvider : IEventProvider
    {
        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Signal(string signalName)
        {
        }

        public void Resume(string nodeId, string eventName)
        {
        }

        public void PublishTopic(string topic, object? payloadSummary)
        {
        }
    }

    private sealed class StubStateStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }
}
