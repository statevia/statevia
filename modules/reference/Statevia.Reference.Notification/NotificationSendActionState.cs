using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using System.Net.Mail;

namespace Statevia.Reference.Notification;

/// <summary>email 通知を送るリファレンス Action。</summary>
internal sealed class NotificationSendActionState : IState<object?, object?>
{
    private const string MissingConnectionMessage = "Notification SMTP connection is not available for this execution.";
    private const string MissingFromMessage = "SMTP from address is not configured.";
    private const string SendFailedMessage = "SMTP send failed.";

    private readonly Func<MailMessage, NotificationSmtpDispatch, CancellationToken, Task> _sendAsync;

    /// <summary>呼び出しスコープの SMTP 接続で送信する。</summary>
    public NotificationSendActionState()
        : this(static (message, connection, cancellationToken) =>
            SmtpMailSender.SendAsync(connection, message, cancellationToken))
    {
    }

    /// <summary>テスト用に送信処理を差し替える。</summary>
    /// <param name="sendAsync">組み立て済みメールと接続を受け取るデリゲート。</param>
    internal NotificationSendActionState(Func<MailMessage, NotificationSmtpDispatch, CancellationToken, Task> sendAsync) =>
        _sendAsync = sendAsync;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        _ = ctx;
        if (!ActionInputReader.TryReadObject(input, out var fields))
            throw new ArgumentException("notification.send action requires input fields.");

        var channel = ActionInputReader.RequireString(fields, "channel");
        if (!string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("notification.send action supports channel=email only.");

        var connection = NotificationSmtpDispatchScope.Current
            ?? throw new InvalidOperationException(MissingConnectionMessage);
        var to = ActionInputReader.RequireString(fields, "to");
        var subject = ActionInputReader.RequireString(fields, "subject");
        var body = ActionInputReader.RequireString(fields, "body");
        var from = ActionInputReader.OptionalString(fields, "from");
        var fromAddress = string.IsNullOrWhiteSpace(from) ? connection.DefaultFrom : from;
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException(MissingFromMessage);

        using var message = CreateMessage(to, subject, body, fromAddress);
        try
        {
            await _sendAsync(message, connection, ct).ConfigureAwait(false);
        }
        catch (SmtpException)
        {
            throw new InvalidOperationException(SendFailedMessage);
        }

        return new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["messageId"] = null,
        };
    }

    private static MailMessage CreateMessage(string to, string subject, string body, string fromAddress)
    {
        var message = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = subject,
            Body = body,
        };
        message.To.Add(to);
        return message;
    }
}
