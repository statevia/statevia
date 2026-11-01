using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using System.Net.Mail;

namespace Statevia.Reference.Notification.Tests;

/// <summary>notification.send Action のチャネル制約と Module 公開の検証。</summary>
public sealed class NotificationSendActionStateTests
{
    /// <summary>email 以外のチャネルは失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_NonEmailChannel_Throws()
    {
        // Arrange
        var state = new NotificationSendActionState((_, _, _) => Task.CompletedTask);
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "slack",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = "noreply@example.com",
        };

        // Act / Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(CreateContext(), input, CancellationToken.None));
        Assert.Contains("channel=email", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>email チャネルは注入した送信処理を呼ぶ。</summary>
    [Fact]
    public async Task ExecuteAsync_EmailChannel_InvokesSender()
    {
        // Arrange
        MailMessage? captured = null;
        var state = new NotificationSendActionState((message, _, _) =>
        {
            captured = message;
            return Task.CompletedTask;
        });
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = "noreply@example.com",
        };

        // Act
        object? result;
        using (NotificationSmtpDispatchScope.Begin(SampleConnection()))
        {
            result = await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);
        }

        // Assert
        Assert.NotNull(captured);
        Assert.Equal("user@example.com", captured.To.ToString());
        Assert.Equal("hello", captured.Subject);
        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("email", dict["channel"]);
    }

    /// <summary>実送信が SMTP で拒否されたとき、サーバ詳細を含まない固定文で失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSmtpRejectsConnection_ThrowsWithoutServerDetail()
    {
        // Arrange
        var state = new NotificationSendActionState();
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = "noreply@example.com",
        };
        using var scope = NotificationSmtpDispatchScope.Begin(
            new NotificationSmtpDispatch("127.0.0.1", 1, "mailer", "secret", "noreply@example.com"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.ExecuteAsync(CreateContext(), input, cancellation.Token));

        // Assert
        Assert.Equal("SMTP send failed.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>入力がオブジェクトでないとき失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_NonObjectInput_Throws()
    {
        // Arrange
        var state = new NotificationSendActionState((_, _, _) => Task.CompletedTask);

        // Act / Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(CreateContext(), 42, CancellationToken.None));
        Assert.Contains("input fields", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>channel の大文字小文字は無視する。</summary>
    [Fact]
    public async Task ExecuteAsync_EmailChannelIgnoreCase_InvokesSender()
    {
        // Arrange
        var invoked = false;
        var state = new NotificationSendActionState((_, _, _) =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "Email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = "noreply@example.com",
        };

        // Act
        using (NotificationSmtpDispatchScope.Begin(SampleConnection()))
        {
            await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);
        }

        // Assert
        Assert.True(invoked);
    }

    /// <summary>送信が SmtpException のとき固定文で失敗し、例外文にパスワードを残さない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSenderThrowsSmtpException_ThrowsFixedMessage()
    {
        // Arrange
        const string password = "smtp-secret-value";
        var state = new NotificationSendActionState((_, _, _) =>
            throw new SmtpException("auth failed " + password));
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = "noreply@example.com",
        };

        // Act
        InvalidOperationException exception;
        using (NotificationSmtpDispatchScope.Begin(SampleConnection(password)))
        {
            exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                state.ExecuteAsync(CreateContext(), input, CancellationToken.None));
        }

        // Assert
        Assert.Equal("SMTP send failed.", exception.Message);
        Assert.DoesNotContain(password, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>既定の送信先が拒否したとき固定文で失敗し、パスワードを残さない。</summary>
    [Fact]
    public async Task ExecuteAsync_DefaultSender_WhenConnectionRefused_ThrowsFixedMessage()
    {
        // Arrange
        const string password = "smtp-secret-value";
        var state = new NotificationSendActionState();
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
            ["from"] = "noreply@example.com",
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        InvalidOperationException exception;
        using (NotificationSmtpDispatchScope.Begin(
            new NotificationSmtpDispatch("127.0.0.1", 1, "mailer", password, "noreply@example.com")))
        {
            exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                state.ExecuteAsync(CreateContext(), input, timeout.Token));
        }

        // Assert
        Assert.Equal("SMTP send failed.", exception.Message);
        Assert.DoesNotContain(password, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Module が send Action を公開する。</summary>
    [Fact]
    public void Module_ExposesSendAction()
    {
        // Arrange
        var module = new NotificationReferenceModule();
        var services = new ServiceCollection().BuildServiceProvider();

        // Act
        var actions = module.GetActions(services).ToArray();
        var registration = Assert.Single(actions);
        _ = registration.ExecutorFactory(services);

        // Assert
        Assert.Equal(NotificationReferenceActionIds.ModuleId, module.ModuleId);
        Assert.Equal(NotificationReferenceActionIds.Send, registration.ActionId);
        Assert.NotNull(registration.Publication);
    }

    /// <summary>csproj は Infrastructure.Notification を参照しない。</summary>
    [Fact]
    public void Project_DoesNotReferenceInfrastructureNotification()
    {
        // Arrange
        var csprojPath = FindCsproj();
        var xml = File.ReadAllText(csprojPath);

        // Assert
        Assert.DoesNotContain("Statevia.Infrastructure.Notification", xml, StringComparison.Ordinal);
    }

    private static string FindCsproj()
    {
        var candidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Statevia.Reference.Notification",
            "Statevia.Reference.Notification.csproj"));
        if (!File.Exists(candidate))
        {
            throw new InvalidOperationException($"Statevia.Reference.Notification.csproj was not found at '{candidate}'.");
        }

        return candidate;
    }

    private static NotificationSmtpDispatch SampleConnection(string password = "secret") =>
        new("smtp.example.com", 587, "mailer", password, "noreply@example.com");

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
        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

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
